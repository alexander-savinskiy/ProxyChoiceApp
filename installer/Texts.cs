using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

internal static class Texts
{
    internal static readonly string[] Codes = { "en", "fr", "es", "zh", "de", "ru" };
    internal static readonly string[] Names = { "English", "Français", "Español", "中文", "Deutsch", "Русский" };
    private static Dictionary<string, string> _catalog;
    internal static string Code;
    static Texts() { Set(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName); }
    internal static void Set(string code)
    {
        Code = Array.IndexOf(Codes, code) >= 0 ? code : "en";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("language." + Code + ".json"))
        using (var reader = new StreamReader(stream))
            _catalog = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
    }
    internal static string T(string key) { string value; return _catalog.TryGetValue(key, out value) ? value : key; }
}
