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

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;

            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
            if (data is null) return;

            FaceStyle = Enum.TryParse<DiceFaceStyle>(data.FaceStyle, out var style) ? style : DiceFaceStyle.Pips;
            DieSize = Math.Clamp(data.DieSize, 36, 72);
            DiceCount = Math.Clamp(data.DiceCount ?? 6, 5, 6);
            Theme = Enum.TryParse<AppTheme>(data.Theme, out var theme) ? theme : AppTheme.Dark;
            Language = Enum.TryParse<AppLanguage>(data.Language, out var language) ? language : AppLanguage.Serbian;
            AutoCalculate = data.AutoCalculate ?? true;
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    public static void Save()
    {
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
                AutoCalculate = AutoCalculate
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data));
        }
        catch (IOException)
        {
        }
    }

    private sealed class SettingsData
    {
        public string FaceStyle { get; set; } = "Pips";

        public double DieSize { get; set; } = 54;

        public int? DiceCount { get; set; }

        public string Theme { get; set; } = "Dark";

        public string Language { get; set; } = "Serbian";

        public bool? AutoCalculate { get; set; }
    }
}
