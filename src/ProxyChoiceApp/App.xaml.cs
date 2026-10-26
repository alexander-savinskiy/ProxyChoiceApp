using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var captureIndex = Array.IndexOf(e.Args, "--capture-preview");
            var preview = e.Args.Contains("--preview") || captureIndex >= 0;
            L.Current.Set("ru");
            if (preview) SecureStorage.RootOverride = Path.Combine(AppContext.BaseDirectory, "preview-data");
            var parentIndex = Array.IndexOf(e.Args, "--parent");
            if (parentIndex >= 0 && parentIndex + 1 < e.Args.Length && int.TryParse(e.Args[parentIndex + 1], out var pid))
            {
                try { using var parent = Process.GetProcessById(pid); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(12)); await parent.WaitForExitAsync(limit.Token); }
                catch (ArgumentException) { }
            }
            _mutex = new Mutex(false, "Local\\ProxyChoicePro-" + WindowsIdentity.GetCurrent().User!.Value + (preview ? "-preview" : ""));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            do
            {
                try { _ownsMutex = _mutex.WaitOne(0); } catch (AbandonedMutexException) { _ownsMutex = true; }
                if (_ownsMutex) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < deadline);
            if (!_ownsMutex)
            {
                MessageBox.Show(L.T("AlreadyRunning"), "ProxyChoicePro", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(); return;
            }
            ProxyProfiles profiles;
            string? loadError = null;
            var transferIndex = Array.IndexOf(e.Args, "--connect-profile");
            var autoConnect = transferIndex >= 0 && transferIndex + 1 < e.Args.Length;
            if (autoConnect)
            {
                var path = Elevation.ValidateTransferPath(e.Args[transferIndex + 1]);
                try { profiles = SecureStorage.LoadProfiles(path); }
                finally { SecureStorage.Delete(path); }
            }
            else
            {
                try { profiles = preview ? new ProxyProfiles() : SecureStorage.LoadProfiles(); }
                catch (Exception error) when (error is IOException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
                { profiles = new(); loadError = L.T("LoadError"); }
            }
            if (!preview) CleanStaleSessions();
            var window = new MainWindow(profiles, preview);
            MainWindow = window;
            window.Show();
            if (captureIndex >= 0 && captureIndex + 1 < e.Args.Length) {
                await window.CaptureProPreviewsAsync(Path.GetFullPath(e.Args[captureIndex + 1]));
                window.Close(); return;
            }
            if (loadError is not null) window.ShowNotice(loadError, true);
            if (autoConnect) await window.ConnectAsync();
        }
        catch (Exception error)
        {
            MessageBox.Show(L.T("StartupError") + " " + error.Message, "ProxyChoicePro", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void CleanStaleSessions()
    {
        // Only after obtaining the single-instance mutex. Remove credentials from sessions left by a crash.
        foreach (var name in new[] { "sessions", "handoff" })
        {
            var folder = Path.Combine(SecureStorage.Root, name);
            if (!Directory.Exists(folder) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions {
                RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }))
                if (Path.GetExtension(file) is ".json" or ".dat" or ".log") SecureStorage.Delete(file);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
