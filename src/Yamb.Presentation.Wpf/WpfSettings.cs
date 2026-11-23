using System.IO;
using System.Text.Json;

namespace Yamb.Presentation.Wpf;

public enum DiceFaceStyle
{
    Pips,
    Numbers,
    Roman
}

public enum AppLanguage
{
    Serbian,
    English
}

public static class WpfSettings
{
    public const int MinAnimationFps = 24;
    public const int MaxAnimationFps = 999;
    public const int DefaultAnimationFps = 165;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Yamb",
        "settings.json");

    public static DiceFaceStyle FaceStyle { get; set; } = DiceFaceStyle.Pips;

    public static double DieSize { get; set; } = 54;

    public static int DiceCount { get; set; } = 6;

    public static AppTheme Theme { get; set; } = AppTheme.Dark;

    public static AppLanguage Language { get; set; } = AppLanguage.Serbian;

    public static bool AutoCalculate { get; set; } = true;

    private static int _animationFps = DefaultAnimationFps;

    public static int AnimationFps
    {
        get => _animationFps;
        set => _animationFps = Math.Clamp(value, MinAnimationFps, MaxAnimationFps);
    }

    public static string? LastError { get; private set; }

    public static void Load()
    {
        LastError = null;
        try
        {
            if (!File.Exists(FilePath)) return;

            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
            if (data is null) return;

            FaceStyle = Enum.TryParse<DiceFaceStyle>(data.FaceStyle, out var style) ? style : DiceFaceStyle.Pips;
            DieSize = Math.Clamp(data.DieSize, 36, 72);
            DiceCount = Math.Clamp(data.DiceCount ?? 6, 5, 6);
            Theme = Enum.TryParse<AppTheme>(data.Theme, ignoreCase: true, out var theme) && ThemeManager.IsSupported(theme)
                ? theme
                : AppTheme.Dark;
            Language = Enum.TryParse<AppLanguage>(data.Language, out var language) ? language : AppLanguage.Serbian;
            AutoCalculate = data.AutoCalculate ?? true;
            AnimationFps = data.AnimationFps ?? DefaultAnimationFps;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
        }
    }

    public static bool Save()
    {
        LastError = null;
        string? tempPath = null;
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (directory is not null) Directory.CreateDirectory(directory);

            var data = new SettingsData
            {
                FaceStyle = FaceStyle.ToString(),
                DieSize = DieSize,
                DiceCount = DiceCount,
                Theme = Theme.ToString(),
                Language = Language.ToString(),
                AutoCalculate = AutoCalculate,
                AnimationFps = AnimationFps
            };

            tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(data));
            File.Move(tempPath, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = ex.Message;
            if (tempPath is not null && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (IOException) { }
            }

            return false;
        }
    }

    public static void Reset()
    {
        FaceStyle = DiceFaceStyle.Pips;
        DieSize = 54;
        DiceCount = 6;
        Theme = AppTheme.Dark;
        Language = AppLanguage.Serbian;
        AutoCalculate = true;
        AnimationFps = DefaultAnimationFps;
    }

    private sealed class SettingsData
    {
        public string FaceStyle { get; set; } = "Pips";

        public double DieSize { get; set; } = 54;

        public int? DiceCount { get; set; }

        public string Theme { get; set; } = "Dark";

        public string Language { get; set; } = "Serbian";

        public bool? AutoCalculate { get; set; }

        public int? AnimationFps { get; set; }
    }
}
