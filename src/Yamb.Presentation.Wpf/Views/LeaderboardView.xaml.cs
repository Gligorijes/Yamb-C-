using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Domain.Exceptions;

namespace Yamb.Presentation.Wpf.Views;

public partial class LeaderboardView : UserControl
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly Action _onBack;
    private bool _busy;

    public LeaderboardView(ILeaderboardService leaderboardService, Action onBack)
    {
        InitializeComponent();
        _leaderboardService = leaderboardService;
        _onBack = onBack;
        Loaded += OnViewLoaded;
    }

    private async void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var entries = await _leaderboardService.GetTopScoresAsync();
            Render(entries);
        }
        catch (Exception ex) when (ex is DomainException or GameException)
        {
            MessageBox.Show(Window.GetWindow(this), ErrorText.From(ex), Localization.Text("L.App.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
    }

    private void Render(IReadOnlyList<LeaderboardEntryDto> entries)
    {
        RowsPanel.Children.Clear();

        if (entries.Count == 0)
        {
            RowsPanel.Children.Add(new TextBlock
            {
                Text = Localization.Text("L.Leaderboard.Empty"),
                Foreground = (Brush)FindResource("MutedTextBrush"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 40)
            });
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            RowsPanel.Children.Add(MakeRow(entries[i], i));
        }
    }

    private UIElement MakeRow(LeaderboardEntryDto entry, int index)
    {
        var textBrush = (Brush)FindResource("TextBrush");
        var mutedBrush = (Brush)FindResource("MutedTextBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var surfaceBrush = (Brush)FindResource("SurfaceAltBrush");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var badge = MakeRankBadge(entry.Rank, mutedBrush);
        var nameText = MakeCellText(entry.Participants, textBrush, true, HorizontalAlignment.Left);
        var scoreText = MakeCellText(entry.Score.ToString(), accentBrush, true, HorizontalAlignment.Right);
        var dateText = MakeCellText(entry.AchievedAt.ToLocalTime().ToString("dd.MM.yyyy"), mutedBrush, false, HorizontalAlignment.Right);

        foreach (var (element, column) in new[] { (badge, 0), (nameText, 1), (scoreText, 2), (dateText, 3) })
        {
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        return new Border
        {
            Background = index % 2 == 0 ? surfaceBrush : Brushes.Transparent,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 2, 0, 2),
            Child = grid
        };
    }

    private UIElement MakeRankBadge(int rank, Brush mutedBrush)
    {
        Brush background = rank switch
        {
            1 => (Brush)FindResource("WarningBrush"),
            2 => MakeSolid(0xC3, 0xCB, 0xD9),
            3 => MakeSolid(0xD0, 0x8A, 0x54),
            _ => (Brush)FindResource("PanelBrush")
        };
        var foreground = rank is >= 1 and <= 3 ? MakeSolid(0x14, 0x16, 0x1E) : mutedBrush;

        return new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = background,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = rank.ToString(),
                Foreground = foreground,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static Brush MakeSolid(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static TextBlock MakeCellText(string text, Brush foreground, bool bold, HorizontalAlignment alignment) => new()
    {
        Text = text,
        Foreground = foreground,
        FontSize = 14,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        HorizontalAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        PopupText.Text = Localization.Text("L.Leaderboard.ClearText");
        PopupOverlay.Visibility = Visibility.Visible;
        UiAnimations.FadeIn(PopupOverlay, 0.16);
    }

    private void OnClearCancelled(object sender, RoutedEventArgs e)
    {
        PopupOverlay.Visibility = Visibility.Collapsed;
    }

    private async void OnClearConfirmed(object sender, RoutedEventArgs e)
    {
        PopupOverlay.Visibility = Visibility.Collapsed;
        try
        {
            await _leaderboardService.ClearScoresAsync();
            await LoadAsync();
        }
        catch (Exception ex) when (ex is DomainException or GameException)
        {
            MessageBox.Show(Window.GetWindow(this), ErrorText.From(ex), Localization.Text("L.App.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _onBack();
    }
}
