using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Services;

public static class Elevation
{
    public static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static bool Restart(string transferPath)
    {
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            start.Arguments = "--connect-profile " + NativeCore.Quote(transferPath) + " --parent " + Environment.ProcessId;
            Process.Start(start);
            return true;
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { return false; }
    }

    public static string ValidateTransferPath(string path)
    {
        path = Path.GetFullPath(path);
        var folder = Path.GetFullPath(Path.Combine(SecureStorage.Root, "handoff"));
        if (!string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(path).StartsWith("connect-", StringComparison.Ordinal) || Path.GetExtension(path) != ".dat")
            throw new IOException(L.T("TransferError"));
        return path;
    }
}
