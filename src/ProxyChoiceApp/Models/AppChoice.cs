using System.ComponentModel;
using ProxyChoiceApp.Localization;

namespace ProxyChoiceApp.Models;

public sealed class AppChoice : INotifyPropertyChanged
{
    public string Name { get; }
    public string Path { get; }
    public bool Available { get; }
    public bool IsBuiltIn { get; }
    public bool IsYouTube { get; }
    public bool IsBrowserYouTube { get; }
    private bool _useProxy;
    public bool UseProxy { get => _useProxy; set { if (_useProxy == value) return; _useProxy = value; PropertyChanged?.Invoke(this, new(nameof(UseProxy))); } }
    public string Hint => !Available ? L.T("NotInstalled") : IsBrowserYouTube ? L.F("YouTubeBrowser", new AppEntry(Path).Name) : Path;
    public event PropertyChangedEventHandler? PropertyChanged;
    public AppChoice(string name, string path, bool available = true, bool builtIn = false, bool youtube = false, bool browserYouTube = false)
    { Name = name; Path = path; Available = available; IsBuiltIn = builtIn; IsYouTube = youtube; IsBrowserYouTube = browserYouTube; }
    public void RefreshLanguage() => PropertyChanged?.Invoke(this, new(nameof(Hint)));
}
