using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProxyChoiceApp.Localization;
using ProxyChoiceApp.Models;
using ProxyChoiceApp.Services;

namespace ProxyChoiceApp.Tests;

internal static partial class Program
{
    private static void TestPublicPrivacyDefaults()
    {
        var profiles = new ProxyProfiles(); profiles.Normalize();
        Assert(profiles.Profiles.All(p => p.Settings.Host.Length == 0 && p.Settings.Username.Length == 0 && p.Settings.Password.Length == 0),
            "a clean public installation has no configured proxy address or credentials");
        Assert(profiles.Profiles.All(p => p.Settings.Mode == ConnectionMode.Proxy && p.Settings.WorkNetwork is null) && profiles.RemoteComputers.Count == 0,
            "a clean public installation has no WireGuard profile, VPN keys or saved work computer");
        using var resource = typeof(WireGuardImporter).Assembly.GetManifestResourceStream("ProxyChoiceApp.import-template.conf")!;
        using var reader = new StreamReader(resource); var template = reader.ReadToEnd();
        Assert(!Regex.IsMatch(template, @"\b(?:\d{1,3}\.){3}\d{1,3}\b") &&
            !Regex.IsMatch(template, @"[A-Za-z0-9+/]{43}="),
            "the actual embedded import template contains no IP address or encoded WireGuard key");
        var fields = template.Split('\n').Select(l => l.Trim()).Where(l => l.Contains('=') && !l.StartsWith('#'))
            .Select(l => l.Split('=', 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        Assert(new[] { "PrivateKey", "Address", "PublicKey", "AllowedIPs", "Endpoint" }.All(k => fields[k].StartsWith("REPLACE_WITH_", StringComparison.Ordinal)),
            "every imported connection and identity field is an explicit placeholder");
        var language = L.Current.Code;
        try {
            foreach (var option in L.Supported) {
                L.Current.Set(option.Code);
                try { WireGuardImporter.Parse(template); throw new Exception("An unfilled public template was accepted."); }
                catch (ArgumentException error) { Assert(error.Message == L.T("ImportTemplatePlaceholder"),
                    "unfilled import templates are rejected with a localized explanation: " + option.Code); }
            }
            var pendingEndpoint = ExampleProfile.Replace("Endpoint = vpn.example.invalid:51820", "Endpoint = REPLACE_WITH_VPN_SERVER_HOST:REPLACE_WITH_VPN_UDP_PORT");
            try { WireGuardImporter.Parse(pendingEndpoint); throw new Exception("A partially filled endpoint was accepted."); }
            catch (ArgumentException error) { Assert(error.Message == L.T("ImportTemplatePlaceholder"),
                "partially filled templates cannot retain a placeholder endpoint"); }
        }
        finally { L.Current.Set(language); }
    }
}
