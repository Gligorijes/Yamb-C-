namespace Yamb.Presentation.Wpf;

public static class HelpText
{
    public static IReadOnlyList<string> Titles => new[]
    {
        Localization.Text("L.Help.Rows"),
        Localization.Text("L.Help.Columns"),
        Localization.Text("L.Help.Bonuses"),
        Localization.Text("L.Help.Goal"),
        Localization.Text("L.Help.About")
    };

    public static IReadOnlyList<string> Pages => new[]
    {
        Localization.Text("L.Help.RowsPage"),
        Localization.Text("L.Help.ColumnsPage"),
        Localization.Text("L.Help.BonusesPage"),
        Localization.Text("L.Help.GoalPage"),
        Localization.Format("L.Help.AboutPage", BuildInfo.Display)
    };
}
