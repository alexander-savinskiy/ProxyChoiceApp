using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using ProxyChoiceApp.Models;

namespace ProxyChoiceApp.Services;

public static class AppCatalog
{
    public static IReadOnlyList<AppChoice> Discover(CancellationToken token = default)
    {
        var running = RunningApps.Discover(token).Apps;
        var apps = DiscoverKnown(running, FindPaths).ToList();
        foreach (var path in FindPaths("YouTube.exe").Concat(running.Where(a => System.IO.Path.GetFileName(a.Path).Equals("YouTube.exe", StringComparison.OrdinalIgnoreCase)).Select(a => a.Path))
            .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            apps.Add(new("YouTube", path, builtIn: true, youtube: true));
        var deadline = Stopwatch.StartNew();
        var youtubePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.DesktopDirectory })
        {
            token.ThrowIfCancellationRequested();
            var directory = Environment.GetFolderPath(folder);
            if (!Directory.Exists(directory)) continue;
            try
            {
                var files = Directory.EnumerateFiles(directory, "*.lnk", new EnumerationOptions {
                    RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 4 });
                var count = 0;
                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    if (++count > 500 || deadline.Elapsed > TimeSpan.FromSeconds(3)) break;
                    if (!System.IO.Path.GetFileNameWithoutExtension(file).Contains("YouTube", StringComparison.OrdinalIgnoreCase)) continue;
                    var path = ResolveShortcut(file);
                    if (path is null || !File.Exists(path) || !youtubePaths.Add(path) || apps.Any(a => a.IsYouTube && a.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                    var browser = System.IO.Path.GetFileName(path) is "chrome.exe" or "msedge.exe";
                    apps.Add(new("YouTube", path, builtIn: true, youtube: true, browserYouTube: browser));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return apps;
    }

    internal static IReadOnlyList<AppChoice> DiscoverKnown(IEnumerable<AppEntry> running, Func<string, IEnumerable<string>> lookup)
    {
        var processes = running.ToArray(); var apps = new List<AppChoice>();
        string[] Paths(string exe) => lookup(exe).Concat(processes.Where(a => Path.GetFileName(a.Path).Equals(exe, StringComparison.OrdinalIgnoreCase)).Select(a => a.Path))
            .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var chats = Paths("ChatGPT.exe"); var codex = Paths("codex.exe").Concat(Paths("CodexDesktop.exe")).ToArray();
        var codexRoutes = codex.Concat(Paths("codex-code-mode-host.exe")).Concat(chats.Where(p => p.Contains("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var (exe, name, required) in new[] {
            ("chrome.exe", "Google Chrome", true), ("msedge.exe", "Microsoft Edge", true),
            ("ChatGPT.exe", "ChatGPT", true), ("codex.exe", "Codex", true),
            ("codex-computer-use-swift.exe", "codex-computer-use-swift", true),
            ("firefox.exe", "Mozilla Firefox", false), ("opera.exe", "Opera", false),
            ("brave.exe", "Brave", false), ("browser.exe", "Yandex Browser", false) }) {
            var paths = exe == "codex.exe" ? codexRoutes : Paths(exe);
            if (exe == "codex-computer-use-swift.exe") paths = paths.Concat(Paths("codex-compuer-use-swift.exe")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length == 0) { if (required) apps.Add(new(name, "", false, true)); }
            else if (exe == "codex.exe") apps.Add(new(name, codex.FirstOrDefault() ?? paths[0], builtIn: true, routePaths: paths));
            else foreach (var path in paths) apps.Add(new(name, path, builtIn: true));
        }
        return apps;
    }

    private static IEnumerable<string> FindPaths(string exe)
    {
        var result = new List<string>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
                if (key?.GetValue(null) is string path) result.Add(path.Trim('"'));
            }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
        {
            var prefix = Environment.GetFolderPath(folder);
            if (exe == "chrome.exe") result.Add(System.IO.Path.Combine(prefix, "Google", "Chrome", "Application", exe));
            if (exe == "msedge.exe") result.Add(System.IO.Path.Combine(prefix, "Microsoft", "Edge", "Application", exe));
            if (exe == "firefox.exe") result.Add(Path.Combine(prefix, "Mozilla Firefox", exe));
            if (exe == "brave.exe") result.Add(Path.Combine(prefix, "BraveSoftware", "Brave-Browser", "Application", exe));
            if (exe == "browser.exe") result.Add(Path.Combine(prefix, "Yandex", "YandexBrowser", "Application", exe));
            if (exe == "opera.exe") result.Add(Path.Combine(prefix, "Programs", "Opera", exe));
            if (exe == "ChatGPT.exe") result.Add(Path.Combine(prefix, "Programs", "ChatGPT", exe));
        }
        if (exe.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase) || exe.Equals("CodexDesktop.exe", StringComparison.OrdinalIgnoreCase)) {
            try {
                using var packages = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
                if (packages is not null) foreach (var name in packages.GetSubKeyNames().Where(n => n.StartsWith("OpenAI.", StringComparison.OrdinalIgnoreCase))) {
                    using var package = packages.OpenSubKey(name);
                    if (package?.GetValue("PackageRootFolder") is string folder) {
                        result.Add(Path.Combine(folder, "app", exe)); result.Add(Path.Combine(folder, exe));
                    }
                }
            } catch (UnauthorizedAccessException) { } catch (System.Security.SecurityException) { }
        }
        if (exe.StartsWith("codex", StringComparison.OrdinalIgnoreCase)) {
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex");
            var binaries = Path.Combine(cache, "bin");
            try {
                if (Directory.Exists(binaries)) foreach (var version in Directory.EnumerateDirectories(binaries)) result.Add(Path.Combine(version, exe));
                var runtimes = Path.Combine(cache, "runtimes", "cua_node");
                if (Directory.Exists(runtimes)) foreach (var version in Directory.EnumerateDirectories(runtimes)) {
                    result.Add(Path.Combine(version, "bin", exe));
                    result.Add(Path.Combine(version, "bin", "node_modules", "@oai", "sky", "bin", "windows", "swift", "x64", exe));
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return result;
    }

    internal static string NormalizeBrowserPath(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        if (name.Equals("chrome_proxy.exe", StringComparison.OrdinalIgnoreCase)) return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, "chrome.exe");
        if (name.Equals("msedge_proxy.exe", StringComparison.OrdinalIgnoreCase)) return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, "msedge.exe");
        return path;
    }
    private static string? ResolveShortcut(string file)
    {
        object? shell = null, link = null;
        try {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return null;
            shell = Activator.CreateInstance(type);
            link = ((dynamic)shell!).CreateShortcut(file);
            var path = (string)((dynamic)link).TargetPath;
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !System.IO.Path.IsPathFullyQualified(path)) return null;
            return NormalizeBrowserPath(path);
        }
        catch (COMException) { return null; }
        finally { if (link is not null) Marshal.FinalReleaseComObject(link); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }
}
