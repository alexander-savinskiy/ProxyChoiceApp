using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class UninstallProgram
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProxyChoiceApp");
    private static readonly string[] Files = { "ProxyChoiceApp.exe", "README.txt", "LICENSE.txt", "THIRD-PARTY-NOTICES.txt", "DOTNET-LICENSE.txt", "DOTNET-THIRD-PARTY-NOTICES.txt", "WINDOWS-DESKTOP-LICENSE.txt" };
    private static readonly string[] Guides = { "Guide-ru.md", "Guide-en.md", "Guide-de.md", "Guide-es.md" };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--cleanup")
        {
            int parent;
            if (!int.TryParse(args[1], out parent)) return 1;
            try { using (var process = Process.GetProcessById(parent)) process.WaitForExit(20000); } catch (ArgumentException) { }
            try
            {
                if ((File.GetAttributes(Folder) & FileAttributes.ReparsePoint) != 0) return 1;
                File.Delete(Path.Combine(Folder, "Uninstall.exe"));
                if (Directory.GetFileSystemEntries(Folder).Length == 0) Directory.Delete(Folder);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return 0;
        }
        if (!string.Equals(Path.GetDirectoryName(Application.ExecutablePath), Folder, StringComparison.OrdinalIgnoreCase)) return 1;
        if ((File.GetAttributes(Folder) & FileAttributes.ReparsePoint) != 0) return 1;
        var quiet = args.Length == 1 && args[0] == "--quiet";
        Application.EnableVisualStyles();
        if (!quiet && MessageBox.Show(Texts.T("UninstallQuestion"), Texts.T("UninstallTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
        try
        {
            foreach (var process in Process.GetProcessesByName("ProxyChoiceApp"))
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule.FileName, Path.Combine(Folder, "ProxyChoiceApp.exe"), StringComparison.OrdinalIgnoreCase))
                    {
                        if (!quiet) MessageBox.Show(Texts.T("SetupRunning"), "ProxyChoiceApp");
                        return 1;
                    }
                }
                catch (System.ComponentModel.Win32Exception) { return 1; }
            }
            foreach (var name in Files) if (File.Exists(Path.Combine(Folder, name))) File.Delete(Path.Combine(Folder, name));
            var docs = Path.Combine(Folder, "docs");
            if (Directory.Exists(docs) && (File.GetAttributes(docs) & FileAttributes.ReparsePoint) == 0) {
                foreach (var name in Guides) if (File.Exists(Path.Combine(docs, name))) File.Delete(Path.Combine(docs, name));
                if (Directory.GetFileSystemEntries(docs).Length == 0) Directory.Delete(docs);
            }
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ProxyChoiceApp.lnk"));
            var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ProxyChoiceApp");
            DeleteShortcut(Path.Combine(programs, "ProxyChoiceApp.lnk"));
            DeleteShortcut(Path.Combine(programs, "Удалить ProxyChoiceApp.lnk"));
            DeleteShortcut(Path.Combine(programs, "Uninstall ProxyChoiceApp.lnk"));
            if (Directory.Exists(programs) && Directory.GetFileSystemEntries(programs).Length == 0) Directory.Delete(programs);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProxyChoiceApp", false);
            var helper = Path.Combine(Path.GetTempPath(), "ProxyChoiceApp-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, helper);
            Process.Start(new ProcessStartInfo(helper, "--cleanup " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            if (!quiet) MessageBox.Show(Texts.T("UninstallDone"), "ProxyChoiceApp", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception error) { if (!quiet) MessageBox.Show(error.Message, Texts.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }

    private static void DeleteShortcut(string path)
    {
        if (!File.Exists(path)) return;
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        var shell = Activator.CreateInstance(shellType); object shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            var target = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
            if (string.Equals(Path.GetDirectoryName(target), Folder, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
        }
        finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
}
