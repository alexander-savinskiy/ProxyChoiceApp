using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class SetupProgram
{
    internal static readonly string InstallFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProxyChoicePro");
    internal static readonly string[] PayloadNames = { "ProxyChoicePro.exe", "Uninstall.exe", "README.txt", "LICENSE.txt", "THIRD-PARTY-NOTICES.txt", "DOTNET-LICENSE.txt", "DOTNET-THIRD-PARTY-NOTICES.txt", "WINDOWS-DESKTOP-LICENSE.txt", "docs/Guide-ru.md", "docs/Guide-en.md", "docs/Guide-de.md", "docs/Guide-es.md", "docs/Guide-fr.md", "docs/Guide-zh.md", "docs/Keenetic-en.md", "docs/Keenetic-ru.md", "docs/Keenetic-fr.md", "docs/Keenetic-de.md", "docs/Keenetic-es.md", "docs/Keenetic-zh.md", "docs/work-client.example.conf" };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--verify")
        {
            try { Extract(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { File.WriteAllText(Path.Combine(Path.GetTempPath(), "ProxyChoicePro-setup-verify-error.txt"), error.ToString()); return 1; }
        }
        if (args.Length == 1 && args[0] == "--quiet")
        {
            try {
                if (IsAppRunning()) throw new IOException(Texts.T("SetupRunning"));
                Extract(InstallFolder); Register(true); return 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(Path.GetTempPath(), "ProxyChoicePro-setup-error.txt"), error.ToString()); return 1; }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupWindow());
        return 0;
    }

    internal static void Extract(string folder)
    {
        Directory.CreateDirectory(folder);
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new IOException(Texts.T("SymlinkError"));
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("payload.sha256"))
        using (var reader = new StreamReader(stream))
        {
            var hashes = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries))
                .ToDictionary(parts => parts[1], parts => parts[0], StringComparer.OrdinalIgnoreCase);
            foreach (var name in PayloadNames)
            {
                var path = Path.Combine(folder, name);
                var directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException(Texts.T("SymlinkError"));
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(Texts.T("SymlinkError"));
                using (var input = assembly.GetManifestResourceStream("payload." + name))
                using (var output = File.Create(path + ".installing")) input.CopyTo(output);
                using (var file = File.OpenRead(path + ".installing"))
                using (var hash = SHA256.Create())
                    if (!BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").Equals(hashes[name], StringComparison.OrdinalIgnoreCase))
                        throw new IOException(Texts.T("SetupHashError") + name);
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".installing", path);
            }
        }
    }

    internal static bool IsAppRunning()
    {
        var executable = Path.Combine(InstallFolder, "ProxyChoicePro.exe");
        foreach (var process in Process.GetProcessesByName("ProxyChoicePro"))
        using (process)
        {
            try { if (string.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase)) return true; }
            catch (System.ComponentModel.Win32Exception) { return true; }
        }
        return false;
    }

    internal static void Register(bool desktop)
    {
        var executable = Path.Combine(InstallFolder, "ProxyChoicePro.exe");
        var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ProxyChoicePro");
        Directory.CreateDirectory(programs);
        Shortcut(Path.Combine(programs, "ProxyChoicePro.lnk"), executable, "");
        Shortcut(Path.Combine(programs, "Uninstall ProxyChoicePro.lnk"), Path.Combine(InstallFolder, "Uninstall.exe"), "");
        if (desktop) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ProxyChoicePro.lnk"), executable, "");
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProxyChoicePro"))
        {
            key.SetValue("DisplayName", "ProxyChoicePro"); key.SetValue("DisplayVersion", "1.1.3"); key.SetValue("Publisher", "Alexander Savinskiy");
            key.SetValue("InstallLocation", InstallFolder); key.SetValue("DisplayIcon", executable);
            key.SetValue("UninstallString", "\"" + Path.Combine(InstallFolder, "Uninstall.exe") + "\"");
            key.SetValue("QuietUninstallString", "\"" + Path.Combine(InstallFolder, "Uninstall.exe") + "\" --quiet");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)(new FileInfo(executable).Length / 1024), RegistryValueKind.DWord);
        }
    }

    private static void Shortcut(string path, string target, string arguments)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        var shell = Activator.CreateInstance(shellType);
        object shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            var type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallFolder });
            type.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
}

internal sealed class SetupWindow : Form
{
    private readonly Button _install;
    private readonly CheckBox _desktop, _launch;
    private readonly Label _status, _tagline, _account;
    private readonly ProgressBar _progress;
    private readonly ComboBox _language;
    private bool _installing;

    internal SetupWindow()
    {
        ClientSize = new Size(660, 540); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 11);
        AutoScaleMode = AutoScaleMode.Dpi; BackColor = Color.FromArgb(246, 248, 251);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var header = new Panel { Dock = DockStyle.Top, Height = 126, BackColor = Color.FromArgb(20, 47, 64) };
        header.Controls.Add(new Label { Text = "ProxyChoicePro", Location = new Point(24, 22), Size = new Size(440, 44), Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Color.White });
        _tagline = new Label { Location = new Point(26, 80), Size = new Size(430, 30), ForeColor = Color.FromArgb(176, 203, 217) };
        header.Controls.Add(_tagline);
        _language = new ComboBox { Location = new Point(470, 76), Size = new Size(166, 30), DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 25 };
        _language.Items.AddRange(Texts.Names); _language.SelectedIndex = Array.IndexOf(Texts.Codes, Texts.Code);
        _language.DrawItem += DrawLanguage;
        header.Controls.Add(_language); Controls.Add(header);
        _account = new Label { Location = new Point(24, 148), Size = new Size(612, 34) }; Controls.Add(_account);
        Controls.Add(new Label { Text = SetupProgram.InstallFolder, Location = new Point(24, 188), Size = new Size(612, 50), ForeColor = Color.DimGray });
        _desktop = new CheckBox { Checked = true, Location = new Point(24, 253), Size = new Size(612, 32) };
        _launch = new CheckBox { Checked = true, Location = new Point(24, 292), Size = new Size(612, 32) };
        Controls.Add(_desktop); Controls.Add(_launch);
        _status = new Label { Location = new Point(24, 345), Size = new Size(612, 55), ForeColor = Color.DimGray };
        _progress = new ProgressBar { Location = new Point(24, 409), Size = new Size(612, 8), Visible = false, Style = ProgressBarStyle.Marquee };
        _install = new Button { Location = new Point(436, 439), Size = new Size(200, 44), BackColor = Color.FromArgb(8, 127, 112), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _install.FlatAppearance.BorderSize = 0; _install.Click += InstallClick;
        var contact = new LinkLabel { Text = "© 2026 Alexander Savinskiy · a.savinskiy@gmail.com", Location = new Point(24, 500), Size = new Size(612, 25), LinkColor = Color.FromArgb(8, 127, 112) };
        contact.LinkClicked += delegate { Process.Start(new ProcessStartInfo("mailto:a.savinskiy@gmail.com") { UseShellExecute = true }); };
        Controls.Add(_status); Controls.Add(_progress); Controls.Add(_install); Controls.Add(contact);
        _language.SelectedIndexChanged += delegate { Texts.Set(Texts.Codes[_language.SelectedIndex]); RefreshLanguage(); };
        RefreshLanguage();
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (_installing) e.Cancel = true; };
    }
    private void RefreshLanguage()
    {
        Text = Texts.T("SetupTitle"); _tagline.Text = Texts.T("SetupTagline"); _account.Text = Texts.T("SetupUser");
        _desktop.Text = Texts.T("SetupDesktop"); _launch.Text = Texts.T("SetupLaunch"); _status.Text = Texts.T("SetupReady"); _install.Text = Texts.T("Install");
    }
    private void DrawLanguage(object sender, DrawItemEventArgs e)
    {
        e.DrawBackground(); if (e.Index < 0) return;
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("flag." + Texts.Codes[e.Index] + ".png"))
        using (var bitmap = new Bitmap(stream))
            e.Graphics.DrawImage(bitmap, e.Bounds.X + 5, e.Bounds.Y + 5, 26, 17);
        TextRenderer.DrawText(e.Graphics, Texts.Names[e.Index], Font, new Point(e.Bounds.X + 38, e.Bounds.Y + 3), e.ForeColor);
        e.DrawFocusRectangle();
    }
    private async void InstallClick(object sender, EventArgs e)
    {
        if (SetupProgram.IsAppRunning()) { MessageBox.Show(this, Texts.T("SetupRunning"), "ProxyChoicePro"); return; }
        _installing = true; _install.Enabled = _desktop.Enabled = _launch.Enabled = _language.Enabled = false;
        _progress.Visible = true; _status.Text = Texts.T("SetupExtract");
        try
        {
            await Task.Run(() => SetupProgram.Extract(SetupProgram.InstallFolder)); SetupProgram.Register(_desktop.Checked);
            _status.Text = Texts.T("SetupDone");
            if (_launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(SetupProgram.InstallFolder, "ProxyChoicePro.exe")) { UseShellExecute = true });
            _installing = false; Close();
        }
        catch (Exception error)
        {
            _status.Text = Texts.T("SetupFailed"); MessageBox.Show(this, error.Message, Texts.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installing = false; _install.Enabled = _desktop.Enabled = _launch.Enabled = _language.Enabled = true; _progress.Visible = false;
        }
    }
}
