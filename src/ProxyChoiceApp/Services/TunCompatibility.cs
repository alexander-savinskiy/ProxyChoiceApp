using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ProxyChoiceApp.Localization;
using ProxyChoiceApp.Models;

namespace ProxyChoiceApp.Services;

internal static class TunCompatibility
{
    internal static async Task<T> StartAsync<T>(ProxySettings settings, bool tunnel,
        Func<bool, CancellationToken, Task<T>> start, Action<string>? diagnostic, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await start(true, token); }
        catch (CoreStartupException error) when (tunnel && error.Tunnel &&
            error.Message.Contains("set ipv6 address:", StringComparison.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            // Explicit IPv6 work routes must never disappear during a compatibility retry.
            if (settings.Mode == ConnectionMode.WorkNetwork && settings.WorkNetwork!.Routes
                .Any(route => IpNetwork.Parse(route).Address.AddressFamily == AddressFamily.InterNetworkV6))
                throw new CoreStartupException(L.T("Ipv6WorkRequired") + " " + error.Message, true);
            diagnostic?.Invoke(L.T("Ipv4FallbackLog"));
            token.ThrowIfCancellationRequested();
            // The caller disposes the failed session before returning its error. Retry once only.
            return await start(false, token);
        }
    }
}
