using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Win32;
using ProxyChoiceApp.Localization;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ProxyChoiceApp;

public partial class MainWindow : Window
{
    public ObservableCollection<AppChoice> Applications { get; } = new();
    public ObservableCollection<ProxyProfile> Profiles { get; } = new();
    private ProxyProfiles _profiles;
    private ProxySettings _settings;
    private readonly bool _preview;
    private CoreSession? _session;
    private CancellationTokenSource? _operation, _connection;
    private readonly CancellationTokenSource _discovery = new();
    private readonly TaskCompletionSource _catalogReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer _timer;
    private readonly StringBuilder _log = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly Drawing.Icon _redIcon, _greenIcon;
    private readonly Forms.ToolStripItem _trayOpen, _trayDisconnect, _trayExit;
    private bool _busy, _closing, _allowClose, _healthy, _initialised, _loading, _syncing, _tickRunning;
    private Task? _healthTask;
    private DateTime _connectedAt, _lastHealthAt;
    private string? _serverIp;
    private readonly TrafficMeter _traffic = new();
    private readonly Stopwatch _trafficClock = new();

    public MainWindow(ProxyProfiles profiles, bool preview)
    {
        profiles.Normalize(); _profiles = profiles; _settings = profiles.SelectedSettings(); _preview = preview;
        L.Current.Set(profiles.Language);
        InitializeComponent(); DataContext = this;
        _loading = true;
        LanguagePicker.ItemsSource = L.Supported; LanguagePicker.SelectedValue = profiles.Language;
        _loading = false;
        foreach (var path in profiles.ProxyApps) AddApp(path, selected: true);
        RefreshProfiles();
        _redIcon = LoadTrayIcon("disconnected"); _greenIcon = LoadTrayIcon("connected");
        var menu = new Forms.ContextMenuStrip();
        _trayOpen = menu.Items.Add("", null, (_, _) => Dispatcher.BeginInvoke(RestoreWindow));
        _trayDisconnect = menu.Items.Add("", null, (_, _) => Dispatcher.BeginInvoke(async () => {
            if (_busy || _session is null) return;
            SetBusy(true); try { await DisconnectAsync(); } catch (Exception error) { ReportError(error); } finally { SetBusy(false); }
        }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _trayExit = menu.Items.Add("", null, (_, _) => Dispatcher.BeginInvoke(Close));
        _tray = new Forms.NotifyIcon { Icon = _redIcon, Text = "ProxyChoiceApp", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(RestoreWindow);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick; _timer.Start();
        _initialised = true; RefreshLanguage(); SetDisconnectedVisuals(); SetBusy(false);
        AddLog(L.T("ReadyLog"));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try {
            var found = await Task.Run(() => AppCatalog.Discover(_discovery.Token), _discovery.Token);
            if (_closing) return;
            foreach (var app in found) {
                if (app.Path.Length > 0) {
                    var existing = Applications.FirstOrDefault(a => a.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase) && a.Name == app.Name);
                    if (existing is not null) { app.UseProxy = existing.UseProxy; Unhook(existing); Applications.Remove(existing); }
                    else app.UseProxy = Applications.Any(a => a.UseProxy && a.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase));
                }
                Hook(app); Applications.Add(app);
            }
            if (!_profiles.DefaultsInitialized) {
                foreach (var app in Applications.Where(a => a.IsYouTube && a.Available)) app.UseProxy = true;
                _profiles.DefaultsInitialized = true;
                if (!_preview) PersistSettings(ReadSettings(false));
            }
            UpdateSelectionSummary();
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception error) { if (!_closing) AddLog(L.T("DiscoveryError") + " " + error.Message); }
        finally { _catalogReady.TrySetResult(); }
    }

    internal async Task CapturePreviewsAsync(string directory)
    {
        await _catalogReady.Task.WaitAsync(TimeSpan.FromSeconds(12));
        Directory.CreateDirectory(directory);
        foreach (var language in L.Supported) {
            LanguagePicker.SelectedValue = language.Code;
            MainTabs.SelectedIndex = 0;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SavePreview(Path.Combine(directory, "Main-" + language.Code + ".png"));
            if (language.Code is "ru" or "en" or "de" or "es") {
                MainTabs.SelectedIndex = 2;
                HelpScroll.ScrollToTop();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                SavePreview(Path.Combine(directory, "Help-" + language.Code + ".png"));
            }
        }
        LanguagePicker.SelectedValue = "ru"; MainTabs.SelectedIndex = 0;
    }
    private void SavePreview(string path)
    {
        UpdateLayout();
        var content = (FrameworkElement)Content;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) {
            drawing.DrawRectangle(Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            drawing.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }

    private void RefreshProfiles()
    {
        _loading = true;
        try {
            Profiles.Clear(); foreach (var profile in _profiles.Profiles) Profiles.Add(profile);
            ProfilePicker.SelectedItem = _profiles.Selected; _settings = _profiles.SelectedSettings();
            HostBox.Text = _settings.Host; PortBox.Text = _settings.Port.ToString();
            UsernameBox.Text = _settings.Username; PasswordBox.Password = _settings.Password;
            ProtocolBox.SelectedIndex = (int)_settings.Protocol; RememberBox.IsChecked = _settings.RememberPassword;
            UdpBox.IsChecked = _profiles.EnableUdp; StoreBox.IsChecked = _profiles.ProxyStore;
            DeleteProfileButton.IsEnabled = Profiles.Count > 1;
            foreach (var app in Applications) app.UseProxy = app.Path.Length > 0 && _profiles.ProxyApps.Contains(app.Path, StringComparer.OrdinalIgnoreCase);
        }
        finally { _loading = false; }
        if (_initialised) { UpdateProtocol(); UpdateSelectionSummary(); }
    }

    private ProxyProfiles DraftProfiles(bool validate = false)
    { var next = _profiles.Copy(); next.UpdateSelected(ReadSettings(validate)); return next; }
    private void CommitProfiles(ProxyProfiles next)
    { if (!_preview) SecureStorage.SaveProfiles(next); _profiles = next; RefreshProfiles(); }
    private void PersistSettings(ProxySettings settings)
    {
        var next = _profiles.Copy(); next.UpdateSelected(settings);
        if (!_preview) SecureStorage.SaveProfiles(next);
        _profiles = next; _settings = settings;
    }
    private ProxySettings ReadSettings(bool validate = true)
    {
        if (!int.TryParse(PortBox.Text.Trim(), out var port)) throw new ArgumentException(L.T("InvalidPort"));
        var settings = new ProxySettings {
            Host = HostBox.Text.Trim(), Port = port, Username = UsernameBox.Text, Password = PasswordBox.Password,
            Protocol = (ProxyProtocol)Math.Max(0, ProtocolBox.SelectedIndex), RememberPassword = RememberBox.IsChecked == true,
            ProxyApps = Applications.Where(a => a.UseProxy && a.Path.Length > 0).Select(a => a.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ProxyStore = StoreBox.IsChecked == true, EnableUdp = UdpBox.IsChecked == true,
            Language = L.Current.Code, DefaultsInitialized = _profiles.DefaultsInitialized
        };
        if (validate) settings.Validate();
        return settings;
    }

    private void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_initialised || ProfilePicker.SelectedItem is not ProxyProfile selected || selected.Id == _profiles.SelectedProfileId) return;
        try {
            if (_busy || _session is not null) throw new ArgumentException(L.T("DisconnectToSwitch"));
            var next = DraftProfiles(); next.SelectedProfileId = selected.Id; CommitProfiles(next);
            ProxyIpText.Text = "—"; Notice.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) {
            _loading = true; ProfilePicker.SelectedItem = Profiles.First(p => p.Id == _profiles.SelectedProfileId); _loading = false; ReportError(error);
        }
    }
    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _session is not null) return;
        var dialog = new ProfileNameWindow(L.T("AddProfile"), _profiles.NextName(), name => _profiles.CheckName(name)) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try { var next = DraftProfiles(); next.Add(dialog.ProfileName); CommitProfiles(next); ShowNotice(L.T("ProfileAdded"), false); }
        catch (Exception error) { ReportError(error); }
    }
    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _session is not null) return;
        var dialog = new ProfileNameWindow(L.T("RenameProfile"), _profiles.Selected.Name, name => _profiles.CheckName(name, _profiles.SelectedProfileId)) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try { var next = DraftProfiles(); next.Rename(dialog.ProfileName); CommitProfiles(next); ShowNotice(L.T("ProfileSaved"), false); }
        catch (Exception error) { ReportError(error); }
    }
    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _session is not null || Profiles.Count <= 1) return;
        if (MessageBox.Show(this, L.F("DeleteProfileQuestion", _profiles.Selected.Name), "ProxyChoiceApp", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { var next = DraftProfiles(); next.RemoveSelected(); CommitProfiles(next); ShowNotice(L.T("ProfileRemoved"), false); }
        catch (Exception error) { ReportError(error); }
    }
    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _session is not null) return;
        try { CommitProfiles(DraftProfiles(true)); ShowNotice(L.T(_preview ? "PreviewSaved" : "ProfileSaved"), false); }
        catch (Exception error) { ReportError(error); }
    }

    private static Drawing.Icon LoadTrayIcon(string name)
    {
        using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{name}.ico"))!.Stream;
        using var icon = new Drawing.Icon(stream); return (Drawing.Icon)icon.Clone();
    }
    private void SetIndicator(bool healthy, string description)
    {
        _healthy = healthy;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(healthy ? "#16A56B" : "#E05151"));
        StatusDot.Fill = brush;
        Icon = BitmapFrame.Create(new Uri($"pack://application:,,,/Assets/{(healthy ? "connected" : "disconnected")}.ico"));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new EllipseGeometry(new Point(8, 8), 8, 8)));
        group.Children.Add(new GeometryDrawing(brush, null, new EllipseGeometry(new Point(8, 8), 6.3, 6.3)));
        var overlay = new DrawingImage(group); overlay.Freeze();
        TaskbarItemInfo.Overlay = overlay; TaskbarItemInfo.Description = "ProxyChoiceApp: " + description;
        _tray.Icon = healthy ? _greenIcon : _redIcon; _tray.Text = ("ProxyChoiceApp · " + description)[..Math.Min(63, ("ProxyChoiceApp · " + description).Length)];
    }
    private void SetBusy(bool busy)
    {
        _busy = busy; ConnectButton.IsEnabled = TestButton.IsEnabled = ApplyButton.IsEnabled = !busy;
        ProfileForm.IsEnabled = ProfileControls.IsEnabled = !busy && _session is null;
        SaveProfileButton.IsEnabled = !busy && _session is null;
        ProfileHint.Text = L.T(_session is null ? "ProfileHint" : "ConnectedProfileHint");
        ConnectButton.Content = L.T(busy ? "PleaseWait" : _session is null ? "Connect" : "Disconnect");
        TestButton.Content = L.T(_session is null ? "TestProxy" : "CheckIp");
        ApplyButton.Content = L.T(_session is null ? "Apply" : "ApplyReconnect");
        _trayDisconnect.Enabled = !busy && _session is not null;
    }
    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_session is null) await ConnectAsync();
        else { SetBusy(true); try { await DisconnectAsync(); } catch (Exception error) { ReportError(error); } finally { if (!_closing) SetBusy(false); } }
    }
    public async Task ConnectAsync()
    {
        if (_busy || _closing) return;
        SetBusy(true); using var operation = new CancellationTokenSource(); _operation = operation;
        try {
            var candidate = ReadSettings();
            if (_preview) throw new ArgumentException(L.T("PreviewNoConnect"));
            if (!Elevation.IsAdministrator) {
                var path = Path.Combine(SecureStorage.Root, "handoff", "connect-" + Guid.NewGuid().ToString("N") + ".dat");
                PersistSettings(candidate); SecureStorage.SaveProfiles(_profiles, path, includeSelectedPassword: true);
                if (Elevation.Restart(path)) { _allowClose = true; Close(); return; }
                SecureStorage.Delete(path); ShowNotice(L.T("ElevationCanceled"), false); return;
            }
            _settings = candidate; StatusTitle.Text = L.T("CheckingProxy"); StatusDescription.Text = L.T("PreflightHint");
            SetIndicator(false, L.T("Connecting")); AddLog(L.T("PreflightHint"));
            var ip = await ResolveServerAsync(candidate, operation.Token);
            await using (var probe = await CoreSession.StartAsync(candidate, ip, false, operation.Token))
                await probe.ReadProxyIpAsync(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            await StartTunnelAsync(candidate, ip, operation.Token); PersistSettings(candidate);
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception error) { if (_session is not null) await DisconnectAsync(); SetDisconnectedVisuals(); ReportError(error); }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; if (!_closing) SetBusy(false); }
    }
    private async Task StartTunnelAsync(ProxySettings settings, string ip, CancellationToken token)
    {
        _serverIp = ip; _session = await CoreSession.StartAsync(settings, ip, true, token);
        _connection = new CancellationTokenSource();
        var external = await _session.ReadProxyIpAsync(token);
        token.ThrowIfCancellationRequested();
        _connectedAt = _lastHealthAt = DateTime.UtcNow;
        _traffic.Reset(); _traffic.Sample(new(0, 0, 0), TimeSpan.Zero); _trafficClock.Restart(); UpdateTrafficText();
        ProxyIpText.Text = external; DurationText.Text = L.F("ConnectedDuration", "00:00:00");
        StatusTitle.Text = L.T("Connected"); StatusDescription.Text = L.T("ConnectedHint");
        Notice.Visibility = Visibility.Collapsed; SetIndicator(true, L.T("Connected"));
        AddLog(L.F("ConnectedLog", external)); AddLog(L.F("DirectDnsLog", _session.DirectDnsServer));
    }
    private async Task DisconnectAsync()
    {
        var session = _session; _session = null; _serverIp = null;
        _connection?.Cancel(); _connection?.Dispose(); _connection = null;
        if (session is not null) {
            AddLog(L.T("StoppingLog")); await session.DisposeAsync();
            if (session.ForcedStop) AddLog(L.T("ForcedStopLog"));
        }
        SetDisconnectedVisuals(); AddLog(L.T("DisconnectedLog"));
    }
    private void SetDisconnectedVisuals()
    {
        SetIndicator(false, L.T("Disconnected")); StatusTitle.Text = L.T("Disconnected"); StatusDescription.Text = L.T("DisconnectedHint");
        ProxyIpText.Text = "—"; _traffic.Stop(); _trafficClock.Stop(); UpdateTrafficText(); DurationText.Text = L.T("NoConnection");
    }
    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _closing) return;
        SetBusy(true); using var operation = new CancellationTokenSource(); _operation = operation;
        try {
            if (_preview) throw new ArgumentException(L.T("PreviewNoConnect"));
            if (_session is not null) {
                var ip = await _session.ReadProxyIpAsync(operation.Token); ProxyIpText.Text = ip;
                SetIndicator(true, L.T("Connected")); StatusTitle.Text = L.T("Connected"); StatusDescription.Text = L.T("ConnectedHint");
                ShowNotice(L.F("IpSuccess", ip), false); AddLog(L.F("IpSuccess", ip));
            } else {
                var settings = ReadSettings(); _settings = settings;
                var ip = await ResolveServerAsync(settings, operation.Token);
                await using var probe = await CoreSession.StartAsync(settings, ip, false, operation.Token);
                var external = await probe.ReadProxyIpAsync(operation.Token);
                ProxyIpText.Text = external; ShowNotice(L.F("TestSuccess", external), false); AddLog(L.F("IpSuccess", external));
                PersistSettings(settings);
            }
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception error) { if (_session is not null) MarkUnhealthy(); ReportError(error); }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; if (!_closing) SetBusy(false); }
    }
    private static async Task<string> ResolveServerAsync(ProxySettings settings, CancellationToken token)
    {
        if (IPAddress.TryParse(settings.Host, out var ip)) return ip.ToString();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(12));
        try {
            var addresses = await Dns.GetHostAddressesAsync(settings.Host, limit.Token);
            return (addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault())?.ToString()
                ?? throw new IOException(L.T("ResolveError"));
        }
        catch (SocketException) { throw new IOException(L.T("ResolveError")); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException(L.T("ResolveError")); }
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_session is null || _busy || _tickRunning || _closing) return;
        _tickRunning = true; var session = _session;
        try {
            if (session.HasExited) {
                AddLog(L.T("CoreStopped") + " " + session.ReadLogTail()); SetBusy(true);
                await DisconnectAsync(); ShowNotice(L.T("TunnelStoppedHint"), true); SetBusy(false); return;
            }
            var age = DateTime.UtcNow - _connectedAt; DurationText.Text = L.F("ConnectedDuration", $"{(int)age.TotalHours:00}:{age.Minutes:00}:{age.Seconds:00}");
            try {
                var traffic = await session.GetTrafficAsync(_connection!.Token);
                if (ReferenceEquals(session, _session)) { _traffic.Sample(traffic, _trafficClock.Elapsed); UpdateTrafficText(); }
            }
            catch (Exception error) when (error is System.Net.Http.HttpRequestException or TaskCanceledException or ObjectDisposedException)
            { if (ReferenceEquals(session, _session)) { _traffic.Stop(); UpdateTrafficText(); } }
            if (ReferenceEquals(session, _session) && DateTime.UtcNow - _lastHealthAt > TimeSpan.FromSeconds(30) && (_healthTask is null || _healthTask.IsCompleted)) {
                _lastHealthAt = DateTime.UtcNow; _healthTask = CheckHealthAsync(session, _connection!.Token);
            }
            if (MainTabs.SelectedIndex == 1) UpdateLogDisplay();
        }
        catch (Exception error) { if (!_closing) AddLog(error.Message); }
        finally { _tickRunning = false; }
    }
    private async Task CheckHealthAsync(CoreSession session, CancellationToken token)
    {
        try {
            var ip = await session.ReadProxyIpAsync(token);
            if (ReferenceEquals(session, _session) && !_closing) {
                ProxyIpText.Text = ip;
                if (!_healthy) { SetIndicator(true, L.T("Connected")); StatusTitle.Text = L.T("Connected"); StatusDescription.Text = L.T("ConnectedHint"); AddLog(L.T("RestoredLog")); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { if (ReferenceEquals(session, _session) && !_closing && _healthy) { MarkUnhealthy(); AddLog(L.T("HealthFailed")); } }
    }
    private void MarkUnhealthy()
    {
        SetIndicator(false, L.T("NoAccess")); StatusTitle.Text = L.T("NoAccess"); StatusDescription.Text = L.T("NoAccessHint");
    }
    private void UpdateTrafficText()
    {
        DownloadTotalText.Text = (_traffic.ReceivedBytes / 1048576d).ToString("N2") + " " + L.T("MB");
        UploadTotalText.Text = (_traffic.SentBytes / 1048576d).ToString("N2") + " " + L.T("MB");
        DownloadSpeedText.Text = FormatSpeed(_traffic.ReceiveBytesPerSecond); UploadSpeedText.Text = FormatSpeed(_traffic.SendBytesPerSecond);
    }
    private static string FormatSpeed(double bytes) => bytes >= 1048576
        ? (bytes / 1048576).ToString("N2") + " " + L.T("MBps") : (bytes / 1024).ToString("N1") + " " + L.T("KBps");

    private void Hook(AppChoice app) => app.PropertyChanged += App_Changed;
    private void Unhook(AppChoice app) => app.PropertyChanged -= App_Changed;
    private void App_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppChoice.UseProxy) || _syncing || sender is not AppChoice app) return;
        _syncing = true;
        try { foreach (var alias in Applications.Where(a => a != app && a.Path.Length > 0 && a.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase))) alias.UseProxy = app.UseProxy; }
        finally { _syncing = false; }
        UpdateSelectionSummary();
    }
    private void Rules_Changed(object sender, RoutedEventArgs e) { if (_initialised && !_loading) UpdateSelectionSummary(); }
    private void UpdateSelectionSummary()
    {
        if (SelectionSummary is null) return;
        var count = Applications.Where(a => a.UseProxy && a.Path.Length > 0).Select(a => a.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        SelectionSummary.Text = L.F("SelectionSummary", count) + " " + L.T("ApplyHint");
    }
    private void RunningApps_Click(object sender, RoutedEventArgs e)
    {
        var picker = new RunningAppsWindow { Owner = this };
        if (picker.ShowDialog() == true) foreach (var path in picker.SelectedPaths) AddApp(path, true);
    }
    private void BrowseApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = L.T("ExeFilter"), Multiselect = true, Title = L.T("ChooseExe") };
        if (dialog.ShowDialog(this) == true) foreach (var path in dialog.FileNames) AddApp(path, true);
    }
    private void AddApp(string path, bool selected)
    {
        try {
            path = InputRules.App(AppCatalog.NormalizeBrowserPath(path));
            var existing = Applications.FirstOrDefault(a => a.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { if (selected) existing.UseProxy = true; return; }
            var app = new AppChoice(new AppEntry(path).Name, path, File.Exists(path)) { UseProxy = selected };
            Hook(app); Applications.Add(app);
        }
        catch (ArgumentException error) { if (_initialised) ShowNotice(error.Message, true); }
        if (_initialised) UpdateSelectionSummary();
    }
    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (AppsList.SelectedItem is not AppChoice app) return;
        if (app.IsBuiltIn) app.UseProxy = false;
        else { Unhook(app); Applications.Remove(app); }
        UpdateSelectionSummary();
    }
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _closing) return;
        SetBusy(true); using var operation = new CancellationTokenSource(); _operation = operation;
        try {
            var settings = ReadSettings(false); settings.ProxyApps = settings.ProxyApps.Select(InputRules.App).ToList();
            if (_session is not null) {
                settings.Validate();
                var ip = _serverIp ?? throw new IOException(L.T("ResolveError"));
                await DisconnectAsync(); operation.Token.ThrowIfCancellationRequested();
                await StartTunnelAsync(settings, ip, operation.Token);
                ShowNotice(L.T("RulesApplied"), false);
            } else ShowNotice(L.T("RulesSaved"), false);
            PersistSettings(settings); AddLog(L.T("RulesSaved"));
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception error) { if (_session is not null) await DisconnectAsync(); ReportError(error); }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; if (!_closing) SetBusy(false); }
    }

    private void Protocol_Changed(object sender, SelectionChangedEventArgs e) { if (_initialised) UpdateProtocol(); }
    private void UpdateProtocol()
    {
        UdpBox.IsEnabled = ProtocolBox.SelectedIndex == 0;
        UdpBox.ToolTip = L.T(ProtocolBox.SelectedIndex == 0 ? "SocksHint" : ProtocolBox.SelectedIndex == 1 ? "HttpHint" : "HttpsHint");
    }
    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_initialised || LanguagePicker.SelectedItem is not LanguageOption language) return;
        L.Current.Set(language.Code); _profiles.Language = language.Code; RefreshLanguage();
        try { if (!_preview) SecureStorage.SaveProfiles(_profiles); }
        catch (Exception error) { ReportError(error); }
    }
    private void RefreshLanguage()
    {
        HelpSections.ItemsSource = L.Current.Help;
        foreach (var app in Applications) app.RefreshLanguage();
        _trayOpen.Text = L.T("OpenApp"); _trayDisconnect.Text = L.T("Disconnect"); _trayExit.Text = L.T("Exit");
        FooterMode.Text = L.T(_preview ? "PreviewFooter" : "Footer"); UpdateProtocol(); UpdateSelectionSummary(); UpdateTrafficText();
        if (_session is null) SetDisconnectedVisuals();
        else { SetIndicator(_healthy, L.T(_healthy ? "Connected" : "NoAccess")); StatusTitle.Text = L.T(_healthy ? "Connected" : "NoAccess"); StatusDescription.Text = L.T(_healthy ? "ConnectedHint" : "NoAccessHint"); }
        SetBusy(_busy);
    }
    private void Tabs_Changed(object sender, SelectionChangedEventArgs e)
    { if (_initialised && e.Source == MainTabs && MainTabs.SelectedIndex == 1) UpdateLogDisplay(); }
    public void ShowNotice(string text, bool error)
    {
        NoticeText.Text = Sanitize(text); Notice.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(error ? "#FFF1EE" : "#EAF6F1"));
        NoticeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(error ? "#A74537" : "#18785C")); Notice.Visibility = Visibility.Visible;
    }
    private void DismissNotice_Click(object sender, RoutedEventArgs e) => Notice.Visibility = Visibility.Collapsed;
    private void ReportError(Exception error)
    {
        var message = error.Message; if (message.Length > 1200) message = message[..1200];
        ShowNotice(message, true); AddLog(L.T("Error") + " " + message);
    }
    private string Sanitize(string text)
    {
        foreach (var secret in _profiles.Profiles.SelectMany(p => new[] { p.Settings.Password, p.Settings.Username })
            .Concat(new[] { _settings.Password, PasswordBox.Password, _settings.Username, UsernameBox.Text }).Where(s => !string.IsNullOrEmpty(s)).Distinct())
            text = text.Replace(secret, "••••", StringComparison.Ordinal);
        return text;
    }
    private void AddLog(string text)
    {
        _log.AppendLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + Sanitize(text));
        if (_log.Length > 60000) _log.Remove(0, _log.Length - 45000);
        UpdateLogDisplay();
    }
    private string CurrentLog() => Sanitize(_log + (_session is null ? "" : "\n" + _session.ReadLogTail()));
    private void UpdateLogDisplay() { LogBox.Text = CurrentLog(); LogBox.ScrollToEnd(); }
    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _log.Clear(); _session?.ClearVisibleLog(); UpdateLogDisplay(); ShowNotice(L.T("LogCleared"), false);
    }
    private void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = L.T("LogFilter"), FileName = "ProxyChoiceApp-log-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, CurrentLog(), Encoding.UTF8); ShowNotice(L.T("LogSaved"), false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { ReportError(error); }
    }
    private void Licenses_Click(object sender, RoutedEventArgs e)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProxyChoiceApp.notices.txt")!;
        using var reader = new StreamReader(stream);
        var text = new TextBox { Text = reader.ReadToEnd(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20), Background = Brushes.White, FontSize = 14 };
        new Window { Title = L.T("ComponentLicenses"), Owner = this, Width = 740, Height = 540, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = text }.ShowDialog();
    }
    private void Contact_Click(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Win32Exception error) { ReportError(error); }
        e.Handled = true;
    }
    private void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) { CleanupUi(); return; }
        e.Cancel = true; if (_closing) return;
        _closing = true; _timer.Stop(); _discovery.Cancel(); _operation?.Cancel(); _connection?.Cancel(); SetBusy(true);
        try {
            // All core operations have their own deadlines. Never wait indefinitely on a UI flag.
            var deadline = Stopwatch.StartNew();
            while ((_operation is not null || _tickRunning) && deadline.Elapsed < TimeSpan.FromSeconds(20)) await Task.Delay(50);
            await DisconnectAsync();
            if (_healthTask is not null) { try { await _healthTask.WaitAsync(TimeSpan.FromSeconds(3)); } catch (TimeoutException) { } }
            if (!_preview) {
                try { PersistSettings(ReadSettings(false)); }
                catch (ArgumentException) { PersistSettings(_settings); }
            }
        }
        catch (Exception error) { AddLog(error.Message); }
        finally { _allowClose = true; Close(); }
    }
    private void CleanupUi()
    { _timer.Stop(); _discovery.Cancel(); _tray.Visible = false; _tray.Dispose(); _redIcon.Dispose(); _greenIcon.Dispose(); }
}
