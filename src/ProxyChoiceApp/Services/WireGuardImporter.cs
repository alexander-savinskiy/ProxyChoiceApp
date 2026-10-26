using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Services;

public sealed record ImportedWorkProfile(string Host, int Port, WorkNetworkProfile Profile);

public static class WireGuardImporter
{
    public static ImportedWorkProfile Parse(string text, string sourceName = "")
    {
        if (text.Length > 131072) throw new ArgumentException(L.T("ProfileTooLarge"));
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var original in text.Split('\n')) {
            var line = original.Trim().TrimStart('\uFEFF');
            if (line.StartsWith("# ProxyChoicePro.", StringComparison.OrdinalIgnoreCase)) {
                var splitMeta = line.IndexOf('=');
                if (splitMeta >= 0 && line[(splitMeta + 1)..].Contains("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(L.T("ImportTemplatePlaceholder"));
                var name = splitMeta < 0 ? "" : line[17..splitMeta].Trim();
                if (!(name.Equals("Routes", StringComparison.OrdinalIgnoreCase) || name.Equals("ProbeHost", StringComparison.OrdinalIgnoreCase) || name.Equals("ProbePort", StringComparison.OrdinalIgnoreCase)) ||
                    !metadata.TryAdd(name, line[(splitMeta + 1)..].Trim())) throw new ArgumentException(L.T("InvalidWorkOptions"));
                continue;
            }
            var comment = line.IndexOf('#'); if (comment >= 0) line = line[..comment].Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) {
                var name = line[1..^1].Trim();
                if (sections.ContainsKey(name) || !(name.Equals("Interface", StringComparison.OrdinalIgnoreCase) || name.Equals("Peer", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException(L.T("OnePeerOnly"));
                sections[name] = current = new(StringComparer.OrdinalIgnoreCase); continue;
            }
            var split = line.IndexOf('=');
            if (current is null || split <= 0) throw new ArgumentException(L.T("InvalidWorkProfile"));
            if (line[(split + 1)..].Contains("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(L.T("ImportTemplatePlaceholder"));
            var key = line[..split].Trim();
            if (new[] { "PreUp", "PostUp", "PreDown", "PostDown", "SaveConfig" }.Contains(key, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(L.T("ProfileScriptsNotAllowed"));
            if (!current.TryAdd(key, line[(split + 1)..].Trim())) throw new ArgumentException(L.T("InvalidWorkProfile"));
        }
        if (!sections.TryGetValue("Interface", out var iface) || !sections.TryGetValue("Peer", out var peer))
            throw new ArgumentException(L.T("InvalidWorkProfile"));
        string Required(Dictionary<string, string> values, string name) => values.TryGetValue(name, out var value) && value.Length > 0 ? value : throw new ArgumentException(L.T("InvalidWorkProfile"));
        var endpoint = Required(peer, "Endpoint");
        string host; int port;
        if (endpoint.StartsWith('[')) {
            var end = endpoint.IndexOf(']');
            if (end < 0 || endpoint.Length < end + 3 || endpoint[end + 1] != ':' || !int.TryParse(endpoint[(end + 2)..], out port))
                throw new ArgumentException(L.T("InvalidEndpoint"));
            host = endpoint[1..end];
        } else {
            var colon = endpoint.LastIndexOf(':');
            if (colon < 1 || !int.TryParse(endpoint[(colon + 1)..], out port)) throw new ArgumentException(L.T("InvalidEndpoint"));
            host = endpoint[..colon];
        }
        host = InputRules.Host(host);
        if (port is < 1 or > 65535) throw new ArgumentException(L.T("InvalidPort"));
        var profile = new WorkNetworkProfile {
            PrivateKey = Required(iface, "PrivateKey"), PublicKey = Required(peer, "PublicKey"),
            PreSharedKey = peer.GetValueOrDefault("PresharedKey", ""), SourceName = sourceName,
            Addresses = Split(Required(iface, "Address")), AllowedIps = Split(Required(peer, "AllowedIPs"))
        };
        WorkNetworkProfile.ValidateKey(profile.PrivateKey); WorkNetworkProfile.ValidateKey(profile.PublicKey);
        if (profile.PreSharedKey.Length > 0) WorkNetworkProfile.ValidateKey(profile.PreSharedKey);
        foreach (var address in profile.Addresses) IpNetwork.Parse(address);
        foreach (var address in profile.AllowedIps) IpNetwork.Parse(address);
        if (iface.TryGetValue("MTU", out var mtu)) { if (!int.TryParse(mtu, out var number)) throw new ArgumentException(L.T("InvalidWorkOptions")); profile.Mtu = number; }
        if (peer.TryGetValue("PersistentKeepalive", out var keepalive)) { if (!int.TryParse(keepalive, out var number)) throw new ArgumentException(L.T("InvalidWorkOptions")); profile.Keepalive = number; }
        profile.Routes = profile.AllowedIps.Where(r => IpNetwork.Parse(r).Prefix > 0).Select(r => IpNetwork.Parse(r).ToString()).Distinct().ToList();
        // Full-tunnel configs need an explicit work subnet; do not silently redirect all Internet traffic.
        var networks = profile.Routes.Select(IpNetwork.Parse).ToArray();
        var network = networks.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? networks.FirstOrDefault();
        profile.ProbeHost = network?.FirstHost().ToString() ?? "";
        if (metadata.TryGetValue("Routes", out var routes)) profile.Routes = Split(routes);
        if (metadata.TryGetValue("ProbeHost", out var probeHost)) profile.ProbeHost = probeHost;
        if (metadata.TryGetValue("ProbePort", out var probePort)) {
            if (!int.TryParse(probePort, out var number) || number is < 1 or > 65535) throw new ArgumentException(L.T("InvalidProbe"));
            profile.ProbePort = number;
        }
        if (metadata.Count > 0) profile.Validate();
        return new(host, port, profile);
    }
    public static List<string> Split(string text) => text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
