using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Models;

public sealed class WorkNetworkProfile
{
    public string PrivateKey { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string PreSharedKey { get; set; } = "";
    public List<string> Addresses { get; set; } = new();
    public List<string> AllowedIps { get; set; } = new();
    public List<string> Routes { get; set; } = new();
    public string ProbeHost { get; set; } = "";
    public int ProbePort { get; set; } = 80;
    public int Keepalive { get; set; } = 25;
    public int Mtu { get; set; } = 1408;
    public string SourceName { get; set; } = "";
    public WorkNetworkProfile Copy() => JsonSerializer.Deserialize<WorkNetworkProfile>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        ValidateKey(PrivateKey); ValidateKey(PublicKey);
        if (PreSharedKey.Length > 0) ValidateKey(PreSharedKey);
        if (Addresses.Count == 0 || AllowedIps.Count == 0 || Routes.Count == 0)
            throw new ArgumentException(L.T("WorkRoutesRequired"));
        foreach (var address in Addresses) IpNetwork.Parse(address);
        AllowedIps = AllowedIps.Select(n => IpNetwork.Parse(n).ToString()).Distinct().ToList();
        Routes = Routes.Select(n => IpNetwork.Parse(n).ToString()).Distinct().ToList();
        if (Routes.Any(r => IpNetwork.Parse(r).Prefix == 0)) throw new ArgumentException(L.T("WorkOnlyRoutes"));
        foreach (var route in Routes) {
            var network = IpNetwork.Parse(route);
            if (!AllowedIps.Any(a => IpNetwork.Parse(a).Contains(network)))
                throw new ArgumentException(L.F("RouteNotAllowed", route));
        }
        if (Mtu is < 576 or > 9000 || Keepalive is < 0 or > 65535)
            throw new ArgumentException(L.T("InvalidWorkOptions"));
        if (ProbePort is < 1 or > 65535 || !IPAddress.TryParse(ProbeHost, out var probe))
            throw new ArgumentException(L.T("InvalidProbe"));
        if (!Routes.Any(r => IpNetwork.Parse(r).Contains(probe))) throw new ArgumentException(L.T("ProbeOutsideWork"));
    }
    public static void ValidateKey(string value)
    {
        try { if (Convert.FromBase64String(value).Length == 32) return; }
        catch (FormatException) { }
        throw new ArgumentException(L.T("InvalidWireGuardKey"));
    }
}

public sealed class IpNetwork
{
    public IPAddress Address { get; }
    public int Prefix { get; }
    private IpNetwork(IPAddress address, int prefix) { Address = Mask(address, prefix); Prefix = prefix; }
    public static IpNetwork Parse(string value)
    {
        var parts = value.Trim().Split('/');
        if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var ip)) throw new ArgumentException(L.T("InvalidNetwork"));
        var bits = ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefix = bits;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out prefix) || prefix < 0 || prefix > bits))
            throw new ArgumentException(L.T("InvalidNetwork"));
        if (ip.IsIPv4MappedToIPv6) throw new ArgumentException(L.T("InvalidNetwork"));
        return new(ip, prefix);
    }
    public bool Contains(IPAddress ip) => ip.AddressFamily == Address.AddressFamily && Mask(ip, Prefix).Equals(Address);
    public bool Contains(IpNetwork network) => network.Address.AddressFamily == Address.AddressFamily && Prefix <= network.Prefix && Contains(network.Address);
    public bool Overlaps(IpNetwork network) => Contains(network.Address) || network.Contains(Address);
    public IPAddress FirstHost() {
        var bytes = Address.GetAddressBytes();
        if (Prefix < bytes.Length * 8) for (var i = bytes.Length - 1; i >= 0; i--) { if (++bytes[i] != 0) break; }
        return new(bytes);
    }
    private static IPAddress Mask(IPAddress ip, int prefix) {
        var bytes = ip.GetAddressBytes();
        for (var bit = prefix; bit < bytes.Length * 8; bit++) bytes[bit / 8] &= (byte)~(1 << (7 - bit % 8));
        return new(bytes);
    }
    public override string ToString() => Address + "/" + Prefix;
}

public sealed class RemoteComputer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3389;
    public string Username { get; set; } = "";
    public string Endpoint => (Host.Contains(':') ? "[" + Host + "]" : Host) + ":" + Port;
    public void Validate() {
        Name = Name.Trim();
        if (Name.Length is < 1 or > 80 || Name.Any(char.IsControl)) throw new ArgumentException(L.T("InvalidComputerName"));
        if (!IPAddress.TryParse(Host.Trim(), out var ip) || IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            throw new ArgumentException(L.T("ComputerIpRequired"));
        Host = ip.ToString(); Username = Username.Trim();
        if (Port is < 1 or > 65535) throw new ArgumentException(L.T("InvalidPort"));
        if (Username.Length > 256 || Username.Any(char.IsControl)) throw new ArgumentException(L.T("InvalidCredentials"));
    }
}
