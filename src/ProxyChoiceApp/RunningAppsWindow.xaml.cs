using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ProxyChoiceApp.Services;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp;

public sealed record AppEntry(string Path)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path).ToLowerInvariant() switch {
        "chrome" => "Google Chrome",
        "msedge" => "Microsoft Edge",
        _ => System.IO.Path.GetFileNameWithoutExtension(Path)
    };
}

public partial class RunningAppsWindow : Window
{
    private readonly List<AppEntry> _apps = new();
    private readonly CancellationTokenSource _refreshCancellation = new();
    private bool _refreshing, _closed;
    public List<string> SelectedPaths { get; private set; } = new();

    public RunningAppsWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        if (_refreshing || _closed) return;
        _refreshing = true; RefreshButton.IsEnabled = false; StatusText.Text = L.T("LoadingApps");
        try
        {
            var token = _refreshCancellation.Token;
            var snapshot = await Task.Run(() => RunningApps.Discover(token), token);
            if (_closed) return;
            _apps.Clear(); _apps.AddRange(snapshot.Apps); ApplySearch();
            StatusText.Text = L.F("AppsFound", _apps.Count) + (snapshot.Incomplete ? " " + L.T("AppsIncomplete") : "");
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { if (!_closed) StatusText.Text = L.T("AppsLoadError"); }
        finally { _refreshing = false; if (!_closed) RefreshButton.IsEnabled = true; }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    { _closed = true; _refreshCancellation.Cancel(); _refreshCancellation.Dispose(); }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (ProcessList is null) return;
        ApplySearch();
    }
    private void ApplySearch()
    {
        ProcessList.ItemsSource = _apps.Where(a => a.Name.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase) ||
            a.Path.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    { if (AddSelectedButton is not null) AddSelectedButton.IsEnabled = ProcessList.SelectedItems.Count > 0; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        SelectedPaths = ProcessList.SelectedItems.Cast<AppEntry>().Select(a => a.Path).ToList();
        if (SelectedPaths.Count > 0) DialogResult = true;
    }
}
