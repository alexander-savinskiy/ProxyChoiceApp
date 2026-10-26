using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Tests;

internal static partial class Program
{
    private static int _checks;
    private static string _root = "";
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); _checks++; Console.WriteLine("PASS: " + message); }
    private static void Throws(Action action, string message)
    { try { action(); } catch (ArgumentException) { Assert(true, message); return; } throw new Exception("FAIL: " + message); }

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--probe-work") return await ProbePersonalWork(args[1], args[2]);
        if (args.Length > 0 && args[0] == "--list-apps") {
            Console.WriteLine(JsonSerializer.Serialize(AppCatalog.Discover().Select(a => new { a.Name, a.Path, a.Available, a.RoutePaths }))); return 0;
        }
        if (args.Length > 0 && args[0] == "--list-browsers")
        {
            var snapshot = RunningApps.Discover();
            Console.WriteLine(JsonSerializer.Serialize(new { snapshot.Incomplete, snapshot.UnavailableCount,
                Browsers = snapshot.Apps.Where(a => Path.GetFileName(a.Path).ToLowerInvariant() is "chrome.exe" or "msedge.exe") }));
            return 0;
        }
        // Isolated child fixtures exercise timeouts and crash cleanup without changing Windows routes.
        if (args.Length > 0 && args[0] == "--crash-core")
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.WriteLine("early stdout diagnostic");
            Console.Error.WriteLine("early stderr diagnostic: ошибка до открытия журнала");
            return 23;
        }
        if (args.Length > 0 && args[0] == "--cooperative-core")
        {
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopped.TrySetResult(); };
            Console.WriteLine("cooperative core ready");
            await stopped.Task;
            Console.Error.WriteLine("cooperative core stopped");
            return 0;
        }
        if (args.Length > 0 && args[0] == "--stall-core")
        {
            Console.CancelKeyPress += (_, e) => e.Cancel = true;
            if (args.Length > 1) PublishFixtureReady(args[1], Environment.ProcessId);
            await Task.Delay(Timeout.Infinite); return 0;
        }
        if (args.Length > 1 && args[0] == "--job-parent")
        {
            await using var child = new NativeCore(Environment.ProcessPath!, new[] { "--stall-core", args[1] + ".child" });
            PublishFixtureReady(args[1], child.ProcessId);
            await Task.Delay(Timeout.Infinite); return 0;
        }
        if (args.Length == 3 && args[0] == "check")
        {
            PublishFixtureReady(args[2] + ".pid", Environment.ProcessId);
            await Task.Delay(Timeout.Infinite); return 0;
        }
        _root = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "test-artifacts"));
        Directory.CreateDirectory(_root);
        SecureStorage.RootOverride = Path.Combine(_root, "data");
        try
        {
            ValidateInput();
            TestLanguages();
            TestStorage();
            TestProfiles();
            TestTraffic();
            TestWorkProfiles();
            TestPublicPrivacyDefaults();
            TestKnownApplications();
            Assert(string.Equals(RunningApps.ReadPath(Environment.ProcessId), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase),
                "native Windows image-name lookup returns the real process path without reading modules");
            var exe = await EnginePackage.EnsureAsync();
            Assert(File.Exists(exe), "bundled engine extraction and SHA-256 verification");
            await CheckConfigurations(exe);
            await CheckWorkConfigurations(exe);
            await CheckIpv4Compatibility(exe);
            await CheckWorkTransportRoutes(exe);
            await CheckWorkProbeDeadlines();
            await CheckConnectionRecovery();
            await RealWireGuardTraffic(exe);
            foreach (var protocol in new[] { ProxyProtocol.Socks5, ProxyProtocol.Http })
            {
                await AuthenticatedProxy(protocol);
                await RejectedCredentials(protocol);
            }
            await AnonymousProxy();
            await RejectUntrustedTls();
            await ApplicationRouting(exe, false, false);
            await ApplicationRouting(exe, true, false);
            await ApplicationRouting(exe, false, true);
            await ApplicationRouting(exe, true, false, keepLan: true);
            await StoreRouting(exe, false);
            await StoreRouting(exe, true);
            await CheckProcessLifecycle();
            await CheckStartupCapture(exe);
            await RepeatedSessions();
            await CancelledSession();
            Assert(!Directory.EnumerateFiles(SecureStorage.Root, "core.json", System.IO.SearchOption.AllDirectories).Any(), "temporary plaintext configs removed after all sessions");
            Console.WriteLine($"ALL {_checks} CHECKS PASSED");
            File.WriteAllText(Path.Combine(_root, "result.json"), JsonSerializer.Serialize(new { passed = _checks, status = "passed" }));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(_root, "failure.txt"), error.ToString()); return 1; }
    }

    private static void PublishFixtureReady(string path, int pid)
    {
        // Publish the signal only after closing its writer; File.Exists alone cannot prove readiness.
        var pending = path + ".pending";
        File.WriteAllText(pending, pid.ToString());
        File.Move(pending, path, true);
    }

    private static void TestProfiles()
    {
        var previousRoot = SecureStorage.RootOverride;
        SecureStorage.RootOverride = Path.Combine(_root, "profile-tests");
        try
        {
            var legacy = new ProxySettings { Host = "old.proxy.example", Port = 8080, Username = "legacy-user", Password = "legacy-secret",
                ProxyApps = new() { @"C:\Apps\Client.exe" }, ProxyStore = true, Language = "de", DefaultsInitialized = true };
            SecureStorage.Save(legacy);
            var profiles = SecureStorage.LoadProfiles();
            var firstId = profiles.SelectedProfileId;
            Assert(profiles.Profiles.Count == 1 && profiles.SelectedSettings().Password == legacy.Password, "old profile migrates with credentials intact");
            Assert(profiles.SelectedSettings().ProxyApps.SequenceEqual(legacy.ProxyApps) && profiles.ProxyStore && profiles.Language == "de", "migration preserves shared app choices and language");
            Assert(File.Exists(SecureStorage.ProfilesPath) && File.Exists(SecureStorage.SettingsPath), "migration writes new encrypted store and retains original");
            profiles.Rename("Основной"); profiles.Add("Резервный");
            var secondId = profiles.SelectedProfileId;
            var second = profiles.SelectedSettings(); second.Host = "second.proxy.example"; second.Port = 1081; second.Username = "second-user"; second.Password = "second-secret";
            profiles.UpdateSelected(second); SecureStorage.SaveProfiles(profiles);
            profiles = SecureStorage.LoadProfiles();
            Assert(profiles.Profiles.Count == 2 && profiles.SelectedProfileId == secondId, "multiple profiles and selected profile survive restart");
            Assert(profiles.Profiles.First(p => p.Id == firstId).Settings.Password == "legacy-secret" && profiles.Selected.Settings.Password == "second-secret", "profile credentials remain separate");
            Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(SecureStorage.ProfilesPath)).Contains("second-secret"), "profile collection is encrypted with Windows DPAPI");
            profiles.SelectedProfileId = firstId;
            var selected = profiles.SelectedSettings(); selected.ProxyApps.Add(@"C:\Apps\Shared.exe"); profiles.UpdateSelected(selected);
            profiles.SelectedProfileId = secondId;
            Assert(profiles.SelectedSettings().ProxyApps.Contains(@"C:\Apps\Shared.exe"), "application choices apply to every profile");
            var detached = profiles.SelectedSettings(); detached.Password = "changed"; detached.ProxyApps.Clear();
            Assert(profiles.Selected.Settings.Password == "second-secret" && profiles.ProxyApps.Contains(@"C:\Apps\Shared.exe"), "editing a detached draft cannot corrupt saved profiles");
            profiles.Profiles[0].Settings.RememberPassword = false;
            profiles.Selected.Settings.RememberPassword = false;
            SecureStorage.SaveProfiles(profiles);
            Assert(SecureStorage.LoadProfiles().Profiles.All(p => p.Settings.Password == ""), "remember-password choice is respected for each profile");
            var transfer = Path.Combine(SecureStorage.Root, "handoff", "connect-profiles.dat");
            SecureStorage.SaveProfiles(profiles, transfer, includeSelectedPassword: true);
            var handoff = SecureStorage.LoadProfiles(transfer);
            Assert(handoff.SelectedProfileId == secondId && handoff.Selected.Settings.Password == "second-secret", "elevation handoff keeps selected profile and its unsaved password");
            Assert(handoff.Profiles.Count == 2 && handoff.Profiles[0].Settings.Password == "", "elevation retains other profiles without persisting their unsaved passwords");
            SecureStorage.Delete(transfer);
            Throws(() => profiles.Add("резервный"), "duplicate profile names are rejected case-insensitively");
            Throws(() => profiles.Rename(" \n "), "empty and multiline profile names are rejected");
            profiles.Rename("Запасной"); SecureStorage.SaveProfiles(profiles);
            Assert(SecureStorage.LoadProfiles().Selected.Name == "Запасной", "renaming a profile persists without changing its ID");
            profiles.RemoveSelected();
            Assert(profiles.SelectedProfileId == firstId && profiles.ProxyApps.Contains(@"C:\Apps\Shared.exe"), "deleting selected profile chooses another and retains shared rules");
            Throws(profiles.RemoveSelected, "last remaining profile cannot be deleted");
            profiles.SelectedProfileId = Guid.NewGuid(); profiles.Normalize();
            Assert(profiles.SelectedProfileId == firstId, "unknown selected profile recovers to an existing profile");
        }
        finally { SecureStorage.RootOverride = previousRoot; }
    }

    private static void TestTraffic()
    {
        var meter = new TrafficMeter();
        meter.Sample(new(0, 0, 0), TimeSpan.Zero);
        meter.Sample(new(1048576, 2097152, 1), TimeSpan.FromSeconds(2));
        Assert(meter.SentBytes == 1048576 && meter.ReceivedBytes == 2097152, "upload and download totals are counted separately in bytes");
        Assert(meter.SendBytesPerSecond == 524288 && meter.ReceiveBytesPerSecond == 1048576, "speeds use measured time between samples");
        meter.Sample(new(1048576, 2097152, 0), TimeSpan.FromSeconds(3));
        Assert(meter.SendBytesPerSecond == 0 && meter.ReceiveBytesPerSecond == 0, "idle connections show zero speed");
        meter.Sample(new(10, 20, 0), TimeSpan.FromSeconds(4));
        Assert(meter.SendBytesPerSecond == 0 && meter.ReceiveBytesPerSecond == 0, "reset counters do not create negative speeds");
        meter.Sample(new(1034, 2068, 1), TimeSpan.FromSeconds(4.5));
        Assert(meter.SendBytesPerSecond == 2048 && meter.ReceiveBytesPerSecond == 4096, "speed remains correct with irregular refresh intervals");
        meter.Stop();
        Assert(meter.SentBytes == 1034 && meter.ReceiveBytesPerSecond == 0, "disconnect retains last totals and clears speed");
        meter.Reset();
        Assert(meter.ReceivedBytes == 0 && meter.SentBytes == 0 && meter.SendBytesPerSecond == 0, "new connection starts new traffic totals");
    }

    private static bool IsAlive(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < timeout) await Task.Delay(50);
    }

    private static async Task CheckProcessLifecycle()
    {
        var ready = Path.Combine(_root, "stalled-core.ready");
        await using (var core = new NativeCore(Environment.ProcessPath!, new[] { "--stall-core", ready }))
        {
            await WaitUntil(() => File.Exists(ready), TimeSpan.FromSeconds(5));
            Assert(File.Exists(ready), "unresponsive core fixture is running");
            var watch = Stopwatch.StartNew();
            await Task.WhenAll(core.StopAsync(), core.StopAsync());
            Assert(core.ForcedStop && !IsAlive(core.ProcessId) && watch.Elapsed < TimeSpan.FromSeconds(15), "unresponsive core is forcibly stopped within a bounded time");
            await Task.WhenAll(core.DisposeAsync().AsTask(), core.DisposeAsync().AsTask());
            Assert(core.HasExited, "repeated concurrent stop and dispose are safe");
        }
        var checkPath = Path.Combine(_root, "stalled-check.json");
        var checkWatch = Stopwatch.StartNew();
        try { await CoreSession.CheckConfigAsync(Environment.ProcessPath!, checkPath, timeout: TimeSpan.FromSeconds(2)); throw new Exception("check did not time out"); }
        catch (IOException) { }
        var checkPid = int.Parse(File.ReadAllText(checkPath + ".pid"));
        Assert(!IsAlive(checkPid) && checkWatch.Elapsed < TimeSpan.FromSeconds(10), "configuration checker timeout leaves no process running");
        var cancelPath = Path.Combine(_root, "cancelled-check.json");
        using (var cancel = new CancellationTokenSource())
        {
            var checking = CoreSession.CheckConfigAsync(Environment.ProcessPath!, cancelPath, cancel.Token);
            await WaitUntil(() => File.Exists(cancelPath + ".pid"), TimeSpan.FromSeconds(5));
            cancel.Cancel();
            try { await checking; throw new Exception("check was not cancelled"); } catch (OperationCanceledException) { }
            Assert(!IsAlive(int.Parse(File.ReadAllText(cancelPath + ".pid"))), "cancelling configuration check terminates its process");
        }
        var parentReady = Path.Combine(_root, "job-parent.ready");
        using var parent = new Process { StartInfo = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true } };
        parent.StartInfo.ArgumentList.Add("--job-parent"); parent.StartInfo.ArgumentList.Add(parentReady);
        int? ownedChild = null;
        parent.Start();
        try
        {
            await WaitUntil(() => File.Exists(parentReady), TimeSpan.FromSeconds(5));
            ownedChild = int.Parse(File.ReadAllText(parentReady));
            Assert(IsAlive(ownedChild.Value), "owned core is alive before GUI-crash simulation");
            parent.Kill(); // Intentionally kill only the parent: its job must clean up the child.
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntil(() => !IsAlive(ownedChild.Value), TimeSpan.FromSeconds(5));
            Assert(!IsAlive(ownedChild.Value), "Windows job removes the core when its owning GUI process crashes");
        }
        finally
        {
            if (!parent.HasExited) { parent.Kill(); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            if (ownedChild is int pid && IsAlive(pid)) { using var child = Process.GetProcessById(pid); child.Kill(true); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    private static async Task CheckStartupCapture(string engine)
    {
        var crashLog = Path.Combine(_root, "early-crash.log");
        await using (var core = new NativeCore(Environment.ProcessPath!, new[] { "--crash-core" }, crashLog)) {
            await core.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var details = File.ReadAllText(crashLog, Encoding.UTF8);
            Assert(core.ExitCode == 23 && details.Contains("early stdout diagnostic") && details.Contains("ошибка до открытия журнала"),
                "early engine stdout, Unicode stderr and exit code are captured before the normal log exists");
        }
        File.Delete(crashLog);
        Assert(!File.Exists(crashLog), "captured startup handles are released after an early crash");

        var stopLog = Path.Combine(_root, "cooperative-core.log");
        await using (var core = new NativeCore(Environment.ProcessPath!, new[] { "--cooperative-core" }, stopLog)) {
            await WaitUntil(() => File.Exists(stopLog) && new FileInfo(stopLog).Length > 0, TimeSpan.FromSeconds(5));
            await core.StopAsync();
            Assert(!core.ForcedStop && core.ExitCode == 0 && File.ReadAllText(stopLog).Contains("cooperative core stopped"),
                "redirecting startup output preserves graceful hidden-console shutdown");
        }

        var runtimeConfig = Path.Combine(_root, "runtime-failure.json");
        var runtimeLog = Path.Combine(_root, "runtime-failure.log");
        var port = CoreSession.FreePort();
        File.WriteAllText(runtimeConfig, JsonSerializer.Serialize(new {
            inbounds = new[] {
                new { type = "mixed", tag = "one", listen = "127.0.0.1", listen_port = port },
                new { type = "mixed", tag = "two", listen = "127.0.0.1", listen_port = port }
            }, outbounds = new[] { new { type = "direct", tag = "direct" } }
        }));
        await CoreSession.CheckConfigAsync(engine, runtimeConfig);
        await using (var core = new NativeCore(engine, runtimeConfig, runtimeLog)) {
            await core.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert(core.ExitCode != 0 && File.ReadAllText(runtimeLog).Contains("FATAL", StringComparison.OrdinalIgnoreCase),
                "a real sing-box startup failure after successful config validation retains the fatal error");
        }
        File.Delete(runtimeConfig); File.Delete(runtimeLog); File.Delete(stopLog);
    }

    private static async Task RepeatedSessions()
    {
        for (var i = 0; i < 3; i++)
        {
            await using var proxy = new MockProxy(ProxyProtocol.Socks5) { StallResponse = i == 2 };
            var session = await CoreSession.StartAsync(Settings(ProxyProtocol.Socks5, proxy.Port), "127.0.0.1", false);
            var pid = session.ProcessId!.Value;
            var request = session.ProxyClient.GetStringAsync("http://203.0.113.40/lifecycle");
            if (i != 2) Assert(await request == "proxied", "traffic works after repeated start: " + (i + 1));
            else await WaitUntil(() => proxy.AuthAccepted > 0, TimeSpan.FromSeconds(5));
            await Task.WhenAll(session.DisposeAsync().AsTask(), session.DisposeAsync().AsTask());
            Assert(!IsAlive(pid), "disconnect leaves no core process: cycle " + (i + 1));
            if (i == 2)
            {
                try { await request.WaitAsync(TimeSpan.FromSeconds(3)); throw new Exception("pending request unexpectedly succeeded"); }
                catch (OperationCanceledException) { Assert(true, "disconnect cancels stalled requests promptly"); }
            }
        }
        Assert(!Directory.EnumerateDirectories(Path.Combine(SecureStorage.Root, "sessions")).Any(), "disconnected sessions leave no private runtime folders");
    }

    private static async Task CancelledSession()
    {
        await using var proxy = new MockProxy(ProxyProtocol.Socks5);
        using var cancellation = new CancellationTokenSource(); cancellation.CancelAfter(30);
        try
        {
            await using var unexpected = await CoreSession.StartAsync(Settings(ProxyProtocol.Socks5, proxy.Port), "127.0.0.1", false, cancellation.Token);
            throw new Exception("cancelled startup unexpectedly succeeded");
        }
        catch (OperationCanceledException) { Assert(true, "startup cancellation is propagated"); }
        Assert(!Directory.EnumerateFiles(SecureStorage.Root, "core.json", SearchOption.AllDirectories).Any(), "cancelled startup removes plaintext runtime configuration");
        Assert(!Directory.EnumerateDirectories(Path.Combine(SecureStorage.Root, "sessions")).Any(), "cancelled startup cleans its session folder");
    }

    private static void ValidateInput()
    {
        Assert(InputRules.Host("[2001:db8::1]") == "2001:db8::1", "IPv6 proxy addresses");
        Assert(InputRules.Site("https://пример.рф/path?q=1").Value == "xn--e1afmkfd.xn--p1ai", "URL and IDN domain normalization");
        Assert(InputRules.Site("*.Example.COM").Value == "example.com", "domain wildcard normalization");
        Assert(InputRules.Site("198.51.100.87/24").Value == "198.51.100.0/24", "CIDR network normalization");
        Assert(InputRules.Site("2001:db8::cafe/64").Value == "2001:db8::/64", "IPv6 CIDR network normalization");
        Throws(() => InputRules.Host("https://proxy.example:1080"), "reject URLs in proxy host field");
        Throws(() => InputRules.Site("198.51.100.1/99"), "reject invalid CIDR prefix");
        Throws(() => InputRules.Site("*"), "reject universal domain bypass");
        Throws(() => InputRules.App("chrome.exe"), "reject unqualified app path");
        Throws(() => new ProxySettings { Host = "proxy.example", Port = 0 }.Validate(), "reject invalid port");
        Throws(() => new ProxySettings { Host = "proxy.example", Username = new string('я', 128) }.Validate(), "SOCKS5 limits count UTF-8 bytes");
        var pattern = ConfigBuilder.PathPattern(@"C:\Program Files\Example+App\client.exe");
        Assert(Regex.IsMatch(@"c:\program files\example+app\CLIENT.EXE", pattern) &&
            !Regex.IsMatch(@"C:\Program Files\Example+App\client.exe.evil", pattern), "exact, case-insensitive per-app bypass");
    }

    private static void TestStorage()
    {
        var settings = new ProxySettings { Host = "proxy.example", Username = "private-user", Password = "private-password-123" };
        SecureStorage.Save(settings);
        Assert(SecureStorage.Load().Password == settings.Password, "Windows DPAPI round trip");
        Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(SecureStorage.SettingsPath)).Contains(settings.Password), "no plaintext password in saved settings");
        settings.RememberPassword = false;
        SecureStorage.Save(settings);
        Assert(SecureStorage.Load().Password == "", "remember-password off excludes secret");
        var path = Path.Combine(SecureStorage.Root, "handoff", "connect-test.dat");
        SecureStorage.Save(settings, path, includePassword: true);
        Assert(SecureStorage.Load(path).Password == settings.Password, "encrypted one-time elevation handoff preserves unsaved password");
        Assert(Elevation.ValidateTransferPath(path) == path, "handoff allows private handoff folder");
        try { Elevation.ValidateTransferPath(Path.Combine(_root, "connect-test.dat")); throw new Exception("handoff accepted unsafe path"); }
        catch (IOException) { Assert(true, "handoff rejects arbitrary external paths"); }
        SecureStorage.Delete(path);
    }

    private static ProxySettings Settings(ProxyProtocol protocol, int port) => new() {
        Protocol = protocol, Host = protocol == ProxyProtocol.Https ? "localhost" : "127.0.0.1", Port = port,
        Username = "test-user", Password = "test-secret"
    };

    private static async Task CheckConfigurations(string exe)
    {
        foreach (var protocol in Enum.GetValues<ProxyProtocol>())
        foreach (var tunnel in new[] { false, true })
        {
            var settings = Settings(protocol, 1080);
            settings.ProxyApps.Add(@"C:\Program Files\Example App\Client.exe");
            var options = new CoreOptions("127.0.0.1", CoreSession.FreePort(), "probe", "secret", CoreSession.FreePort(), "test-token", Path.Combine(_root, "check.log"), tunnel);
            var config = ConfigBuilder.Build(settings, options);
            var path = Path.Combine(_root, $"{protocol}-{tunnel}.json");
            await File.WriteAllTextAsync(path, config);
            await CoreSession.CheckConfigAsync(exe, path);
            Assert(true, $"real engine config validation: {protocol}, TUN={tunnel}");
            var json = JsonNode.Parse(config)!;
            Assert(json["route"]!["final"]!.GetValue<string>() == "direct" && json["dns"]!["final"]!.GetValue<string>() == "proxy-dns", $"{protocol}: unselected applications default direct and DNS uses proxy");
            var rule = json["route"]!["rules"]![0]!;
            Assert(rule["inbound"]![0]!.GetValue<string>() == "probe-in" && rule["outbound"]!.GetValue<string>() == "proxy", "IP probe takes priority over bypass rules");
            var rejects = json["route"]!["rules"]!.AsArray().Where(r => r?["action"]?.GetValue<string>() == "reject").ToArray();
            Assert(rejects.Length > 0 && rejects.All(r => r?["process_path_regex"] is not null || r?["process_name"] is not null || r?["domain_suffix"] is not null), "unsupported protocols are rejected only for selected routes");
            Assert(rejects.All(r => r!["network"]!.AsArray().Any(n => n!.GetValue<string>() == "udp")), "selected routes disable UDP by default even for SOCKS5");
            settings.EnableUdp = true;
            var enabled = JsonNode.Parse(ConfigBuilder.Build(settings, options))!["route"]!["rules"]!.AsArray();
            Assert(enabled.Where(r => r?["action"]?.GetValue<string>() == "reject").All(r => r!["network"]!.AsArray().Any(n => n!.GetValue<string>() == "udp") == (protocol != ProxyProtocol.Socks5)), "UDP checkbox enables UDP only for SOCKS5");
            SecureStorage.Delete(path);
        }
    }

    private static async Task AuthenticatedProxy(ProxyProtocol protocol)
    {
        await using var proxy = new MockProxy(protocol);
        var session = await CoreSession.StartAsync(Settings(protocol, proxy.Port), "127.0.0.1", false);
        Assert(!Directory.EnumerateFiles(SecureStorage.Root, "core.json", SearchOption.AllDirectories).Any(), "config with password removed after core startup");
        var response = await session.ProxyClient.GetStringAsync("http://203.0.113.40/test");
        Assert(response == "proxied" && proxy.AuthAccepted > 0, protocol + " authentication and traffic through bundled engine");
        Assert(proxy.LastTarget?.Contains("203.0.113.40") == true, protocol + " forwards target correctly");
        var traffic = await session.GetTrafficAsync();
        Assert(traffic.Uploaded > 0 && traffic.Downloaded > 0, "real traffic counters from protected local API");
        Assert(session.ReadLogTail().Length > 0, "engine produces journal entries");
        session.ClearVisibleLog();
        Assert(session.ReadLogTail().Length == 0, "clear removes old engine entries without closing its log handle");
        Assert(await session.ProxyClient.GetStringAsync("http://203.0.113.41/after-clear") == "proxied", "clearing the journal keeps traffic working");
        Assert(session.ReadLogTail().Contains("203.0.113.41"), "new engine entries remain visible after clear");
        await session.DisposeAsync();
        Assert(!session.ForcedStop, "graceful core shutdown via console signal: " + (session.ShutdownDiagnostic ?? "signal delivered"));
    }

    private static async Task RejectedCredentials(ProxyProtocol protocol)
    {
        await using var proxy = new MockProxy(protocol);
        var settings = Settings(protocol, proxy.Port); settings.Password = "incorrect";
        await using var session = await CoreSession.StartAsync(settings, "127.0.0.1", false);
        var succeeded = false;
        try { using var response = await session.ProxyClient.GetAsync("http://203.0.113.40/test"); succeeded = response.IsSuccessStatusCode; }
        catch (HttpRequestException) { }
        Assert(!succeeded && proxy.AuthRejected > 0 && proxy.AuthAccepted == 0, protocol + " wrong password rejected with no direct fallback");
    }

    private static async Task AnonymousProxy()
    {
        await using var proxy = new MockProxy(ProxyProtocol.Socks5) { Anonymous = true };
        var settings = Settings(ProxyProtocol.Socks5, proxy.Port); settings.Username = ""; settings.Password = "";
        await using var session = await CoreSession.StartAsync(settings, "127.0.0.1", false);
        Assert(await session.ProxyClient.GetStringAsync("http://203.0.113.40/test") == "proxied", "anonymous SOCKS5 server");
    }

    private static async Task RejectUntrustedTls()
    {
        await using var proxy = new MockProxy(ProxyProtocol.Https);
        await using var session = await CoreSession.StartAsync(Settings(ProxyProtocol.Https, proxy.Port), "127.0.0.1", false);
        var succeeded = false;
        try { using var response = await session.ProxyClient.GetAsync("http://203.0.113.40/test"); succeeded = response.IsSuccessStatusCode; }
        catch (HttpRequestException) { }
        Assert(!succeeded && proxy.Connections > 0 && proxy.AuthAccepted == 0, "HTTPS proxy certificate must be trusted; credentials are not sent to an untrusted peer");
    }

    private static void TestLanguages()
    {
        var keys = L.ReadCatalog("en").Keys.OrderBy(k => k).ToArray();
        foreach (var language in L.Supported)
        {
            var catalog = L.ReadCatalog(language.Code);
            Assert(catalog.Keys.OrderBy(k => k).SequenceEqual(keys) && catalog.Values.All(v => !string.IsNullOrWhiteSpace(v)), "complete translation catalog: " + language.Code);
            L.Current.Set(language.Code);
            Assert(L.Current.Help.Count == 12 && L.Current.Help.All(h => h.Body.Length > 140), "twelve detailed help topics: " + language.Code);
            foreach (var pair in catalog)
            {
                var indexes = Regex.Matches(pair.Value, @"\{(\d+)\}").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
                if (indexes.Length > 0) _ = string.Format(pair.Value, Enumerable.Repeat<object>("test", indexes.Max() + 1).ToArray());
            }
        }
        L.Current.Set("ru");
        Assert(new ProxySettings().ProxyStore && !new ProxySettings().EnableUdp, "Store is on and UDP is off by default");
        Assert(AppCatalog.NormalizeBrowserPath(@"C:\Browser\msedge_proxy.exe") == @"C:\Browser\msedge.exe", "Edge PWA maps to its actual network browser");
        Assert(AppCatalog.NormalizeBrowserPath(@"C:\Browser\chrome_proxy.exe") == @"C:\Browser\chrome.exe", "Chrome PWA maps to its actual network browser");
    }

    private static async Task WaitCore(int port)
    {
        using var api = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
        api.DefaultRequestHeaders.Add("Authorization", "Bearer api-secret");
        var ready = false;
        for (var i = 0; i < 60; i++) {
            try { using var response = await api.GetAsync($"http://127.0.0.1:{port}/version"); ready = response.IsSuccessStatusCode; if (ready) break; }
            catch (HttpRequestException) { } catch (TaskCanceledException) { }
            await Task.Delay(100);
        }
        Assert(ready, "isolated routing core is ready");
    }

    private static HttpClient RuleClient(int port) => new(new HttpClientHandler { UseProxy = true,
        Proxy = new WebProxy($"http://127.0.0.1:{port}") { BypassProxyOnLocal = false } }) { Timeout = TimeSpan.FromSeconds(10) };

    private static async Task ApplicationRouting(string exe, bool selected, bool self, bool keepLan = false)
    {
        await using var proxy = new MockProxy(ProxyProtocol.Socks5);
        await using var origin = new DirectOrigin();
        var settings = Settings(ProxyProtocol.Socks5, proxy.Port);
        if (selected) settings.ProxyApps.Add(Environment.ProcessPath!);
        var rulePort = CoreSession.FreePort();
        var options = new CoreOptions("127.0.0.1", CoreSession.FreePort(), "probe", "secret", CoreSession.FreePort(), "api-secret",
            Path.Combine(_root, "app-route.log"), false, SelfPath: self ? Environment.ProcessPath : @"C:\Unrelated\ProxyChoiceApp.exe");
        var json = JsonNode.Parse(ConfigBuilder.Build(settings, options))!;
        var rules = json["route"]!["rules"]!.AsArray();
        // The test origin is private. Remove only the LAN rule when exercising public app routing.
        if (!keepLan) rules.Remove(rules.Single(r => r?["ip_is_private"] is not null));
        json["inbounds"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { type = "mixed", tag = "rule-test", listen = "127.0.0.1", listen_port = rulePort }));
        var path = Path.Combine(_root, "app-route.json");
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await CoreSession.CheckConfigAsync(exe, path);
        await using var core = new NativeCore(exe, path);
        await WaitCore(options.ApiPort);
        using var client = RuleClient(rulePort);
        var target = $"http://127.0.0.1:{origin.Port}/test";
        var result = await client.GetStringAsync(target);
        var shouldProxy = (selected || self) && !keepLan;
        Assert(result == (shouldProxy ? "proxied" : "direct") && origin.Requests == (shouldProxy ? 0 : 1),
            keepLan ? "selected apps retain direct access to private LAN addresses" : self ? "ProxyChoiceApp itself is mandatory even when no apps are selected" :
            selected ? "selected process uses the proxy with real Windows process detection" : "unselected process reaches the direct origin");
        using var probe = new HttpClient(new HttpClientHandler { UseProxy = true, Proxy = new WebProxy($"socks5://127.0.0.1:{options.ProbePort}") {
            Credentials = new NetworkCredential("probe", "secret"), BypassProxyOnLocal = false } });
        Assert(await probe.GetStringAsync(target) == "proxied", "IP probe takes precedence over default direct and LAN rules");
        await core.StopAsync(); Assert(!core.ForcedStop, "routing core exits gracefully");
        SecureStorage.Delete(path); SecureStorage.Delete(options.LogPath);
    }

    private static async Task StoreRouting(string exe, bool enabled)
    {
        await using var proxy = new MockProxy(ProxyProtocol.Socks5);
        await using var origin = new DirectOrigin();
        await using var dns = new MockDns();
        var settings = Settings(ProxyProtocol.Socks5, proxy.Port); settings.ProxyStore = enabled;
        var rulePort = CoreSession.FreePort();
        var options = new CoreOptions("127.0.0.1", CoreSession.FreePort(), "probe", "secret", CoreSession.FreePort(), "api-secret",
            Path.Combine(_root, "store-route.log"), false, "127.0.0.1", @"C:\Unrelated\ProxyChoiceApp.exe");
        var json = JsonNode.Parse(ConfigBuilder.Build(settings, options))!;
        json["dns"]!["servers"]![1]!["server_port"] = dns.Port;
        var rules = json["route"]!["rules"]!.AsArray(); rules.Remove(rules.Single(r => r?["ip_is_private"] is not null));
        Assert(rules.Any(r => r?["process_name"] is not null) == enabled, "Store process rule respects the checkbox");
        json["inbounds"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { type = "mixed", tag = "rule-test", listen = "127.0.0.1", listen_port = rulePort }));
        var path = Path.Combine(_root, "store-route.json");
        await File.WriteAllTextAsync(path, json.ToJsonString()); await CoreSession.CheckConfigAsync(exe, path);
        await using var core = new NativeCore(exe, path); await WaitCore(options.ApiPort);
        using var client = RuleClient(rulePort);
        Assert(await client.GetStringAsync($"http://displaycatalog.mp.microsoft.com:{origin.Port}/test") == (enabled ? "proxied" : "direct"), "Store service domain checkbox routing: " + enabled);
        Assert(await client.GetStringAsync($"http://cdn.displaycatalog.mp.microsoft.com:{origin.Port}/test") == (enabled ? "proxied" : "direct"), "Store service subdomains follow the same rule");
        Assert(await client.GetStringAsync($"http://other.test:{origin.Port}/test") == "direct", "unrelated domains retain ISP routing with Store " + enabled);
        await core.StopAsync(); SecureStorage.Delete(path); SecureStorage.Delete(options.LogPath);
    }
}
