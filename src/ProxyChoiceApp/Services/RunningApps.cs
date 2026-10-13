using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProxyChoiceApp.Services;

public sealed record RunningAppsSnapshot(IReadOnlyList<AppEntry> Apps, int UnavailableCount, bool Incomplete);

public static class RunningApps
{
    // Reading the image name does not require access to the browser's memory or modules.
    public static string? ReadPath(int processId)
    {
        using var process = OpenProcess(0x1000, false, processId); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process.IsInvalid) return null;
        var path = new StringBuilder(32768);
        uint size = (uint)path.Capacity;
        return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : null;
    }

    public static RunningAppsSnapshot Discover(CancellationToken token = default)
    {
        var apps = new List<AppEntry>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unavailable = 0;
        var watch = Stopwatch.StartNew();
        var processes = Process.GetProcesses();
        var incomplete = false;
        try
        {
            foreach (var process in processes)
            {
                token.ThrowIfCancellationRequested();
                if (watch.Elapsed > TimeSpan.FromSeconds(5)) { incomplete = true; break; }
                var path = ReadPath(process.Id);
                if (path is null) { unavailable++; continue; }
                if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && Path.IsPathFullyQualified(path) &&
                    !path.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) && paths.Add(path))
                    apps.Add(new(path));
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        return new(apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray(), unavailable, incomplete);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref uint size);
}
