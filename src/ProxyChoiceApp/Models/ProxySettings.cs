using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Models;

public enum ProxyProtocol { Socks5, Http, Https }
public enum ConnectionMode { Proxy, WorkNetwork }

public sealed class ProxySettings
{
    public ConnectionMode Mode { get; set; }
    public WorkNetworkProfile? WorkNetwork { get; set; }
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Socks5;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1080;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool RememberPassword { get; set; } = true;
    public bool ProxyStore { get; set; } = true;
    public bool EnableUdp { get; set; }
    public string Language { get; set; } = "ru";
    public bool DefaultsInitialized { get; set; }
    public List<string> ProxyApps { get; set; } = new();

    public ProxySettings Copy() => JsonSerializer.Deserialize<ProxySettings>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        Host = InputRules.Host(Host);
        if (!Enum.IsDefined(Mode)) throw new ArgumentException(L.T("InvalidMode"));
        if (Mode == ConnectionMode.WorkNetwork) {
            if (WorkNetwork is null) throw new ArgumentException(L.T("ImportWorkFirst"));
            if (Port is < 1 or > 65535) throw new ArgumentException(L.T("InvalidPort"));
            WorkNetwork.Validate(); return;
        }
        if (!Enum.IsDefined(Protocol)) throw new ArgumentException(L.T("InvalidProtocol"));
        if (Port is < 1 or > 65535) throw new ArgumentException(L.T("InvalidPort"));
        if (Password.Length > 0 && Username.Length == 0)
            throw new ArgumentException(L.T("PasswordNeedsUser"));
        if (Protocol == ProxyProtocol.Socks5 &&
            (Encoding.UTF8.GetByteCount(Username) > 255 || Encoding.UTF8.GetByteCount(Password) > 255))
            throw new ArgumentException(L.T("SocksLimit"));
        if (Username.Any(char.IsControl) || Password.Any(char.IsControl))
            throw new ArgumentException(L.T("InvalidCredentials"));
        ProxyApps = ProxyApps.Select(InputRules.App).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed record SiteEntry(string Value, bool IsIp);

public static class InputRules
{
    public static string Host(string input)
    {
        var value = input.Trim().Trim('[', ']');
        if (IPAddress.TryParse(value, out var ip)) return ip.ToString();
        if (value.Length == 0) throw new ArgumentException(L.T("HostRequired"));
        if (value.Contains('/') || value.Contains(':') || value.Contains('@'))
            throw new ArgumentException(L.T("HostOnly"));
        return Domain(value);
    }

    public static SiteEntry Site(string input)
    {
        var value = input.Trim();
        if (value.Length == 0) throw new ArgumentException("Введите адрес сайта, IP-адрес или подсеть.");
        if (value.Contains("://"))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("Ссылка должна начинаться с http:// или https://.");
            value = uri.Host.Trim('[', ']');
        }
        if (IPAddress.TryParse(value, out var ip)) return new(ip.ToString(), true);
        if (value.Contains('/'))
        {
            var parts = value.Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out ip) ||
                !int.TryParse(parts[1], out var prefix) || prefix < 0 ||
                prefix > (ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128))
                throw new ArgumentException("Подсеть указывается в формате 198.51.100.0/24 или IPv6/64.");
            var bytes = ip.GetAddressBytes();
            for (var bit = prefix; bit < bytes.Length * 8; bit++) bytes[bit / 8] &= (byte)~(1 << (7 - bit % 8));
            return new($"{new IPAddress(bytes)}/{prefix}", true);
        }
        if (value.StartsWith("*.")) value = value[2..];
        value = value.TrimStart('.');
        return new(Domain(value), false);
    }

    private static string Domain(string value)
    {
        try { value = new IdnMapping().GetAscii(value.TrimEnd('.')).ToLowerInvariant(); }
        catch (ArgumentException) { throw new ArgumentException(L.T("InvalidHost")); }
        if (value.Length > 253 || value.Length == 0 ||
            value.Split('.').Any(label => label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-' ||
                label.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-'))))
            throw new ArgumentException(L.T("InvalidHost"));
        return value;
    }

    public static string App(string input)
    {
        if (!Path.IsPathFullyQualified(input) || !string.Equals(Path.GetExtension(input), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(L.T("InvalidApp"));
        return Path.GetFullPath(input);
    }
}
