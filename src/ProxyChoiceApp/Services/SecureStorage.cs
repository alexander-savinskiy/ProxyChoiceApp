using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using ProxyChoiceApp.Models;

using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Services;

public static class SecureStorage
{
    internal static string? RootOverride { get; set; }
    public static string Root => RootOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyChoicePro");
    public static string SettingsPath => Path.Combine(Root, "profile.dat");
    public static string ProfilesPath => Path.Combine(Root, "profiles.dat");
    private static readonly byte[] Entropy = "ProxyChoiceApp.settings.v1"u8.ToArray();

    public static void EnsurePrivateDirectory(string path)
    {
        var directory = Directory.CreateDirectory(path);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException(L.T("SymlinkError"));
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
    }

    public static void Save(ProxySettings settings, string? path = null, bool includePassword = false)
    {
        path ??= SettingsPath;
        var copy = settings.Copy();
        if (!copy.RememberPassword && !includePassword) copy.Password = "";
        WriteEncrypted(path, JsonSerializer.SerializeToUtf8Bytes(copy));
    }

    public static void SaveProfiles(ProxyProfiles profiles, string? path = null, bool includeSelectedPassword = false)
    {
        var copy = profiles.Copy(); copy.Normalize();
        foreach (var profile in copy.Profiles)
            if (!profile.Settings.RememberPassword && !(includeSelectedPassword && profile.Id == copy.SelectedProfileId))
                profile.Settings.Password = "";
        WriteEncrypted(path ?? ProfilesPath, JsonSerializer.SerializeToUtf8Bytes(copy));
    }

    public static ProxyProfiles LoadProfiles(string? path = null)
    {
        if (path is null && !File.Exists(ProfilesPath))
        {
            var priorApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyChoiceApp", "profiles.dat");
            if (RootOverride is null && File.Exists(priorApp)) {
                var migrated = LoadProfiles(priorApp); SaveProfiles(migrated); return migrated;
            }
            var oldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyFlow", "profiles.dat");
            if (RootOverride is null && File.Exists(oldPath))
            {
                var oldPlain = ProtectedData.Unprotect(File.ReadAllBytes(oldPath), "ProxyFlow.settings.v1"u8.ToArray(), DataProtectionScope.CurrentUser);
                try
                {
                    var migrated = JsonSerializer.Deserialize<ProxyProfiles>(oldPlain) ?? new();
                    migrated.ProxyApps.Clear(); migrated.ProxyStore = true; migrated.EnableUdp = false;
                    migrated.DefaultsInitialized = false; migrated.Normalize();
                    SaveProfiles(migrated); return migrated;
                }
                finally { CryptographicOperations.ZeroMemory(oldPlain); }
            }
            var profiles = ProxyProfiles.FromLegacy(Load());
            // Keep the original encrypted file intact during migration.
            if (File.Exists(SettingsPath)) SaveProfiles(profiles);
            return profiles;
        }
        path ??= ProfilesPath;
        if (!File.Exists(path)) return new();
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            var profiles = JsonSerializer.Deserialize<ProxyProfiles>(plain) ?? new();
            profiles.Normalize();
            return profiles;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static void WriteEncrypted(string path, byte[] plain)
    {
        string? temp = null;
        try
        {
            EnsurePrivateDirectory(Path.GetDirectoryName(path)!);
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            temp = path + "." + Guid.NewGuid().ToString("N") + ".new";
            File.WriteAllBytes(temp, encrypted);
            File.Move(temp, path, true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); if (temp is not null) Delete(temp); }
    }

    public static ProxySettings Load(string? path = null)
    {
        path ??= SettingsPath;
        if (!File.Exists(path)) return new();
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            var settings = JsonSerializer.Deserialize<ProxySettings>(plain) ?? new();
            settings.ProxyApps ??= new();
            settings.Host ??= ""; settings.Username ??= ""; settings.Password ??= "";
            return settings;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static void Delete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
