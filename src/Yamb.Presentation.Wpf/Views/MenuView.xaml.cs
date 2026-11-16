using System.Windows.Controls;

namespace Yamb.Presentation.Wpf.Views;

public partial class MenuView : UserControl
{
    public MenuView(
        bool hasSavedGame,
        Action onNewGame,
        Action onResume,
        Action onSettings,
        Action onLeaderboard,
        Action onHelp,
        Action onExit)
    {
        InitializeComponent();

        DiceChipText.Text = Localization.Format("L.Menu.DiceCount", WpfSettings.DiceCount);
        ThemeChipText.Text = Localization.Format(
            "L.Menu.ThemeChip",
            Localization.Text(WpfSettings.Theme == AppTheme.Dark
                ? "L.Settings.ThemeDark"
                : "L.Settings.ThemeLight"));
        ScoringChipText.Text = Localization.Text(
            WpfSettings.AutoCalculate ? "L.Menu.AutoOn" : "L.Menu.AutoOff");

        ResumeButton.IsEnabled = hasSavedGame;
        ResumeSubtitle.Text = Localization.Text(
            hasSavedGame ? "L.Menu.ResumeSubtitle" : "L.Menu.NoSaved");

        NewGameButton.Click += (_, _) => onNewGame();
        ResumeButton.Click += (_, _) => onResume();
        SettingsButton.Click += (_, _) => onSettings();
        LeaderboardButton.Click += (_, _) => onLeaderboard();
        HelpButton.Click += (_, _) => onHelp();
        ExitButton.Click += (_, _) => onExit();
    }
}
