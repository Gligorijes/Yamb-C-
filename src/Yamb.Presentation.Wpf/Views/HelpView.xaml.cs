using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Yamb.Presentation.Wpf.Views;

public partial class HelpView : UserControl
{
    private readonly Action _onBack;
    private readonly List<Button> _tabs = new();
    private int _page;
    private string _query = "";

    public HelpView(Action onBack, int initialPage = 0)
    {
        InitializeComponent();
        _onBack = onBack;
        VersionText.Text = BuildInfo.Display;

        for (int i = 0; i < HelpText.Titles.Count; i++)
        {
            int index = i;
            var button = new Button
            {
                Content = $"{i + 1}. {HelpText.Titles[i]}",
                Style = (Style)FindResource("HelpTabStyle"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            AutomationProperties.SetAutomationId(button, $"HelpTab{i}");
            button.Click += (_, _) => ShowPage(index);
            _tabs.Add(button);
            TabPanel.Children.Add(button);
        }

        ShowPage(Math.Clamp(initialPage, 0, HelpText.Pages.Count - 1));
    }

    private void ShowPage(int page)
    {
        _page = page;
        PageText.Text = HelpText.Pages[page];
        PageIndicator.Text = $"{page + 1}/{HelpText.Pages.Count}";

        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Tag = i == page ? "active" : null;
        }

        PageScroll.ScrollToTop();
        UiAnimations.SlideIn(PageText);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_tabs.Count == 0) return;

        _query = SearchBox.Text.Trim();
        var matches = new List<int>();

        for (int i = 0; i < _tabs.Count; i++)
        {
            bool match = _query.Length == 0 ||
                HelpText.Titles[i].Contains(_query, StringComparison.CurrentCultureIgnoreCase) ||
                HelpText.Pages[i].Contains(_query, StringComparison.CurrentCultureIgnoreCase);
            _tabs[i].Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            if (match) matches.Add(i);
        }

        NoResultsText.Visibility = _query.Length > 0 && matches.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_query.Length > 0 && matches.Count > 0 && !matches.Contains(_page))
        {
            ShowPage(matches[0]);
        }
    }

    private void OnPrevClick(object sender, RoutedEventArgs e)
    {
        int page = _page - 1;
        if (page < 0) page = HelpText.Pages.Count - 1;
        ShowPage(page);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        int page = _page + 1;
        if (page >= HelpText.Pages.Count) page = 0;
        ShowPage(page);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _onBack();
    }
}
