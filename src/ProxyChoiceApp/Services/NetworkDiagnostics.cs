using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Localization;
using ProxyChoiceApp.Models;

namespace ProxyChoiceApp.Services;

public sealed record SubnetConflict(string WorkNetwork, string LocalNetwork, string Adapter);

public static class NetworkDiagnostics
{
    public static IReadOnlyList<SubnetConflict> FindConflicts(IEnumerable<string> routes)
    {
        var locals = new List<(string Network, string Adapter)>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                adapter.Name.StartsWith("ProxyChoice", StringComparison.OrdinalIgnoreCase)) continue;
            try {
                foreach (var address in adapter.GetIPProperties().UnicastAddresses) {
                    if (IPAddress.IsLoopback(address.Address) || address.Address.IsIPv6LinkLocal || address.Address.IsIPv6Multicast) continue;
                    locals.Add((address.Address + "/" + address.PrefixLength, adapter.Name));
                }
            } catch (NetworkInformationException) { }
        }
        return FindConflicts(routes, locals);
    }
    public static IReadOnlyList<SubnetConflict> FindConflicts(IEnumerable<string> routes, IEnumerable<(string Network, string Adapter)> locals) {
        var result = new List<SubnetConflict>();
        foreach (var route in routes) foreach (var local in locals) {
            var remote = IpNetwork.Parse(route); var home = IpNetwork.Parse(local.Network);
            if (remote.Overlaps(home)) result.Add(new(remote.ToString(), home.ToString(), local.Adapter));
        }
        return result.Distinct().ToArray();
    }
    public static bool InsideWork(WorkNetworkProfile profile, string host) => IPAddress.TryParse(host, out var ip) && profile.Routes.Any(r => IpNetwork.Parse(r).Contains(ip));
    public static string RdpDocument(RemoteComputer computer) {
        computer.Validate();
        return "full address:s:" + computer.Endpoint + "\r\nusername:s:" + computer.Username +
            "\r\nprompt for credentials:i:1\r\nenablecredsspsupport:i:1\r\nauthentication level:i:2\r\n";
    }
    public static void OpenRdp(RemoteComputer computer) {
        computer.Validate();
        var folder = Path.Combine(SecureStorage.Root, "rdp"); SecureStorage.EnsurePrivateDirectory(folder);
        var path = Path.Combine(folder, computer.Id.ToString("N") + ".rdp");
        File.WriteAllText(path, RdpDocument(computer), Encoding.Unicode);
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mstsc.exe")) { UseShellExecute = false };
        start.ArgumentList.Add(path); Process.Start(start);
    }
    internal static async Task ProbeSocksAsync(int port, string username, string password, string host, int targetPort,
        CancellationToken token, TimeSpan? timeout = null)
    {
        var duration = timeout ?? TimeSpan.FromSeconds(7);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(duration);
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, limit.Token);
            using var stream = client.GetStream();
            await stream.WriteAsync(new byte[] { 5, 1, 2 }, limit.Token);
            var reply = new byte[2]; await stream.ReadExactlyAsync(reply, limit.Token);
            if (reply[0] != 5 || reply[1] != 2) throw new IOException(L.T("WorkProbeFailed"));
            var user = Encoding.UTF8.GetBytes(username); var pass = Encoding.UTF8.GetBytes(password);
            var auth = new byte[3 + user.Length + pass.Length]; auth[0] = 1; auth[1] = (byte)user.Length;
            user.CopyTo(auth, 2); auth[2 + user.Length] = (byte)pass.Length; pass.CopyTo(auth, 3 + user.Length);
            await stream.WriteAsync(auth, limit.Token); await stream.ReadExactlyAsync(reply, limit.Token);
            if (reply[0] != 1 || reply[1] != 0) throw new IOException(L.T("WorkProbeFailed"));
            byte[] address;
            byte addressType;
            if (IPAddress.TryParse(host, out var ip)) { address = ip.GetAddressBytes(); addressType = address.Length == 4 ? (byte)1 : (byte)4; }
            else { var domain = Encoding.ASCII.GetBytes(InputRules.Host(host)); address = new byte[domain.Length + 1]; address[0] = (byte)domain.Length; domain.CopyTo(address, 1); addressType = 3; }
            var request = new byte[6 + address.Length]; request[0] = 5; request[1] = 1; request[3] = addressType;
            address.CopyTo(request, 4); request[^2] = (byte)(targetPort >> 8); request[^1] = (byte)targetPort;
            await stream.WriteAsync(request, limit.Token);
            var header = new byte[4]; await stream.ReadExactlyAsync(header, limit.Token);
            if (header[0] != 5 || header[1] != 0) throw new IOException(L.F("PortUnavailable", host, targetPort));
            var length = header[3] == 1 ? 4 : header[3] == 4 ? 16 : -1;
            if (header[3] == 3) { var count = new byte[1]; await stream.ReadExactlyAsync(count, limit.Token); length = count[0]; }
            if (length < 0) throw new IOException(L.T("WorkProbeFailed"));
            await stream.ReadExactlyAsync(new byte[length + 2], limit.Token);
        }
        catch (OperationCanceledException error) when (limit.IsCancellationRequested && !token.IsCancellationRequested) {
            throw new IOException(L.F("WorkProbeTimeout", host, targetPort, duration.TotalSeconds.ToString("0.##")), error);
        }
    }
}
