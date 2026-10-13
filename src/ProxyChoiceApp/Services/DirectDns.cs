using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ProxyChoiceApp.Services;

public static class DirectDns
{
    // The tunnel's DNS is our own inbound and must never become the upstream for an exception.
    public static string? FindServer()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel &&
                    !n.Name.Equals("ProxyChoiceApp", StringComparison.OrdinalIgnoreCase) &&
                    !n.Name.Equals("ProxyFlow", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.GetIPProperties())
                .Where(p => p.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any)))
                .SelectMany(p => p.DnsAddresses)
                .Where(ip => !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any) &&
                    !ip.Equals(IPAddress.Parse("172.31.255.2")) && !ip.Equals(IPAddress.Parse("172.30.254.2")))
                .OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .Select(ip => ip.ToString()).FirstOrDefault();
        }
        catch (NetworkInformationException) { return null; }
    }
}
