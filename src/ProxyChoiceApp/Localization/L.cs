using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;

namespace ProxyChoiceApp.Localization;

public sealed record LanguageOption(string Code, string Name)
{
    public string Flag => $"pack://application:,,,/Assets/Flags/{Code}.png";
}
public sealed record HelpSection(string Title, string Body);

public sealed class L : INotifyPropertyChanged
{
    public static L Current { get; } = new();
    public static IReadOnlyList<LanguageOption> Supported { get; } = new[] {
        new LanguageOption("en", "English"), new("fr", "Français"), new("es", "Español"),
        new("zh", "中文"), new("de", "Deutsch"), new("ru", "Русский")
    };
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = new();
    private Dictionary<string, string> _strings = new();
    public string Code { get; private set; } = "ru";
    public event PropertyChangedEventHandler? PropertyChanged;
    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : Catalog("en").GetValueOrDefault(key, key);
    public static string T(string key) => Current[key];
    public static string F(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(key), values);

    private L() { }
    public static IReadOnlyDictionary<string, string> ReadCatalog(string code) => Catalog(code);
    private static Dictionary<string, string> Catalog(string code)
    {
        if (Catalogs.TryGetValue(code, out var values)) return values;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ProxyChoiceApp.Localization.{code}.json")
            ?? throw new IOException("Missing language resource: " + code);
        values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        Catalogs[code] = values; return values;
    }
    public void Set(string code)
    {
        if (!Supported.Any(l => l.Code == code)) code = "ru";
        _strings = Catalog(code); Code = code;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(code == "zh" ? "zh-CN" : code);
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        PropertyChanged?.Invoke(this, new("Item[]"));
    }
    public IReadOnlyList<HelpSection> Help => Enumerable.Range(1, 9).Select(i => new HelpSection(this[$"Help{i}Title"], this[$"Help{i}Body"])).ToArray();
}

[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public TrExtension() { }
    public TrExtension(string key) => Key = key;
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = L.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
