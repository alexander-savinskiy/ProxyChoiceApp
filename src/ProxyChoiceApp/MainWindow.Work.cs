using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ProxyChoiceApp.Localization;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;
using Drawing = System.Drawing;

namespace ProxyChoiceApp;

public sealed record DiagnosticResult(string Title, string Status, string Detail);

public partial class MainWindow
{
    public ObservableCollection<RemoteComputer> RemoteComputers { get; } = new();
    public ObservableCollection<DiagnosticResult> DiagnosticResults { get; } = new();
    private readonly Drawing.Icon _grayIcon, _amberIcon;
    private bool _wantConnection, _reconnecting;
    private int _healthFailures;
    private CancellationTokenSource? _retryCancellation;
    private Task? _reconnectTask;
    private Guid? _editingComputer;
    private enum ConnectionVisual { Disconnected, Connecting, Connected, Error }

    private void SetConnectionVisual(ConnectionVisual state, string description)
    {
        var name = state switch { ConnectionVisual.Connected => "connected", ConnectionVisual.Connecting => "connecting", ConnectionVisual.Error => "error", _ => "disconnected" };
        var color = state switch { ConnectionVisual.Connected => "#16A56B", ConnectionVisual.Connecting => "#D49A26", ConnectionVisual.Error => "#E05151", _ => "#7F91A5" };
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/" + name + ".ico"));
        // Use the entire state icon. Windows overlays caused the old red-shield/green-dot combination.
        TaskbarItemInfo.Overlay = null; TaskbarItemInfo.Description = "ProxyChoicePro · " + description;
        _tray.Icon = state switch { ConnectionVisual.Connected => _greenIcon, ConnectionVisual.Connecting => _amberIcon, ConnectionVisual.Error => _redIcon, _ => _grayIcon };
        var tooltip = "ProxyChoicePro · " + description; _tray.Text = tooltip[..Math.Min(63, tooltip.Length)];
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ConnectionGrid is null || ServerCard is null || ProfileCard is null) return;
        var narrow = ActualWidth < 940;
        Grid.SetColumn(ProfileCard, 0); Grid.SetColumnSpan(ProfileCard, narrow ? 3 : 1);
        Grid.SetRow(ServerCard, narrow ? 1 : 0); Grid.SetColumn(ServerCard, narrow ? 0 : 2); Grid.SetColumnSpan(ServerCard, narrow ? 3 : 1);
    }
    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialised || _loading) return;
        _profiles.DarkMode = DarkModeBox.IsChecked == true; ApplyTheme(); SaveGlobalPreferences();
    }
    private void ApplyTheme()
    {
        var dark = _profiles.DarkMode;
        var palette = new[] {
            ("Canvas", dark ? "#111B27" : "#F2F5FA"), ("Surface", dark ? "#1B2939" : "#FFFFFF"),
            ("Field", dark ? "#152131" : "#F8FAFD"), ("Ink", dark ? "#EDF3F9" : "#162B3D"),
            ("Muted", dark ? "#AFBFD0" : "#586D80"), ("Line", dark ? "#40556D" : "#CBD5E1"),
            ("Frame", dark ? "#7288A2" : "#8296AD"), ("Accent", dark ? "#49C6B6" : "#087F79"),
            ("AccentSoft", dark ? "#1F4145" : "#E4F4F1"), ("PrimaryInk", dark ? "#102428" : "#FFFFFF"),
            ("ErrorSurface", dark ? "#452D35" : "#FFF1EE"), ("ErrorInk", dark ? "#FFB5AA" : "#A74537")
        };
        foreach (var (key, color) in palette) Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        if (Notice.Visibility == Visibility.Visible) ShowNotice(NoticeText.Text, _noticeError);
    }
    private async void ReconnectOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialised || _loading) return;
        _profiles.AutoReconnect = AutoReconnectBox.IsChecked == true; SaveGlobalPreferences();
        if (!_profiles.AutoReconnect && _reconnecting) await CancelReconnectAsync();
    }
    private void SaveGlobalPreferences() { try { if (!_preview) SecureStorage.SaveProfiles(_profiles); } catch (Exception error) { ReportError(error); } }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e) { if (_initialised && !_loading) { UpdateMode(); SetBusy(_busy); } }
    private void UpdateMode()
    {
        var work = ModeBox.SelectedIndex == 1;
        WorkFields.Visibility = work ? Visibility.Visible : Visibility.Collapsed; ProxyFields.Visibility = work ? Visibility.Collapsed : Visibility.Visible;
        AppsModeHint.Text = L.T(work ? "WorkAppsHint" : "SelectAppsHint");
        ProxyMandatoryCard.Visibility = work ? Visibility.Collapsed : Visibility.Visible;
        StoreBox.IsEnabled = !work;
        AppsList.IsEnabled = ProxyAppsButtons.IsEnabled = !work;
        LoadWorkDescription();
        ResetDiagnostics();
    }
    private void LoadWorkDescription()
    {
        var work = _settings.WorkNetwork;
        WorkProfileText.Text = work is null ? L.T("ImportWorkHint") : L.F("WorkImported", work.SourceName);
        WorkAddressText.Text = work is null ? "" : L.F("VpnAddress", string.Join(", ", work.Addresses));
    }
    private void LoadWorkFields()
    {
        var work = _settings.WorkNetwork;
        WorkRoutesBox.Text = work is null ? "" : string.Join(", ", work.Routes);
        ProbeHostBox.Text = work?.ProbeHost ?? ""; ProbePortBox.Text = (work?.ProbePort ?? 80).ToString(); LoadWorkDescription();
    }
    private WorkNetworkProfile? ReadWorkFields()
    {
        var work = _settings.WorkNetwork?.Copy();
        if (work is null) return null;
        work.Routes = WireGuardImporter.Split(WorkRoutesBox.Text); work.ProbeHost = ProbeHostBox.Text.Trim();
        if (!int.TryParse(ProbePortBox.Text, out var port)) throw new ArgumentException(L.T("InvalidPort"));
        work.ProbePort = port; return work;
    }
    private void ImportWork_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = L.T("WireGuardFilter"), CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try { if (new FileInfo(dialog.FileName).Length > 131072) throw new ArgumentException(L.T("ProfileTooLarge")); ImportWork(File.ReadAllText(dialog.FileName), Path.GetFileName(dialog.FileName)); }
        catch (Exception error) { ReportError(error); }
    }
    private void PasteWork_Click(object sender, RoutedEventArgs e)
    {
        try { ImportWork(Clipboard.GetText(), L.T("ClipboardProfile")); } catch (Exception error) { ReportError(error); }
    }
    private void ImportWork(string text, string source)
    {
        if (_busy || _session is not null || _wantConnection) return;
        var imported = WireGuardImporter.Parse(text, source);
        _settings.WorkNetwork = imported.Profile; _settings.Mode = ConnectionMode.WorkNetwork;
        _settings.Host = imported.Host; _settings.Port = imported.Port;
        HostBox.Text = imported.Host; PortBox.Text = imported.Port.ToString(); ModeBox.SelectedIndex = 1; LoadWorkFields(); UpdateMode();
        if (imported.Profile.Routes.Count == 0) ShowNotice(L.T("FullProfileNeedsRoutes"), false);
        else { imported.Profile.Validate(); CommitProfiles(DraftProfiles(true)); ShowNotice(L.T("WorkImportSuccess"), false); ShowSubnetConflicts(imported.Profile); }
    }
    private void ShowSubnetConflicts(WorkNetworkProfile profile)
    {
        var conflicts = NetworkDiagnostics.FindConflicts(profile.Routes);
        if (conflicts.Count == 0) return;
        var text = L.T("SubnetWarning") + " " + string.Join("; ", conflicts.Select(c => c.WorkNetwork + " ↔ " + c.LocalNetwork + " (" + c.Adapter + ")"));
        ShowNotice(text, true); AddLog(text);
    }

    private void RefreshComputers()
    {
        var selected = _editingComputer;
        RemoteComputers.Clear(); foreach (var pc in _profiles.RemoteComputers) RemoteComputers.Add(pc);
        ComputersList.SelectedItem = RemoteComputers.FirstOrDefault(p => p.Id == selected);
    }
    private void Computer_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ComputersList.SelectedItem is not RemoteComputer pc) return;
        _editingComputer = pc.Id; ComputerNameBox.Text = pc.Name; ComputerHostBox.Text = pc.Host;
        ComputerPortBox.Text = pc.Port.ToString(); ComputerUserBox.Text = pc.Username;
    }
    private void NewComputer_Click(object sender, RoutedEventArgs e)
    {
        _editingComputer = null; ComputersList.SelectedItem = null; ComputerNameBox.Clear(); ComputerHostBox.Clear(); ComputerUserBox.Clear(); ComputerPortBox.Text = "3389"; ComputerNameBox.Focus();
    }
    private RemoteComputer ReadComputer()
    {
        if (!int.TryParse(ComputerPortBox.Text, out var port)) throw new ArgumentException(L.T("InvalidPort"));
        var pc = new RemoteComputer { Id = _editingComputer ?? Guid.NewGuid(), Name = ComputerNameBox.Text, Host = ComputerHostBox.Text, Port = port, Username = ComputerUserBox.Text };
        pc.Validate(); return pc;
    }
    private void SaveComputer_Click(object sender, RoutedEventArgs e)
    {
        try {
            var pc = ReadComputer(); var next = _profiles.Copy();
            if (next.RemoteComputers.Any(p => p.Id != pc.Id && p.Name.Equals(pc.Name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException(L.T("DuplicateComputer"));
            next.RemoteComputers.RemoveAll(p => p.Id == pc.Id); next.RemoteComputers.Add(pc);
            if (!_preview) SecureStorage.SaveProfiles(next); _profiles = next; _editingComputer = pc.Id; RefreshComputers(); ShowNotice(L.T("ComputerSaved"), false);
        } catch (Exception error) { ReportError(error); }
    }
    private void RemoveComputer_Click(object sender, RoutedEventArgs e)
    {
        if (_editingComputer is null) return;
        try { var next = _profiles.Copy(); next.RemoteComputers.RemoveAll(p => p.Id == _editingComputer); if (!_preview) SecureStorage.SaveProfiles(next); _profiles = next; NewComputer_Click(sender, e); RefreshComputers(); }
        catch (Exception error) { ReportError(error); }
    }
    private RemoteComputer RequireWorkComputer()
    {
        if (_session is null || _settings.Mode != ConnectionMode.WorkNetwork || !_healthy) throw new ArgumentException(L.T("ConnectWorkFirst"));
        var pc = ReadComputer();
        if (!NetworkDiagnostics.InsideWork(_settings.WorkNetwork!, pc.Host)) throw new ArgumentException(L.T("ComputerOutsideWork"));
        return pc;
    }
    private void OpenRdp_Click(object sender, RoutedEventArgs e)
    {
        try { var pc = RequireWorkComputer(); NetworkDiagnostics.OpenRdp(pc); AddLog(L.F("RdpOpened", pc.Name)); }
        catch (Exception error) { ReportError(error); }
    }
    private async void CheckRdp_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try { var pc = RequireWorkComputer(); await _session!.ProbeTcpAsync(pc.Host, pc.Port, _connection!.Token); ShowNotice(L.F("RdpAvailable", pc.Endpoint), false); }
        catch (Exception error) { ReportError(error); }
    }
    private void ResetDiagnostics()
    {
        DiagnosticResults.Clear();
        foreach (var key in new[] { "DiagnosticEngine", ModeBox.SelectedIndex == 1 ? "DiagnosticNetwork" : "DiagnosticProxy", "DiagnosticRdp", "DiagnosticSubnets" }) DiagnosticResults.Add(new(L.T(key), L.T("NotChecked"), ""));
    }
    private void SetDiagnostic(int index, string status, string detail) => DiagnosticResults[index] = DiagnosticResults[index] with { Status = L.T(status), Detail = detail };
    private async void Diagnose_Click(object sender, RoutedEventArgs e) => await DiagnoseAsync();
    private async Task DiagnoseAsync()
    {
        if (_busy || _closing) return;
        SetBusy(true); using var operation = new CancellationTokenSource(); _operation = operation; CoreSession? temporary = null;
        try {
            if (_preview) throw new ArgumentException(L.T("PreviewNoConnect"));
            var settings = _session is null ? ReadSettings() : _settings;
            _settings = settings; ResetDiagnostics(); MainTabs.SelectedItem = DiagnosticsTab;
            Notice.Visibility = Visibility.Collapsed;
            var session = _session ?? (temporary = await CoreSession.StartAsync(settings, await ResolveServerAsync(settings, operation.Token), false, operation.Token));
            SetDiagnostic(0, "Available", L.T(_session is null ? "EngineReady" : "EngineTunnelReady"));
            if (settings.Mode == ConnectionMode.WorkNetwork) {
                var work = settings.WorkNetwork!;
                var conflicts = NetworkDiagnostics.FindConflicts(work.Routes);
                SetDiagnostic(3, conflicts.Count == 0 ? "Available" : "Attention", conflicts.Count == 0 ? L.T("NoSubnetConflict") : L.T("SubnetWarning") + " " + string.Join("; ", conflicts.Select(c => c.WorkNetwork + " ↔ " + c.LocalNetwork)));
                try { await session.ProbeTcpAsync(work.ProbeHost, work.ProbePort, operation.Token); SetDiagnostic(1, "Available", L.F("WorkNetworkAvailable", work.ProbeHost, work.ProbePort)); }
                catch (Exception error) when (!operation.IsCancellationRequested) { SetDiagnostic(1, "Unavailable", L.T("WorkProbeHint") + " " + Sanitize(error.Message)); }
                var pc = ComputersList.SelectedItem as RemoteComputer ?? RemoteComputers.FirstOrDefault(p => NetworkDiagnostics.InsideWork(work, p.Host));
                if (pc is not null && NetworkDiagnostics.InsideWork(work, pc.Host)) {
                    try { await session.ProbeTcpAsync(pc.Host, pc.Port, operation.Token); SetDiagnostic(2, "Available", L.F("RdpAvailable", pc.Endpoint)); }
                    catch (Exception error) when (!operation.IsCancellationRequested) { SetDiagnostic(2, "Unavailable", L.T("RdpProbeHint") + " " + Sanitize(error.Message)); }
                } else SetDiagnostic(2, "NotChecked", L.T("AddComputerHint"));
            } else {
                var ip = await session.ReadProxyIpAsync(operation.Token); SetDiagnostic(1, "Available", L.F("IpSuccess", ip));
                SetDiagnostic(2, "NotChecked", L.T("ConnectWorkFirst")); SetDiagnostic(3, "NotChecked", L.T("WorkAppsHint"));
            }
            if (_session is null) PersistSettings(settings);
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception error) {
            SetDiagnostic(error is CoreStartupException ? 0 : 1, "Unavailable", Sanitize(error.Message));
            ReportError(error);
        }
        finally { if (temporary is not null) await temporary.DisposeAsync(); if (ReferenceEquals(_operation, operation)) _operation = null; if (!_closing) SetBusy(false); }
    }

    private void SubscribeNetworkEvents()
    {
        if (_preview) return;
        NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChanged; SystemEvents.PowerModeChanged += PowerModeChanged;
    }
    private void UnsubscribeNetworkEvents()
    {
        NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChanged; SystemEvents.PowerModeChanged -= PowerModeChanged;
    }
    private void NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable && !_closing) Dispatcher.BeginInvoke(() => RequestReconnect(L.T("NetworkRestored")));
    }
    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume && !_closing) Dispatcher.BeginInvoke(() => RequestReconnect(L.T("ResumeReconnect")));
    }
    private void RequestReconnect(string reason)
    {
        if (!_wantConnection || !_profiles.AutoReconnect || _reconnecting || _busy || _closing || _preview) return;
        _reconnecting = true; _reconnectTask = ReconnectLoopAsync(reason);
    }
    private async Task CancelReconnectAsync()
    {
        _wantConnection = false; _retryCancellation?.Cancel();
        if (_reconnectTask is not null) await _reconnectTask;
        SetDisconnectedVisuals(); SetBusy(false);
    }
    private async Task ReconnectLoopAsync(string reason)
    {
        using var retry = CancellationTokenSource.CreateLinkedTokenSource(_discovery.Token); _retryCancellation = retry;
        var settings = _settings.Copy(); AddLog(reason);
        try {
            await ConnectionRecovery.RunAsync(
                async () => { SetBusy(true); await DisconnectAsync(preserveIntent: true); },
                async token => { SetBusy(true); await ConnectCoreAsync(settings, token); AddLog(L.T("RestoredLog")); },
                NetworkInterface.GetIsNetworkAvailable,
                seconds => { StatusTitle.Text = L.T("Reconnecting"); StatusDescription.Text = L.F("ReconnectWaiting", seconds); SetConnectionVisual(ConnectionVisual.Connecting, L.T("Reconnecting")); SetBusy(false); },
                error => AddLog(L.T("ReconnectFailed") + " " + Sanitize(error.Message)), retry.Token);
        } catch (OperationCanceledException) when (retry.IsCancellationRequested) { }
        catch (Exception error) { if (!_closing) ReportError(error); }
        finally {
            if (ReferenceEquals(_retryCancellation, retry)) _retryCancellation = null;
            _reconnecting = false;
            if (!_wantConnection && _session is not null) await DisconnectAsync();
            if (!_closing) { if (_session is null) SetDisconnectedVisuals(); SetBusy(false); }
        }
    }
    internal async Task CaptureProPreviewsAsync(string directory)
    {
        await _catalogReady.Task.WaitAsync(TimeSpan.FromSeconds(12)); Directory.CreateDirectory(directory);
        foreach (var language in new[] { "en", "ru" }) {
            LanguagePicker.SelectedValue = language; DarkModeBox.IsChecked = true; Width = 1080; Height = 830; MainTabs.SelectedItem = ConnectionTab;
            await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
            SavePreview(Path.Combine(directory, "Clean-" + language + "-dark.png"));
        }
        var demo = new ProxyProfile { Name = "Work VPN", Settings = new ProxySettings { Mode = ConnectionMode.WorkNetwork, Host = "vpn.example.invalid", Port = 51820,
            WorkNetwork = new WorkNetworkProfile { Addresses = new() { "192.0.2.2/24" }, AllowedIps = new() { "192.0.2.1/32", "198.51.100.0/24" }, Routes = new() { "192.0.2.1/32", "198.51.100.0/24" }, ProbeHost = "198.51.100.1", SourceName = "work-template.conf" } } };
        _profiles.Profiles.Add(demo); _profiles.SelectedProfileId = demo.Id;
        _profiles.RemoteComputers.Add(new RemoteComputer { Name = "Office PC", Host = "198.51.100.85" }); RefreshProfiles(); RefreshComputers();
        ComputersList.SelectedIndex = 0;
        foreach (var language in L.Supported) foreach (var dark in new[] { false, true }) {
            LanguagePicker.SelectedValue = language.Code; DarkModeBox.IsChecked = dark; Width = 1080; Height = 830; MainTabs.SelectedItem = ConnectionTab;
            await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
            SavePreview(Path.Combine(directory, "Main-" + language.Code + (dark ? "-dark" : "-light") + ".png"));
            if (language.Code == "ru") {
                foreach (var tab in new[] { ComputersTab, AppsTab, DiagnosticsTab, LogTab, HelpTab }) {
                    MainTabs.SelectedItem = tab; await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
                    SavePreview(Path.Combine(directory, tab.Name + (dark ? "-dark" : "-light") + ".png"));
                }
            }
        }
        foreach (var language in new[] { "ru", "de", "zh" }) {
            LanguagePicker.SelectedValue = language; DarkModeBox.IsChecked = true; Width = 760; Height = 600; MainTabs.SelectedItem = ConnectionTab;
            await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle); SavePreview(Path.Combine(directory, "Compact-" + language + ".png"));
        }
        _profiles.SelectedProfileId = _profiles.Profiles[0].Id;
        _profiles.Selected.Settings.Host = "proxy.example.invalid"; RefreshProfiles();
        foreach (var language in L.Supported) foreach (var dark in new[] { false, true }) {
            LanguagePicker.SelectedValue = language.Code; DarkModeBox.IsChecked = dark; Width = 1080; Height = 830; MainTabs.SelectedItem = ConnectionTab;
            await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
            SavePreview(Path.Combine(directory, "Proxy-" + language.Code + (dark ? "-dark" : "-light") + ".png"));
        }
        LanguagePicker.SelectedValue = "ru";
        foreach (var dark in new[] { false, true }) {
            DarkModeBox.IsChecked = dark; MainTabs.SelectedItem = AppsTab;
            await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
            SavePreview(Path.Combine(directory, "ProxyApps" + (dark ? "-dark" : "-light") + ".png"));
        }
    }
}
