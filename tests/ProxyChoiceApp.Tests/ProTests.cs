using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;

namespace ProxyChoiceApp.Tests;

internal static partial class Program
{
    private static async Task CheckIpv4Compatibility(string engine)
    {
        var attempts = new System.Collections.Generic.List<bool>();
        var logs = new System.Collections.Generic.List<string>();
        var proxy = Settings(ProxyProtocol.Socks5, 1080);
        var failure = "FATAL start inbound/tun[tun-in]: configure tun interface: set ipv6 address: Element not found.";
        Task<string> Start(bool ipv6, CancellationToken token) {
            attempts.Add(ipv6);
            return ipv6 ? Task.FromException<string>(new CoreStartupException(failure, true)) : Task.FromResult("connected");
        }
        var result = await TunCompatibility.StartAsync(proxy, true, Start, logs.Add, CancellationToken.None);
        Assert(result == "connected" && attempts.SequenceEqual(new[] { true, false }) && logs.Count == 1,
            "reported Windows IPv6 address failure retries once with IPv4 and emits a diagnostic");
        attempts.Clear();
        try { await TunCompatibility.StartAsync(proxy, false, Start, null, CancellationToken.None); throw new Exception("preflight incorrectly retried"); }
        catch (CoreStartupException) { Assert(attempts.SequenceEqual(new[] { true }), "userspace preflight failures never trigger a TUN compatibility retry"); }
        attempts.Clear();
        try {
            await TunCompatibility.StartAsync(proxy, true, (ipv6, token) => {
                attempts.Add(ipv6); return Task.FromException<string>(new CoreStartupException("access denied creating adapter", true));
            }, null, CancellationToken.None);
            throw new Exception("unrelated failure incorrectly retried");
        } catch (CoreStartupException) { Assert(attempts.SequenceEqual(new[] { true }), "unrelated adapter failures are preserved without compatibility retry"); }
        attempts.Clear();
        using (var cancellation = new CancellationTokenSource()) {
            try {
                await TunCompatibility.StartAsync(proxy, true, (ipv6, token) => {
                    attempts.Add(ipv6); cancellation.Cancel(); return Task.FromException<string>(new CoreStartupException(failure, true));
                }, null, cancellation.Token);
                throw new Exception("cancelled startup incorrectly retried");
            } catch (OperationCanceledException) { Assert(attempts.SequenceEqual(new[] { true }), "manual cancellation prevents an IPv4 retry after a failed IPv6 launch"); }
        }
        attempts.Clear();
        try {
            await TunCompatibility.StartAsync(proxy, true, (ipv6, token) => {
                attempts.Add(ipv6); return Task.FromException<string>(new CoreStartupException(failure, true));
            }, null, CancellationToken.None);
            throw new Exception("unbounded retries");
        } catch (CoreStartupException) { Assert(attempts.SequenceEqual(new[] { true, false }), "a failed IPv4 compatibility launch is returned without further retries"); }
        var imported = WireGuardImporter.Parse(ExampleProfile);
        var work = new ProxySettings { Mode = ConnectionMode.WorkNetwork, Host = imported.Host, Port = imported.Port, WorkNetwork = imported.Profile };
        attempts.Clear();
        try { await TunCompatibility.StartAsync(work, true, Start, null, CancellationToken.None); throw new Exception("IPv6 work routes were silently dropped"); }
        catch (CoreStartupException error) { Assert(attempts.SequenceEqual(new[] { true }) && error.Message.Contains(failure), "explicit IPv6 work routes prevent IPv4 fallback and retain the original error"); }
        work.WorkNetwork.Routes = new() { "198.51.100.85/32" }; work.WorkNetwork.ProbeHost = "198.51.100.85";
        attempts.Clear();
        Assert(await TunCompatibility.StartAsync(work, true, Start, null, CancellationToken.None) == "connected" && attempts.SequenceEqual(new[] { true, false }),
            "IPv4 work routes remain usable through the same compatibility retry");

        foreach (var settings in new[] { proxy, work }) {
            var path = Path.Combine(_root, "ipv4-compat-" + settings.Mode + ".json");
            var options = new CoreOptions("127.0.0.1", CoreSession.FreePort(), "probe", "secret", CoreSession.FreePort(), "token", path + ".log", true, TunIpv6: false);
            var config = ConfigBuilder.Build(settings, options);
            File.WriteAllText(path, config);
            await CoreSession.CheckConfigAsync(engine, path);
            var json = JsonNode.Parse(config)!;
            Assert(json["inbounds"]![0]!["address"]!.AsArray().Select(v => v!.ToString()).SequenceEqual(new[] { "172.30.254.1/30" }),
                "real engine accepts the IPv4-only adapter configuration: " + settings.Mode);
            if (settings.Mode == ConnectionMode.Proxy) {
                Assert(json["dns"]!["strategy"]!.ToString() == "ipv4_only" && !config.Contains("inet6_range") &&
                    json["dns"]!["rules"]![0]!["query_type"]!.AsArray().Count == 1 && json["route"]!["final"]!.ToString() == "direct",
                    "IPv4 fallback uses IPv4 DNS and preserves per-app direct routing");
            } else {
                Assert(json["inbounds"]![0]!["route_address"]!.AsArray().Single()!.ToString() == "198.51.100.85/32" &&
                    json["endpoints"]![0]!["private_key"]!.ToString() == work.WorkNetwork.PrivateKey,
                    "IPv4 fallback retains the work routes and WireGuard credentials");
            }
            File.Delete(path);
        }
    }

    private static string ExampleKey(int value = 1) => Convert.ToBase64String(Enumerable.Repeat((byte)value, 32).ToArray());
    private static string ExampleProfile => $"""
        [Interface]
        PrivateKey = {ExampleKey()}
        Address = 192.0.2.2/24, 2001:db8:77::2/64
        MTU = 1408
        DNS = 198.51.100.1
        [Peer]
        PublicKey = {ExampleKey(2)}
        PresharedKey = {ExampleKey(3)}
        AllowedIPs = 192.0.2.1/32, 198.51.100.85/32, 2001:db8:77::/64
        Endpoint = vpn.example.invalid:51820
        PersistentKeepalive = 25
        """;
    private static ProxySettings WorkSettings() {
        var imported = WireGuardImporter.Parse(ExampleProfile, "laptop.conf");
        return new() { Mode = ConnectionMode.WorkNetwork, Host = imported.Host, Port = imported.Port, WorkNetwork = imported.Profile };
    }
    private static void TestWorkProfiles()
    {
        var imported = WireGuardImporter.Parse("\uFEFF" + ExampleProfile + "\n# comment\n; comment", "laptop.conf");
        imported.Profile.Validate();
        Assert(imported.Host == "vpn.example.invalid" && imported.Port == 51820 && imported.Profile.Addresses[0] == "192.0.2.2/24", "WireGuard import retains the assigned client address and endpoint");
        Assert(imported.Profile.PreSharedKey == ExampleKey(3) && imported.Profile.ProbeHost == "192.0.2.1", "WireGuard import preserves optional authentication and suggests a work probe");
        var annotated = WireGuardImporter.Parse("# ProxyChoicePro.Routes = 198.51.100.85/32\n# ProxyChoicePro.ProbeHost = 198.51.100.85\n# ProxyChoicePro.ProbePort = 3389\n" + ExampleProfile).Profile;
        Assert(annotated.Routes.Single() == "198.51.100.85/32" && annotated.ProbeHost == "198.51.100.85" && annotated.ProbePort == 3389, "prepared client profiles retain app-specific work routes and the RDP probe");
        Throws(() => WireGuardImporter.Parse("# ProxyChoicePro.Routes = 203.0.113.8/32\n" + ExampleProfile), "profile metadata cannot route outside peer authorization");
        var ipv6 = WireGuardImporter.Parse(ExampleProfile.Replace("vpn.example.invalid:51820", "[2001:db8::1]:51821"));
        Assert(ipv6.Host == "2001:db8::1" && ipv6.Port == 51821, "WireGuard IPv6 endpoint import");
        Throws(() => WireGuardImporter.Parse(ExampleProfile + "\n[Peer]\nPublicKey = " + ExampleKey()), "multiple WireGuard peers require separate client profiles");
        Throws(() => WireGuardImporter.Parse(ExampleProfile.Replace("MTU = 1408", "postup = arbitrary command")), "import rejects scripts regardless of letter case");
        Throws(() => WireGuardImporter.Parse(ExampleProfile.Replace(ExampleKey(), "bad-key")), "malformed WireGuard keys are rejected before startup");
        Throws(() => WireGuardImporter.Parse(ExampleProfile.Replace("192.0.2.2/24", "192.0.2.2/50")), "invalid WireGuard client prefix is rejected");
        Throws(() => WireGuardImporter.Parse(ExampleProfile.Replace(":51820", ":99999")), "invalid WireGuard endpoint port is rejected");
        var full = WireGuardImporter.Parse(ExampleProfile.Replace("192.0.2.1/32, 198.51.100.85/32, 2001:db8:77::/64", "0.0.0.0/0, ::/0")).Profile;
        Assert(full.Routes.Count == 0, "a full-tunnel profile does not silently route the entire Internet");
        Throws(full.Validate, "full-tunnel import needs explicit work routes and probe");
        full.Routes.Add("198.51.100.0/24"); full.ProbeHost = "198.51.100.1"; full.Validate();
        Assert(NetworkDiagnostics.InsideWork(full, "198.51.100.85") && !NetworkDiagnostics.InsideWork(full, "203.0.113.8"), "explicit work routes can be selected from a full-tunnel peer");
        var invalid = imported.Profile.Copy(); invalid.Routes.Add("203.0.113.0/24");
        Throws(invalid.Validate, "work routes must be covered by the peer's AllowedIPs");
        invalid = imported.Profile.Copy(); invalid.Routes.Add("0.0.0.0/0");
        Throws(invalid.Validate, "work mode rejects an Internet default route");
        invalid = imported.Profile.Copy(); invalid.ProbeHost = "203.0.113.8";
        Throws(invalid.Validate, "the work diagnostic cannot probe a direct Internet destination");
        Assert(IpNetwork.Parse("198.51.100.87/24").ToString() == "198.51.100.0/24" && IpNetwork.Parse("2001:db8:77::cafe/64").Contains(IPAddress.Parse("2001:db8:77::2")), "IPv4 and IPv6 subnet calculations retain network boundaries");
        var conflicts = NetworkDiagnostics.FindConflicts(new[] { "198.51.100.0/24", "2001:db8:77::/64" }, new[] { ("198.51.100.20/24", "Wi-Fi"), ("2001:db8:77::42/64", "Ethernet"), ("203.0.113.2/24", "Other") });
        Assert(conflicts.Count == 2 && conflicts.Any(c => c.Adapter == "Wi-Fi"), "diagnostics detect overlapping home/work networks for both IP families");
        Assert(NetworkDiagnostics.FindConflicts(new[] { "203.0.113.0/24" }, new[] { ("198.51.100.20/24", "Wi-Fi") }).Count == 0, "adjacent subnets are not reported as conflicts");
        var pc = new RemoteComputer { Name = "Office", Host = "198.51.100.85", Username = @"WORK\user" };
        var rdp = NetworkDiagnostics.RdpDocument(pc);
        Assert(rdp.Contains("full address:s:198.51.100.85:3389") && rdp.Contains("prompt for credentials:i:1") && rdp.Contains("enablecredsspsupport:i:1") && !rdp.Contains("password"), "RDP files preserve Windows authentication and contain no saved password");
        pc.Host = "2001:db8:77::85"; Assert(NetworkDiagnostics.RdpDocument(pc).Contains("[2001:db8:77::85]:3389"), "RDP favorites support IPv6 addresses");
        pc.Username = "valid\r\nredirectdrives:i:1"; Throws(() => NetworkDiagnostics.RdpDocument(pc), "RDP settings cannot be injected through the username");
        pc.Username = "user"; pc.Host = "office.example.com"; Throws(pc.Validate, "work favorites require an explicit IP address");
        var store = new ProxyProfiles { DarkMode = true, AutoReconnect = false };
        store.Normalize(); store.UpdateSelected(WorkSettings());
        store.RemoteComputers.Add(new() { Name = "Office", Host = "198.51.100.85" });
        var path = Path.Combine(SecureStorage.Root, "work-roundtrip.dat"); SecureStorage.SaveProfiles(store, path);
        var saved = SecureStorage.LoadProfiles(path);
        Assert(saved.DarkMode && !saved.AutoReconnect && saved.RemoteComputers.Single().Host == "198.51.100.85" && saved.SelectedSettings().WorkNetwork!.PrivateKey == ExampleKey(), "theme, reconnect choice, favorites and VPN credentials survive an encrypted restart");
        Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(ExampleKey()), "saved WireGuard private keys are protected by Windows DPAPI");
        SecureStorage.Delete(path);
    }
    private static void TestKnownApplications()
    {
        var directory = Path.Combine(_root, "catalog-fixture"); Directory.CreateDirectory(directory);
        string Fixture(string relative) { var path = Path.Combine(directory, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "fixture"); return path; }
        var chat = Fixture(@"OpenAI.Codex_fixture\app\ChatGPT.exe"); var codex = Fixture(@"bin\codex.exe");
        var host = Fixture(@"bin\codex-code-mode-host.exe"); var swift = Fixture(@"runtime\codex-computer-use-swift.exe");
        var paths = new[] { chat, codex, host, swift };
        var apps = AppCatalog.DiscoverKnown(paths.Select(p => new AppEntry(p)), _ => Array.Empty<string>());
        Assert(new[] { "Google Chrome", "Microsoft Edge", "ChatGPT", "Codex", "codex-computer-use-swift" }.All(n => apps.Any(a => a.Name == n)), "requested browsers, ChatGPT, Codex and computer-use helper have catalog entries");
        Assert(apps.Single(a => a.Name == "Codex").RoutePaths.OrderBy(p => p).SequenceEqual(new[] { codex, host, chat }.OrderBy(p => p)), "Codex selection includes its actual frontend and network helper executables");
        Assert(apps.Single(a => a.Name == "ChatGPT").RoutePaths.Intersect(apps.Single(a => a.Name == "Codex").RoutePaths).Contains(chat), "shared ChatGPT/Codex processes cannot silently receive conflicting selections");
        Assert(apps.Single(a => a.Name == "Google Chrome").Available == false && apps.Single(a => a.Name == "codex-computer-use-swift").Available, "catalog distinguishes missing applications from detected executable paths");
    }
    private static async Task<int> ProbePersonalWork(string file, string resultDirectory)
    {
        string secret = "";
        try {
            Directory.CreateDirectory(resultDirectory); SecureStorage.RootOverride = Path.Combine(resultDirectory, "runtime");
            ProxyChoiceApp.Localization.L.Current.Set("ru");
            var imported = WireGuardImporter.Parse(await File.ReadAllTextAsync(file), Path.GetFileName(file)); secret = imported.Profile.PrivateKey;
            var settings = new ProxySettings { Mode = ConnectionMode.WorkNetwork, Host = imported.Host, Port = imported.Port, WorkNetwork = imported.Profile };
            settings.Validate();
            var addresses = await Dns.GetHostAddressesAsync(settings.Host);
            var address = addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? addresses.First();
            await using var session = await CoreSession.StartAsync(settings, address.ToString(), false);
            await session.ProbeTcpAsync(imported.Profile.ProbeHost, imported.Profile.ProbePort);
            var result = JsonSerializer.Serialize(new { status = "passed", endpoint = settings.Host, serverIp = address.ToString(), probe = imported.Profile.ProbeHost, port = imported.Profile.ProbePort });
            await File.WriteAllTextAsync(Path.Combine(resultDirectory, "work-probe.json"), result); Console.WriteLine(result); return 0;
        }
        catch (Exception error) {
            var message = error.Message; if (secret.Length > 0) message = message.Replace(secret, "[redacted]");
            var result = JsonSerializer.Serialize(new { status = "failed", message });
            Directory.CreateDirectory(resultDirectory); await File.WriteAllTextAsync(Path.Combine(resultDirectory, "work-probe.json"), result); Console.WriteLine(result); return 1;
        }
    }
    private static async Task CheckWorkConfigurations(string exe)
    {
        foreach (var tunnel in new[] { false, true }) {
            var settings = WorkSettings();
            settings.ProxyApps.Add(@"C:\Unused\Browser.exe");
            var options = new CoreOptions("127.0.0.1", CoreSession.FreePort(), "probe", "secret", CoreSession.FreePort(), "api-secret", Path.Combine(_root, "work-check.log"), tunnel);
            var config = ConfigBuilder.Build(settings, options); var json = JsonNode.Parse(config)!;
            var path = Path.Combine(_root, "work-check.json"); await File.WriteAllTextAsync(path, config);
            try { await CoreSession.CheckConfigAsync(exe, path); } finally { SecureStorage.Delete(path); }
            Assert(true, "real engine validates WireGuard configuration, TUN=" + tunnel);
            Assert(json["route"]!["rules"]![0]!["outbound"]!.GetValue<string>() == "work" && !config.Contains("ip_is_private") && !config.Contains("process_path_regex"), "private work addresses use WireGuard regardless of proxy application choices");
            if (tunnel) Assert(json["inbounds"]![0]!["dns_mode"]!.GetValue<string>() == "disabled" && json["inbounds"]![0]!["route_address"]!.AsArray().Count == settings.WorkNetwork!.Routes.Count, "Windows TUN installs only work routes and retains ordinary DNS");
        }
    }
    private static async Task<(string Private, string Public)> GenerateWireGuardKeys(string exe)
    {
        using var process = new Process { StartInfo = new(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("generate"); process.StartInfo.ArgumentList.Add("wg-keypair"); process.Start();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if (process.ExitCode != 0) throw new IOException("WireGuard test key generation failed.");
        var lines = (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keys = lines.Select(l => l[(l.IndexOf(':') + 1)..].Trim()).ToArray();
        WorkNetworkProfile.ValidateKey(keys[0]); WorkNetworkProfile.ValidateKey(keys[1]);
        return (keys[0], keys[1]);
    }
    private static async Task CheckConnectionRecovery()
    {
        var attempts = 0; var errors = 0; var waits = new System.Collections.Generic.List<int>();
        await ConnectionRecovery.RunAsync(() => Task.CompletedTask,
            _ => ++attempts < 7 ? Task.FromException(new IOException("unavailable")) : Task.CompletedTask,
            () => true, waits.Add, _ => errors++, CancellationToken.None, (_, token) => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        Assert(attempts == 7 && errors == 6 && waits.SequenceEqual(new[] { 5, 10, 20, 40, 60, 60, 60 }), "automatic recovery survives failures, bounds retry delay and stops after success");
        using var cancel = new CancellationTokenSource(); var waiting = new TaskCompletionSource(); attempts = 0;
        var recovery = ConnectionRecovery.RunAsync(() => Task.CompletedTask, _ => { attempts++; return Task.CompletedTask; },
            () => true, _ => waiting.TrySetResult(), _ => { }, cancel.Token);
        await waiting.Task; cancel.Cancel();
        try { await recovery.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("retry cancellation was ignored"); }
        catch (OperationCanceledException) { Assert(attempts == 0, "manual disconnect interrupts pending recovery without starting another connection"); }
        var networkChecks = 0; attempts = 0;
        await ConnectionRecovery.RunAsync(() => Task.CompletedTask, _ => { attempts++; return Task.CompletedTask; },
            () => ++networkChecks > 2, _ => { }, _ => { }, CancellationToken.None, (_, _) => Task.CompletedTask);
        Assert(networkChecks == 3 && attempts == 1, "offline recovery waits for network availability before reconnecting");
        using var beforeStart = new CancellationTokenSource(); beforeStart.Cancel(); attempts = 0;
        try { await ConnectionRecovery.RunAsync(() => Task.CompletedTask, _ => { attempts++; return Task.CompletedTask; }, () => true, _ => { }, _ => { }, beforeStart.Token); }
        catch (OperationCanceledException) { }
        Assert(attempts == 0, "cancelled recovery cannot start after window shutdown");
    }
    private static async Task RealWireGuardTraffic(string exe)
    {
        var serverKeys = await GenerateWireGuardKeys(exe); var clientKeys = await GenerateWireGuardKeys(exe);
        var target = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address).First(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); var serverPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port; udp.Close();
        var apiPort = CoreSession.FreePort(); var path = Path.Combine(_root, "wg-server.json"); var log = Path.Combine(_root, "wg-server.log");
        var config = JsonSerializer.Serialize(new {
            log = new { level = "info", output = log },
            endpoints = new[] { new { type = "wireguard", tag = "wg-server", system = false, address = new[] { "192.0.2.1/24" }, private_key = serverKeys.Private,
                listen_port = serverPort, peers = new[] { new { public_key = clientKeys.Public, allowed_ips = new[] { "192.0.2.2/32" } } } } },
            outbounds = new[] { new { type = "direct", tag = "direct" } }, route = new { final = "direct" },
            experimental = new { clash_api = new { external_controller = "127.0.0.1:" + apiPort, secret = "api-secret" } }
        });
        await File.WriteAllTextAsync(path, config); await CoreSession.CheckConfigAsync(exe, path);
        await using var origin = new DirectOrigin(IPAddress.Any);
        await using var server = new NativeCore(exe, path);
        try {
            await WaitCore(apiPort); SecureStorage.Delete(path);
            var settings = new ProxySettings { Mode = ConnectionMode.WorkNetwork, Host = "127.0.0.1", Port = serverPort,
                WorkNetwork = new() { PrivateKey = clientKeys.Private, PublicKey = serverKeys.Public, Addresses = new() { "192.0.2.2/24" },
                    AllowedIps = new() { target + "/32" }, Routes = new() { target + "/32" }, ProbeHost = target.ToString(), ProbePort = origin.Port } };
            await using (var client = await CoreSession.StartAsync(settings, "127.0.0.1", false)) {
                Assert(await client.ProxyClient.GetStringAsync($"http://{target}:{origin.Port}/through-wireguard") == "direct" && origin.Requests == 1, "actual encrypted WireGuard peer delivers TCP data to the work-side service");
                await client.ProbeTcpAsync(target.ToString(), origin.Port);
                await WaitUntil(() => origin.Connections >= 2, TimeSpan.FromSeconds(2));
                Assert(origin.Connections >= 2, "work diagnostics perform an actual TCP connection across WireGuard");
                var rejected = false;
                try { await client.ProbeTcpAsync("127.0.0.1", origin.Port); } catch (IOException) { rejected = true; }
                Assert(rejected && origin.Connections == 2, "diagnostics cannot fall back directly to an available service outside work routes");
                var traffic = await client.GetTrafficAsync(); Assert(traffic.Uploaded > 0 && traffic.Downloaded > 0, "WireGuard traffic is reflected in application counters");
            }
            for (var restart = 0; restart < 3; restart++) {
                await using var restarted = await CoreSession.StartAsync(settings, "127.0.0.1", false);
                Assert(await restarted.ProxyClient.GetStringAsync($"http://{target}:{origin.Port}/recreated-peer") == "direct",
                    "a recreated WireGuard client re-establishes authenticated access with the same keys, restart=" + restart);
            }
            var deliveredBeforeFailure = origin.Connections;
            settings.WorkNetwork!.PublicKey = (await GenerateWireGuardKeys(exe)).Public;
            await using (var wrongPeer = await CoreSession.StartAsync(settings, "127.0.0.1", false)) {
                var failed = false;
                try { await wrongPeer.ProbeTcpAsync(target.ToString(), origin.Port); }
                catch (Exception e) when (e is IOException or OperationCanceledException) { failed = true; }
                Assert(failed && origin.Connections == deliveredBeforeFailure, "an unreachable or incorrect WireGuard peer never reports a healthy work connection");
            }
        }
        finally { await server.StopAsync(); SecureStorage.Delete(path); SecureStorage.Delete(log); }
        Assert(!IsAlive(server.ProcessId), "WireGuard server fixture terminates without persistent network routes");
    }
}
