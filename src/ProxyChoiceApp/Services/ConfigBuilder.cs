using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProxyChoiceApp.Models;

namespace ProxyChoiceApp.Services;

public sealed record CoreOptions(string ServerIp, int ProbePort, string ProbeUser, string ProbePassword,
    int ApiPort, string ApiSecret, string LogPath, bool Tunnel, string? DirectDnsServer = null, string? SelfPath = null, bool TunIpv6 = true);

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
        if (settings.Mode == ConnectionMode.WorkNetwork) return BuildWork(settings, options);
        var proxy = new Dictionary<string, object> {
            ["type"] = settings.Protocol == ProxyProtocol.Socks5 ? "socks" : "http",
            ["tag"] = "proxy", ["server"] = options.ServerIp, ["server_port"] = settings.Port, ["connect_timeout"] = "12s"
        };
        if (settings.Username.Length > 0) { proxy["username"] = settings.Username; proxy["password"] = settings.Password; }
        if (settings.Protocol == ProxyProtocol.Socks5) proxy["version"] = "5";
        if (settings.Protocol == ProxyProtocol.Https) proxy["tls"] = new { enabled = true, server_name = settings.Host };
        var inbounds = new List<object>();
        if (options.Tunnel) inbounds.Add(new {
            type = "tun", tag = "tun-in", interface_name = "ProxyChoicePro",
            address = TunAddresses(options), mtu = 1500,
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
        var paths = settings.ProxyApps.Concat(new[] { options.SelfPath ?? Environment.ProcessPath ?? "ProxyChoicePro.exe" })
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
            inbound = new[] { "tun-in" }, query_type = options.TunIpv6 ? new[] { "A", "AAAA" } : new[] { "A" }, action = "route", server = "fake-dns"
        });
        object directDns = options.DirectDnsServer is not null
            ? new { type = "udp", tag = "direct-dns", server = options.DirectDnsServer, detour = "direct" }
            : new { type = "https", tag = "direct-dns", server = "1.1.1.1", detour = "direct", tls = new { enabled = true, server_name = "cloudflare-dns.com" } };
        var servers = new List<object> {
            new { type = "https", tag = "proxy-dns", server = "1.1.1.1", detour = "proxy", tls = new { enabled = true, server_name = "cloudflare-dns.com" } }, directDns
        };
        if (options.Tunnel) {
            var fakeDns = new Dictionary<string, object> { ["type"] = "fakeip", ["tag"] = "fake-dns", ["inet4_range"] = "198.18.0.0/15" };
            if (options.TunIpv6) fakeDns["inet6_range"] = "fc00::/18";
            servers.Add(fakeDns);
        }
        var dns = new Dictionary<string, object> { ["servers"] = servers, ["rules"] = dnsRules,
            ["final"] = "proxy-dns", ["independent_cache"] = true, ["reverse_mapping"] = true };
        if (!options.TunIpv6) dns["strategy"] = "ipv4_only";
        return JsonSerializer.Serialize(new {
            log = new { level = "info", timestamp = true, disabled = false, output = options.LogPath },
            dns,
            inbounds, outbounds = new object[] { proxy, new { type = "direct", tag = "direct", domain_resolver = "direct-dns" } },
            route = new { rules, final = "direct", auto_detect_interface = options.Tunnel, find_process = options.Tunnel, default_domain_resolver = "proxy-dns" },
            experimental = new { clash_api = new { external_controller = $"127.0.0.1:{options.ApiPort}", secret = options.ApiSecret,
                access_control_allow_origin = new[] { "http://127.0.0.1" }, access_control_allow_private_network = false } }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string[] TunAddresses(CoreOptions options) => options.TunIpv6
        ? new[] { "172.30.254.1/30", "fdfe:6368:6f69:6365::1/126" } : new[] { "172.30.254.1/30" };

    private static string BuildWork(ProxySettings settings, CoreOptions options)
    {
        var work = settings.WorkNetwork!;
        var peer = new Dictionary<string, object> {
            ["address"] = options.ServerIp, ["port"] = settings.Port, ["public_key"] = work.PublicKey,
            ["allowed_ips"] = work.AllowedIps.ToArray(), ["persistent_keepalive_interval"] = work.Keepalive
        };
        if (work.PreSharedKey.Length > 0) peer["pre_shared_key"] = work.PreSharedKey;
        var endpoint = new {
            type = "wireguard", tag = "work", system = false, mtu = work.Mtu,
            address = work.Addresses.ToArray(), private_key = work.PrivateKey, peers = new[] { peer }
        };
        var inbounds = new List<object>();
        if (options.Tunnel) inbounds.Add(new {
            type = "tun", tag = "tun-in", interface_name = "ProxyChoicePro",
            address = TunAddresses(options), mtu = work.Mtu,
            auto_route = true, route_address = work.Routes.ToArray(),
            route_exclude_address = new[] { options.ServerIp + (options.ServerIp.Contains(':') ? "/128" : "/32") },
            strict_route = false, dns_mode = "disabled"
        });
        inbounds.Add(new {
            type = "mixed", tag = "probe-in", listen = "127.0.0.1", listen_port = options.ProbePort,
            users = new[] { new { username = options.ProbeUser, password = options.ProbePassword } }
        });
        // Work routes precede private-address bypass. Probes cannot silently fall back to the home LAN.
        var rules = new object[] {
            new { ip_cidr = work.Routes.ToArray(), action = "route", outbound = "work" },
            new { inbound = new[] { "probe-in" }, action = "reject" }
        };
        return JsonSerializer.Serialize(new {
            log = new { level = "info", timestamp = true, output = options.LogPath },
            dns = new { servers = new[] { new { type = "local", tag = "local-dns" } }, final = "local-dns" },
            endpoints = new[] { endpoint }, inbounds,
            outbounds = new[] { new { type = "direct", tag = "direct", domain_resolver = "local-dns" } },
            // Keep WireGuard transport on the same OS route as the successful userspace preflight.
            // Default-interface binding can break its IPv6 socket on IPv4-only Windows adapters.
            // The resolved peer host route above prevents transport packets from re-entering TUN.
            route = new { rules, final = "direct", auto_detect_interface = false, default_domain_resolver = "local-dns" },
            experimental = new { clash_api = new {
                external_controller = $"127.0.0.1:{options.ApiPort}", secret = options.ApiSecret,
                access_control_allow_origin = new[] { "http://127.0.0.1" }, access_control_allow_private_network = false
            } }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
