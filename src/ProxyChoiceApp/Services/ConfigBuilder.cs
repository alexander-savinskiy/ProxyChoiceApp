using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProxyChoiceApp.Models;

namespace ProxyChoiceApp.Services;

public sealed record CoreOptions(string ServerIp, int ProbePort, string ProbeUser, string ProbePassword,
    int ApiPort, string ApiSecret, string LogPath, bool Tunnel, string? DirectDnsServer = null, string? SelfPath = null);

public static class ConfigBuilder
{
    public static readonly string[] StoreDomains = {
        "displaycatalog.mp.microsoft.com", "purchase.md.mp.microsoft.com", "purchase.mp.microsoft.com",
        "licensing.mp.microsoft.com", "storeedgefd.dsx.mp.microsoft.com", "delivery.mp.microsoft.com",
        "do.dsp.mp.microsoft.com", "windowsupdate.com", "update.microsoft.com", "login.live.com", "account.live.com"
    };
    public static string PathPattern(string path) => "(?i)^" +
        Regex.Replace(path, @"[\\.\^$|?*+(){}\[\]]", m => "\\" + m.Value) + "$";

    public static string Build(ProxySettings settings, CoreOptions options)
    {
        settings = settings.Copy(); settings.Validate();
        var proxy = new Dictionary<string, object> {
            ["type"] = settings.Protocol == ProxyProtocol.Socks5 ? "socks" : "http",
            ["tag"] = "proxy", ["server"] = options.ServerIp, ["server_port"] = settings.Port, ["connect_timeout"] = "12s"
        };
        if (settings.Username.Length > 0) { proxy["username"] = settings.Username; proxy["password"] = settings.Password; }
        if (settings.Protocol == ProxyProtocol.Socks5) proxy["version"] = "5";
        if (settings.Protocol == ProxyProtocol.Https) proxy["tls"] = new { enabled = true, server_name = settings.Host };
        var inbounds = new List<object>();
        if (options.Tunnel) inbounds.Add(new {
            type = "tun", tag = "tun-in", interface_name = "ProxyChoiceApp",
            address = new[] { "172.30.254.1/30", "fdfe:6368:6f69:6365::1/126" }, mtu = 1500,
            auto_route = true, strict_route = true, dns_mode = "hijack"
        });
        inbounds.Add(new {
            type = "mixed", tag = "probe-in", listen = "127.0.0.1", listen_port = options.ProbePort,
            users = new[] { new { username = options.ProbeUser, password = options.ProbePassword } }
        });
        var rules = new List<object> {
            new { inbound = new[] { "probe-in" }, action = "route", outbound = "proxy" },
            new { action = "sniff", timeout = "500ms" },
            new { protocol = new[] { "dns" }, action = "hijack-dns" },
            new { port = new[] { 53 }, action = "hijack-dns" },
            // Routers, printers and private network resources stay reachable.
            new { ip_is_private = true, action = "route", outbound = "direct" }
        };
        var paths = settings.ProxyApps.Concat(new[] { options.SelfPath ?? Environment.ProcessPath ?? "ProxyChoiceApp.exe" })
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(PathPattern).ToArray();
        var unsupported = settings.Protocol != ProxyProtocol.Socks5 || !settings.EnableUdp
            ? new[] { "udp", "icmp" } : new[] { "icmp" };
        // No global UDP rejection: unselected apps must retain their ordinary ISP connection.
        rules.Add(new { process_path_regex = paths, network = unsupported, action = "reject" });
        rules.Add(new { process_path_regex = paths, action = "route", outbound = "proxy" });
        if (settings.ProxyStore) {
            var store = new[] { "WinStore.App.exe", "WinStore.exe" };
            rules.Add(new { process_name = store, network = unsupported, action = "reject" });
            rules.Add(new { process_name = store, action = "route", outbound = "proxy" });
            rules.Add(new { domain_suffix = StoreDomains, network = unsupported, action = "reject" });
            rules.Add(new { domain_suffix = StoreDomains, action = "route", outbound = "proxy" });
        }
        var dnsRules = new List<object>();
        if (options.Tunnel) dnsRules.Add(new {
            inbound = new[] { "tun-in" }, query_type = new[] { "A", "AAAA" }, action = "route", server = "fake-dns"
        });
        object directDns = options.DirectDnsServer is not null
            ? new { type = "udp", tag = "direct-dns", server = options.DirectDnsServer, detour = "direct" }
            : new { type = "https", tag = "direct-dns", server = "1.1.1.1", detour = "direct", tls = new { enabled = true, server_name = "cloudflare-dns.com" } };
        var servers = new List<object> {
            new { type = "https", tag = "proxy-dns", server = "1.1.1.1", detour = "proxy", tls = new { enabled = true, server_name = "cloudflare-dns.com" } }, directDns
        };
        if (options.Tunnel) servers.Add(new { type = "fakeip", tag = "fake-dns", inet4_range = "198.18.0.0/15", inet6_range = "fc00::/18" });
        return JsonSerializer.Serialize(new {
            log = new { level = "info", timestamp = true, disabled = false, output = options.LogPath },
            dns = new { servers, rules = dnsRules, final = "proxy-dns", independent_cache = true, reverse_mapping = true },
            inbounds, outbounds = new object[] { proxy, new { type = "direct", tag = "direct", domain_resolver = "direct-dns" } },
            route = new { rules, final = "direct", auto_detect_interface = options.Tunnel, find_process = options.Tunnel, default_domain_resolver = "proxy-dns" },
            experimental = new { clash_api = new { external_controller = $"127.0.0.1:{options.ApiPort}", secret = options.ApiSecret,
                access_control_allow_origin = new[] { "http://127.0.0.1" }, access_control_allow_private_network = false } }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
