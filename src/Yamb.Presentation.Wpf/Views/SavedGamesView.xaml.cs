using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Domain.Exceptions;

namespace Yamb.Presentation.Wpf.Views;

public partial class SavedGamesView : UserControl
{
    private readonly IGameService _gameService;
    private readonly Action<Guid> _onResume;
    private readonly Action _onBack;
    private Guid? _pendingDelete;
    private bool _busy;

    public SavedGamesView(IGameService gameService, Action<Guid> onResume, Action onBack)
    {
        InitializeComponent();
        _gameService = gameService;
        _onResume = onResume;
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
            var games = await _gameService.ListGamesAsync();
            Render(games);
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

    private void Render(IReadOnlyList<GameSummaryDto> games)
    {
        GamesPanel.Children.Clear();

        if (games.Count == 0)
        {
            GamesPanel.Children.Add(new StackPanel
            {
                Margin = new Thickness(16, 42, 16, 42),
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = Localization.Text("L.SavedGames.EmptyTitle"),
                        FontSize = 17,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("TextBrush"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = Localization.Text("L.SavedGames.EmptyText"),
                        FontSize = 13,
                        Foreground = (Brush)FindResource("MutedTextBrush"),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 420,
                        Margin = new Thickness(0, 8, 0, 0)
                    }
                }
            });
            return;
        }

        foreach (var game in games)
        {
            GamesPanel.Children.Add(MakeGameCard(game));
        }
    }

    private UIElement MakeGameCard(GameSummaryDto game)
    {
        var textBrush = (Brush)FindResource("TextBrush");
        var mutedBrush = (Brush)FindResource("MutedTextBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var surfaceBrush = (Brush)FindResource("SurfaceAltBrush");
        var dangerBrush = (Brush)FindResource("DangerBrush");

        int currentIndex = Math.Clamp(game.CurrentPlayerIndex, 0, Math.Max(0, game.Players.Count - 1));
        var current = game.Players.Count > 0 ? game.Players[currentIndex] : null;

        var title = new TextBlock
        {
            Text = game.PlayerNames,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var meta = new TextBlock
        {
            Text = $"{Localization.Format("L.SavedGames.Meta", game.ColumnCount, game.DiceCount)}  Â·  " +
                   (current is null ? "" : Localization.Format("L.SavedGames.Turn", current.Name)),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = mutedBrush,
            Margin = new Thickness(0, 5, 0, 0)
        };

        var scores = new TextBlock
        {
            Text = string.Join("    ", game.Players.Select(p => $"{p.Name}: {p.Score}")),
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = accentBrush,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var progressLine = new TextBlock
        {
            Text = current is null
                ? ""
                : Localization.Format("L.SavedGames.Progress", current.FilledCells, game.TotalCells),
            FontSize = 11,
            Foreground = mutedBrush,
            Margin = new Thickness(0, 7, 0, 0)
        };

        int filled = current?.FilledCells ?? 0;
        double ratio = game.TotalCells > 0 ? Math.Clamp((double)filled / game.TotalCells, 0, 1) : 0;
        var progress = new Grid
        {
            Width = 210,
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
            Background = (Brush)FindResource("BorderBrush")
        };
        progress.Children.Add(new Border
        {
            Width = 210 * ratio,
            Background = accentBrush,
            CornerRadius = new CornerRadius(3.5),
            HorizontalAlignment = HorizontalAlignment.Left
        });
        progress.Clip = new RectangleGeometry(new Rect(0, 0, 210, 7), 3.5, 3.5);

        var date = new TextBlock
        {
            Text = Localization.Format(
                "L.SavedGames.SavedAt",
                game.SavedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")),
            FontSize = 11,
            Foreground = mutedBrush,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var info = new StackPanel();
        info.Children.Add(title);
        info.Children.Add(meta);
        info.Children.Add(scores);
        info.Children.Add(progressLine);
        info.Children.Add(progress);
        info.Children.Add(date);

        var continueButton = new Button
        {
            Content = Localization.Text("L.SavedGames.Continue"),
            Style = (Style)FindResource("PrimaryButtonStyle"),
            Height = 36,
            MinWidth = 104,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        AutomationProperties.SetAutomationId(continueButton, $"ResumeGame_{game.Id:N}");
        continueButton.Click += (_, _) => _onResume(game.Id);

        var deleteButton = new Button
        {
            Content = Localization.Text("L.SavedGames.Delete"),
            Style = (Style)FindResource("SmallButtonStyle"),
            Foreground = dangerBrush,
            Height = 34,
            MinWidth = 86,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        AutomationProperties.SetAutomationId(deleteButton, $"DeleteGame_{game.Id:N}");
        deleteButton.Click += (_, _) => ShowDeleteConfirmation(game);

        var actions = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 0, 0)
        };
        actions.Children.Add(continueButton);
        actions.Children.Add(deleteButton);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(info);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        return new Border
        {
            Background = surfaceBrush,
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20, 16, 20, 16),
            Margin = new Thickness(4, 4, 4, 6),
            Child = grid
        };
    }

    private void ShowDeleteConfirmation(GameSummaryDto game)
    {
        _pendingDelete = game.Id;
        PopupText.Text = Localization.Format("L.SavedGames.DeleteText", game.PlayerNames);
        PopupOverlay.Visibility = Visibility.Visible;
        UiAnimations.FadeIn(PopupOverlay, 0.16);
    }

    private void OnDeleteCancelled(object sender, RoutedEventArgs e)
    {
        _pendingDelete = null;
        PopupOverlay.Visibility = Visibility.Collapsed;
    }

    private async void OnDeleteConfirmed(object sender, RoutedEventArgs e)
    {
        PopupOverlay.Visibility = Visibility.Collapsed;
        if (_pendingDelete is not { } gameId) return;

        _pendingDelete = null;
        try
        {
            await _gameService.DeleteGameAsync(gameId);
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
