using System.Windows;

namespace Yamb.Presentation.Wpf;

public enum AppTheme
{
    Dark,
    Light
}

public static class ThemeManager
{
    private static ResourceDictionary? _themeDictionary;

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static void Apply(AppTheme theme)
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null) return;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative)
        };

        if (_themeDictionary is not null)
        {
            resources.MergedDictionaries.Remove(_themeDictionary);
        }

        resources.MergedDictionaries.Add(dictionary);
        _themeDictionary = dictionary;
        Current = theme;
    }
}
