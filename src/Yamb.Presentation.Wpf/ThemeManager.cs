using System.Windows;

namespace Yamb.Presentation.Wpf;

public enum AppTheme
{
    Dark,
    Light,
    Midnight,
    Emerald,
    Sunset,
    Rose
}

public sealed record ThemeInfo(
    AppTheme Theme,
    string NameKey,
    string DescriptionKey,
    string SurfaceColor,
    string AccentColor,
    bool IsDark)
{
    public string AutomationId => $"Theme{Theme}";
}

public static class ThemeManager
{
    private static ResourceDictionary? _themeDictionary;

    public static IReadOnlyList<ThemeInfo> Themes { get; } =
    [
        new(AppTheme.Dark, "L.Settings.ThemeDark", "L.Settings.ThemeDarkSubtitle", "#1D1913", "#EDD5A8", true),
        new(AppTheme.Light, "L.Settings.ThemeLight", "L.Settings.ThemeLightSubtitle", "#EAF2FC", "#1F6FEB", false),
        new(AppTheme.Midnight, "L.Settings.ThemeMidnight", "L.Settings.ThemeMidnightSubtitle", "#0D1E31", "#57D4FF", true),
        new(AppTheme.Emerald, "L.Settings.ThemeEmerald", "L.Settings.ThemeEmeraldSubtitle", "#0E2118", "#6EE7B7", true),
        new(AppTheme.Sunset, "L.Settings.ThemeSunset", "L.Settings.ThemeSunsetSubtitle", "#2A1424", "#FF9F6E", true),
        new(AppTheme.Rose, "L.Settings.ThemeRose", "L.Settings.ThemeRoseSubtitle", "#FFF1F4", "#D6336C", false)
    ];

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static ThemeInfo Info(AppTheme theme)
    {
        foreach (var info in Themes)
        {
            if (info.Theme == theme) return info;
        }

        return Themes[0];
    }

    public static bool IsSupported(AppTheme theme)
    {
        foreach (var info in Themes)
        {
            if (info.Theme == theme) return true;
        }

        return false;
    }

    public static void Apply(AppTheme theme)
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null) return;

        var selected = IsSupported(theme) ? theme : AppTheme.Dark;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/{selected}.xaml", UriKind.Relative)
        };

        if (_themeDictionary is not null)
        {
            resources.MergedDictionaries.Remove(_themeDictionary);
        }

        resources.MergedDictionaries.Add(dictionary);
        _themeDictionary = dictionary;
        Current = selected;
    }
}
