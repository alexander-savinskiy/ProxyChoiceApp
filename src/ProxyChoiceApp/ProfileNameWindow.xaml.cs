using System;
using System.Windows;

namespace ProxyChoiceApp;

public partial class ProfileNameWindow : Window
{
    private readonly Func<string, string> _checkName;
    public string ProfileName { get; private set; } = "";
    public ProfileNameWindow(string title, string name, Func<string, string> checkName)
    {
        InitializeComponent(); Title = title; NameBox.Text = name; _checkName = checkName;
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) { NameBox.Focus(); NameBox.SelectAll(); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { ProfileName = _checkName(NameBox.Text); DialogResult = true; }
        catch (ArgumentException error) { ErrorText.Text = error.Message; NameBox.Focus(); }
    }
}
