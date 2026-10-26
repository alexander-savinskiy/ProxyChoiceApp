using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Models;

public sealed class ProxyProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Proxy 1";
    public ProxySettings Settings { get; set; } = new();
}

public sealed class ProxyProfiles
{
    public List<ProxyProfile> Profiles { get; set; } = new() { new() };
    public Guid SelectedProfileId { get; set; }
    public bool ProxyStore { get; set; } = true;
    public bool EnableUdp { get; set; }
    public string Language { get; set; } = "ru";
    public bool DarkMode { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public List<RemoteComputer> RemoteComputers { get; set; } = new();
    public bool DefaultsInitialized { get; set; }
    public List<string> ProxyApps { get; set; } = new();

    [JsonIgnore]
    public ProxyProfile Selected => Profiles.First(p => p.Id == SelectedProfileId);

    public ProxyProfiles() => SelectedProfileId = Profiles[0].Id;

    public ProxyProfiles Copy() => JsonSerializer.Deserialize<ProxyProfiles>(JsonSerializer.Serialize(this))!;

    public void Normalize()
    {
        Profiles ??= new();
        if (Profiles.Count == 0) Profiles.Add(new());
        ProxyApps ??= new();
        RemoteComputers ??= new();
        if (!L.Supported.Any(l => l.Code == Language)) Language = "ru";
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            if (profile is null) throw new JsonException(L.T("EmptyProfile"));
            if (profile.Id == Guid.Empty || !ids.Add(profile.Id)) { profile.Id = Guid.NewGuid(); ids.Add(profile.Id); }
            var name = profile.Name?.Trim() ?? "";
            if (name.Length is < 1 or > 60 || name.Any(char.IsControl) || names.Contains(name))
            {
                var number = 1;
                do { name = "Proxy " + number++; } while (names.Contains(name));
            }
            profile.Name = name; names.Add(name);
            profile.Settings ??= new();
            profile.Settings.Host ??= ""; profile.Settings.Username ??= ""; profile.Settings.Password ??= "";
            if (!Enum.IsDefined(profile.Settings.Protocol)) profile.Settings.Protocol = ProxyProtocol.Socks5;
            if (!Enum.IsDefined(profile.Settings.Mode)) profile.Settings.Mode = ConnectionMode.Proxy;
            // Routing rules belong to the computer, not to an individual endpoint.
            profile.Settings.ProxyApps = new();
        }
        if (!Profiles.Any(p => p.Id == SelectedProfileId)) SelectedProfileId = Profiles[0].Id;
    }

    public ProxySettings SelectedSettings()
    {
        var settings = Selected.Settings.Copy();
        settings.ProxyApps = ProxyApps.ToList(); settings.ProxyStore = ProxyStore; settings.EnableUdp = EnableUdp;
        settings.Language = Language; settings.DefaultsInitialized = DefaultsInitialized;
        return settings;
    }

    public void UpdateSelected(ProxySettings settings)
    {
        var copy = settings.Copy();
        ProxyApps = copy.ProxyApps.ToList(); ProxyStore = copy.ProxyStore; EnableUdp = copy.EnableUdp;
        Language = copy.Language; DefaultsInitialized = copy.DefaultsInitialized;
        copy.ProxyApps.Clear();
        Selected.Settings = copy;
    }

    public string NextName()
    {
        var number = 1;
        while (Profiles.Any(p => p.Name.Equals("Proxy " + number, StringComparison.OrdinalIgnoreCase))) number++;
        return "Proxy " + number;
    }

    public string CheckName(string name, Guid? exceptId = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 60 || name.Any(char.IsControl))
            throw new ArgumentException(L.T("InvalidProfileName"));
        if (Profiles.Any(p => p.Id != exceptId && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException(L.T("DuplicateProfile"));
        return name;
    }

    public void Add(string name)
    {
        var profile = new ProxyProfile { Name = CheckName(name) };
        Profiles.Add(profile); SelectedProfileId = profile.Id;
    }

    public void Rename(string name) => Selected.Name = CheckName(name, SelectedProfileId);

    public void RemoveSelected()
    {
        if (Profiles.Count <= 1) throw new ArgumentException(L.T("LastProfile"));
        var index = Profiles.FindIndex(p => p.Id == SelectedProfileId);
        Profiles.RemoveAt(index);
        SelectedProfileId = Profiles[Math.Min(index, Profiles.Count - 1)].Id;
    }

    public static ProxyProfiles FromLegacy(ProxySettings settings)
    {
        var profiles = new ProxyProfiles();
        profiles.UpdateSelected(settings);
        return profiles;
    }
}
