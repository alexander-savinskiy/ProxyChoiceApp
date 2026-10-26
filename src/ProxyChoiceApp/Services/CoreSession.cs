using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Models;

using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Services;

public sealed class CoreSession : IAsyncDisposable
{
    private NativeCore? _core;
    private readonly string _folder;
    private readonly string _config;
    private readonly string _startupLog;
    private readonly CoreOptions _options;
    private readonly HttpClient _api;
    private readonly object _lifecycle = new();
    private Task? _disposeTask;
    public HttpClient ProxyClient { get; }
    public bool HasExited => _core is null || _core.HasExited;
    public int? ProcessId => _core?.ProcessId;
    public string LogPath => _options.LogPath;
    public string DirectDnsServer => _options.DirectDnsServer ?? "Cloudflare DoH";
    public bool ForcedStop => _core?.ForcedStop == true;
    public string? ShutdownDiagnostic => _core?.ShutdownDiagnostic;
    public int? ExitCode => _core?.ExitCode;
    public bool UsesIpv4Only => _options.Tunnel && !_options.TunIpv6;
    private long _visibleLogStart;

    private CoreSession(ProxySettings settings, string serverIp, bool tunnel, bool tunIpv6)
    {
        _folder = Path.Combine(SecureStorage.Root, "sessions", Guid.NewGuid().ToString("N"));
        SecureStorage.EnsurePrivateDirectory(Path.Combine(SecureStorage.Root, "sessions"));
        SecureStorage.EnsurePrivateDirectory(_folder);
        _config = Path.Combine(_folder, "core.json");
        _startupLog = Path.Combine(_folder, "startup.log");
        var probePort = FreePort();
        var apiPort = FreePort();
        while (apiPort == probePort) apiPort = FreePort();
        _options = new(serverIp, probePort, "proxychoice", Secret(), apiPort, Secret(), Path.Combine(_folder, "core.log"), tunnel, DirectDns.FindServer(), TunIpv6: tunIpv6);
        File.WriteAllText(_config, ConfigBuilder.Build(settings, _options), new UTF8Encoding(false));
        ProxyClient = new HttpClient(new HttpClientHandler {
            UseProxy = true, Proxy = new WebProxy($"socks5://127.0.0.1:{probePort}") {
                Credentials = new NetworkCredential(_options.ProbeUser, _options.ProbePassword), BypassProxyOnLocal = false
            }, AllowAutoRedirect = false
        }) { Timeout = TimeSpan.FromSeconds(18) };
        _api = new HttpClient(new HttpClientHandler { UseProxy = false }) {
            BaseAddress = new Uri($"http://127.0.0.1:{apiPort}"), Timeout = TimeSpan.FromSeconds(2)
        };
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiSecret);
    }

    public static async Task<CoreSession> StartAsync(ProxySettings settings, string serverIp, bool tunnel,
        CancellationToken token = default, Action<string>? diagnostic = null)
    {
        var exe = await EnginePackage.EnsureAsync(token);
        return await TunCompatibility.StartAsync(settings, tunnel,
            (ipv6, cancellation) => StartOnceAsync(exe, settings, serverIp, tunnel, ipv6, cancellation), diagnostic, token);
    }

    private static async Task<CoreSession> StartOnceAsync(string exe, ProxySettings settings, string serverIp,
        bool tunnel, bool tunIpv6, CancellationToken token)
    {
        var session = new CoreSession(settings, serverIp, tunnel, tunIpv6);
        try
        {
            await CheckConfigAsync(exe, session._config, token);
            session._core = new NativeCore(exe, session._config, session._startupLog);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(15))
            {
                token.ThrowIfCancellationRequested();
                if (session.HasExited) throw new CoreStartupException(L.T("EngineStartError") + " " + session.ReadStartupFailure(), tunnel);
                try
                {
                    using var response = await session._api.GetAsync("/version", token);
                    if (response.IsSuccessStatusCode)
                    {
                        // sing-box has read its config. Do not retain a plaintext password on disk.
                        SecureStorage.Delete(session._config);
                        return session;
                    }
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!token.IsCancellationRequested) { }
                await Task.Delay(150, token);
            }
            throw new CoreStartupException(L.T("EngineTimeout") + " " + session.ReadStartupFailure(), tunnel);
        }
        catch { await session.DisposeAsync(); throw; }
    }

    public static async Task CheckConfigAsync(string executable, string path, CancellationToken token = default, TimeSpan? timeout = null)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
        } };
        process.StartInfo.ArgumentList.Add("check"); process.StartInfo.ArgumentList.Add("-c"); process.StartInfo.ArgumentList.Add(path);
        process.Start();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        try
        {
            using var job = NativeCore.Guard(process);
            await process.WaitForExitAsync(limit.Token);
            await Task.WhenAll(errors, output).WaitAsync(limit.Token);
            var diagnostic = errors.Result + output.Result;
            if (process.ExitCode != 0) throw new IOException(L.T("ConfigError") + " " + diagnostic);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { }
            try { await Task.WhenAll(errors, output).WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { }
            if (limit.IsCancellationRequested && !token.IsCancellationRequested)
                throw new IOException(L.T("ConfigTimeout"));
            throw;
        }
    }

    public async Task<string> ReadProxyIpAsync(CancellationToken token = default)
    {
        // The authenticated probe inbound is explicitly forced to the proxy in the routing rules.
        Exception? last = null;
        foreach (var endpoint in new[] { "https://api.ipify.org", "https://checkip.amazonaws.com" })
        {
            try
            {
                var value = (await ProxyClient.GetStringAsync(endpoint, token)).Trim();
                if (IPAddress.TryParse(value, out var ip)) return ip.ToString();
                throw new IOException(L.T("IpReplyError"));
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException)
            { token.ThrowIfCancellationRequested(); last = error; }
        }
        throw new IOException(L.T("ProxyAccessError"), last);
    }

    public Task ProbeTcpAsync(string host, int port, CancellationToken token = default, TimeSpan? timeout = null) =>
        NetworkDiagnostics.ProbeSocksAsync(_options.ProbePort, _options.ProbeUser, _options.ProbePassword, host, port, token, timeout);

    public async Task<TrafficSnapshot> GetTrafficAsync(CancellationToken token = default)
    {
        using var response = await _api.GetAsync("/connections", token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = json.RootElement;
        return new(root.GetProperty("uploadTotal").GetInt64(), root.GetProperty("downloadTotal").GetInt64(),
            root.GetProperty("connections").GetArrayLength());
    }

    public string ReadLogTail()
    {
        try
        {
            using var file = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            file.Seek(Math.Min(file.Length, Math.Max(_visibleLogStart, file.Length - 12000)), SeekOrigin.Begin);
            using var reader = new StreamReader(file);
            return reader.ReadToEnd();
        }
        catch (IOException) { return ""; }
    }

    internal string ReadStartupFailure()
    {
        var details = "";
        if (_core?.ExitCode is int code) details = L.F("EngineExitCode", unchecked((uint)code).ToString("X8")) + "\n";
        try {
            using var file = new FileStream(_startupLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(file);
            var buffer = new char[6000]; var count = reader.ReadBlock(buffer, 0, buffer.Length);
            details += new string(buffer, 0, count).Trim();
        } catch (IOException) { }
        var log = ReadLogTail().Trim();
        if (log.Length > 0) details += "\n" + log;
        if (string.IsNullOrWhiteSpace(details)) details = L.T("EngineNoDetails");
        return Regex.Replace(details, @"\x1B\[[0-?]*[ -/]*[@-~]", "").Trim();
    }

    // Keep the engine's file handle intact; cleared entries never reappear in the UI or export.
    public void ClearVisibleLog()
    {
        try {
            using var file = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            _visibleLogStart = file.Length;
        }
        catch (IOException) { _visibleLogStart = 0; }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycle) return new(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        // Abort requests before waiting for the core, including requests without an explicit token.
        ProxyClient.CancelPendingRequests(); _api.CancelPendingRequests();
        try { if (_core is not null) await _core.DisposeAsync(); }
        finally
        {
            ProxyClient.Dispose(); _api.Dispose();
            SecureStorage.Delete(_config);
            SecureStorage.Delete(LogPath);
            SecureStorage.Delete(_startupLog);
            try { Directory.Delete(_folder); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    private static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
}

public sealed class CoreStartupException : IOException
{
    public bool Tunnel { get; }
    public CoreStartupException(string message, bool tunnel) : base(message) => Tunnel = tunnel;
}

public sealed record TrafficSnapshot(long Uploaded, long Downloaded, int Connections);
