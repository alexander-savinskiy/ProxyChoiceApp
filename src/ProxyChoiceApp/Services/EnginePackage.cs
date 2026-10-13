using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Services;

public static class EnginePackage
{
    public const string Version = "1.14.3";

    public static async Task<string> EnsureAsync(CancellationToken token = default)
    {
        var folder = Path.Combine(SecureStorage.Root, "engine", Version);
        SecureStorage.EnsurePrivateDirectory(SecureStorage.Root);
        SecureStorage.EnsurePrivateDirectory(Path.Combine(SecureStorage.Root, "engine"));
        SecureStorage.EnsurePrivateDirectory(folder);
        using var manifestStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProxyChoiceApp.engine-manifest.json")!;
        var manifest = (await JsonSerializer.DeserializeAsync<EngineManifest>(manifestStream, cancellationToken: token))!;
        var exe = Path.Combine(folder, "sing-box.exe");
        if (await IsValidAsync(exe, manifest.ExeSha256, token) &&
            (manifest.WintunSha256 is null || await IsValidAsync(Path.Combine(folder, "wintun.dll"), manifest.WintunSha256, token))) return exe;

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProxyChoiceApp.engine.zip")
            ?? throw new IOException(L.T("MissingEngine"));
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (name is not ("sing-box.exe" or "wintun.dll" or "LICENSE" or "LICENSE.txt")) continue;
            var destination = Path.Combine(folder, name);
            if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(L.T("SymlinkError"));
            await using var input = entry.Open();
            await using var output = new FileStream(destination + ".new", FileMode.Create, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, token);
            await output.DisposeAsync();
            File.Move(destination + ".new", destination, true);
        }
        if (!await IsValidAsync(exe, manifest.ExeSha256, token) ||
            (manifest.WintunSha256 is not null && !await IsValidAsync(Path.Combine(folder, "wintun.dll"), manifest.WintunSha256, token)))
            throw new IOException(L.T("EngineHashError"));
        return exe;
    }

    private static async Task<bool> IsValidAsync(string path, string expected, CancellationToken token)
    {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
        await using var file = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(file, token)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EngineManifest
    {
        public string ExeSha256 { get; set; } = "";
        public string? WintunSha256 { get; set; }
    }
}
