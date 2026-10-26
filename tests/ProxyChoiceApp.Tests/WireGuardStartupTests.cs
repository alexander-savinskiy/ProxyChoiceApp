using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;

namespace ProxyChoiceApp.Tests;

internal static partial class Program
{
    private static async Task CheckWorkTransportRoutes(string engine)
    {
        var work = WorkSettings();
        work.WorkNetwork!.Routes = new() { "198.51.100.85/32" };
        work.WorkNetwork.ProbeHost = "198.51.100.85";
        foreach (var server in new[] { "198.51.100.24", "2001:db8::24" })
        foreach (var ipv6 in new[] { true, false }) {
            var path = Path.Combine(_root, "work-transport.json");
            var options = new CoreOptions(server, CoreSession.FreePort(), "probe", "secret", CoreSession.FreePort(),
                "api-secret", path + ".log", true, TunIpv6: ipv6);
            var json = JsonNode.Parse(ConfigBuilder.Build(work, options))!;
            File.WriteAllText(path, json.ToJsonString());
            try { await CoreSession.CheckConfigAsync(engine, path); }
            finally { SecureStorage.Delete(path); }
            var excluded = server + (server.Contains(':') ? "/128" : "/32");
            Assert(!json["route"]!["auto_detect_interface"]!.GetValue<bool>() &&
                json["inbounds"]![0]!["route_exclude_address"]!.AsArray().Single()!.ToString() == excluded &&
                json["endpoints"]![0]!["peers"]![0]!["address"]!.ToString() == server,
                "real engine accepts transport peer exclusion without default-adapter socket binding: " + server + ", TUN IPv6=" + ipv6);
            Assert(json["inbounds"]![0]!["route_address"]!.AsArray().Single()!.ToString() == "198.51.100.85/32" &&
                json["route"]!["rules"]![0]!["outbound"]!.ToString() == "work" &&
                json["endpoints"]![0]!["address"]!.AsArray().Any(a => a!.ToString() == "192.0.2.2/24"),
                "transport exclusion preserves the selected work route and authenticated WireGuard client address");
        }
    }

    private static async Task CheckWorkProbeDeadlines()
    {
        const string target = "198.51.100.85";
        const int targetPort = 3389;
        foreach (var stallAfterAuthentication in new[] { false, true }) {
            var closed = false;
            await WithProbeServerAsync(async (stream, token) => {
                if (stallAfterAuthentication) await ReadProbeRequestAsync(stream, token);
                else await stream.ReadExactlyAsync(new byte[3], token);
                // Leave a partial CONNECT response, or no method response, until the client times out.
                if (stallAfterAuthentication) await stream.WriteAsync(new byte[] { 5, 0 }, token);
                closed = await ProbeClientClosedAsync(stream, token);
            }, async port => {
                var watch = Stopwatch.StartNew();
                try {
                    await NetworkDiagnostics.ProbeSocksAsync(port, "probe", "secret", target, targetPort,
                        CancellationToken.None, TimeSpan.FromMilliseconds(500));
                    throw new Exception("stalled probe did not time out");
                }
                catch (IOException error) {
                    Assert(error.InnerException is OperationCanceledException && error.Message.Contains(target) &&
                        error.Message.Contains(targetPort.ToString()) && watch.Elapsed < TimeSpan.FromSeconds(3),
                        "a stalled SOCKS check reports its destination and bounded timeout, phase=" + stallAfterAuthentication);
                }
            });
            Assert(closed, "a timed-out work probe closes its TCP connection without retaining a stalled process");
        }
        var requestReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        await WithProbeServerAsync(async (stream, token) => {
            await ReadProbeRequestAsync(stream, token); requestReady.TrySetResult();
            if (!await ProbeClientClosedAsync(stream, token)) throw new IOException("Unexpected data after CONNECT.");
        }, async port => {
            var probe = NetworkDiagnostics.ProbeSocksAsync(port, "probe", "secret", target, targetPort,
                cancellation.Token, TimeSpan.FromSeconds(4));
            await requestReady.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            try { await probe.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("manual probe cancellation was ignored"); }
            catch (OperationCanceledException) { Assert(true, "manual disconnect cancels an active work check without presenting a network timeout"); }
        });
        foreach (var code in new byte[] { 0, 5 }) {
            await WithProbeServerAsync(async (stream, token) => {
                var address = await ReadProbeRequestAsync(stream, token);
                if (address != target + ":" + targetPort) throw new IOException("Probe requested a different destination.");
                await stream.WriteAsync(new byte[] { 5, code, 0, 1, 0, 0, 0, 0, 0, 0 }, token);
                if (!await ProbeClientClosedAsync(stream, token)) throw new IOException("Unexpected data after CONNECT.");
            }, async port => {
                try {
                    await NetworkDiagnostics.ProbeSocksAsync(port, "probe", "secret", target, targetPort,
                        CancellationToken.None, TimeSpan.FromSeconds(2));
                    Assert(code == 0, "an authenticated successful CONNECT is required to confirm work access");
                }
                catch (IOException error) when (code != 0) {
                    Assert(error.Message.Contains(target) && error.Message.Contains(targetPort.ToString()) &&
                        error.InnerException is not OperationCanceledException,
                        "a refused work port reports the actual destination without claiming a timeout");
                }
            });
        }
    }

    private static async Task<bool> ProbeClientClosedAsync(NetworkStream stream, CancellationToken token)
    {
        try { return await stream.ReadAsync(new byte[1], token) == 0; }
        // Windows may send RST when the client rejects a SOCKS reply before reading its bound address.
        catch (IOException error) when (error.InnerException is SocketException socket &&
            socket.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted) { return true; }
    }

    private static async Task<string> ReadProbeRequestAsync(NetworkStream stream, CancellationToken token)
    {
        await stream.ReadExactlyAsync(new byte[3], token);
        await stream.WriteAsync(new byte[] { 5, 2 }, token);
        var auth = new byte[2]; await stream.ReadExactlyAsync(auth, token);
        var user = new byte[auth[1] + 1]; await stream.ReadExactlyAsync(user, token);
        await stream.ReadExactlyAsync(new byte[user[^1]], token);
        await stream.WriteAsync(new byte[] { 1, 0 }, token);
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token);
        if (header[3] != 1) throw new IOException("IPv4 probe fixture expected.");
        var address = new byte[6]; await stream.ReadExactlyAsync(address, token);
        return new IPAddress(address[..4]) + ":" + ((address[4] << 8) | address[5]);
    }

    private static async Task WithProbeServerAsync(Func<NetworkStream, CancellationToken, Task> respond, Func<int, Task> check)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server = Task.Run(async () => {
            using var client = await listener.AcceptTcpClientAsync(limit.Token);
            using var stream = client.GetStream(); await respond(stream, limit.Token);
        });
        try {
            await check(((IPEndPoint)listener.LocalEndpoint).Port);
            await server.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally {
            limit.Cancel(); listener.Stop();
            try { await server.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (Exception error) when (error is OperationCanceledException or IOException or SocketException) { }
        }
    }
}
