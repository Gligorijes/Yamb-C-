using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Domain.Exceptions;
using Yamb.Presentation.Wpf.Views;

namespace Yamb.Presentation.Wpf;

public partial class MainWindow : Window
{
    private readonly IGameService _gameService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IBotAdvisor _botAdvisor;
    private bool _started;
    private bool _isFullscreen;
    private string _breadcrumbKey = "L.Nav.Menu";
    private WindowState _previousState = WindowState.Normal;

    public MainWindow(IGameService gameService, ILeaderboardService leaderboardService, IBotAdvisor botAdvisor)
    {
        InitializeComponent();
        _gameService = gameService;
        _leaderboardService = leaderboardService;
        _botAdvisor = botAdvisor;
        Localization.LanguageChanged += OnLanguageChanged;
        Loaded += OnWindowLoaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BreadcrumbText.Text = Localization.Text(_breadcrumbKey);
        FullscreenButton.ToolTip = Localization.Text(
            _isFullscreen ? "L.Main.ExitFullscreen" : "L.Main.Fullscreen");
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        UiAnimations.FadeIn(this);
        if (_started) return;
        _started = true;
        await ShowMenuAsync();
    }

    private async Task ShowMenuAsync()
    {
        bool hasSaved;
        try
        {
            hasSaved = (await _gameService.ListGamesAsync()).Count > 0;
        }
        catch (Exception ex) when (ex is DomainException or GameException)
        {
            hasSaved = false;
        }

        var view = new MenuView(
            hasSaved,
            OnNewGameRequested,
            OnResumeRequested,
            OnSettingsRequested,
            OnLeaderboardRequested,
            OnHelpRequested,
            Close);
        Navigate(view, "L.Nav.Menu");
    }

    private void Navigate(FrameworkElement view, string breadcrumbKey)
    {
        _breadcrumbKey = breadcrumbKey;
        BreadcrumbText.Text = Localization.Text(breadcrumbKey);
        PageHost.Content = view;
        UiAnimations.SlideIn(view);
    }

    private void OnNewGameRequested()
    {
        Navigate(new NewGameView(OnGameStartRequested, GoMenu), "L.Nav.NewGame");
    }

    private async void OnGameStartRequested(StartGameCommand command)
    {
        try
        {
            var game = await _gameService.StartGameAsync(command);
            ShowGame(game.Id);
        }
        catch (Exception ex) when (ex is DomainException or GameException)
        {
            MessageBox.Show(this, ErrorText.From(ex), Localization.Text("L.App.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnResumeRequested()
    {
        try
        {
            var games = await _gameService.ListGamesAsync();
            if (games.Count == 0)
            {
                await ShowMenuAsync();
                return;
            }

            Navigate(
                new SavedGamesView(_gameService, ShowGame, GoMenu),
                "L.Nav.SavedGames");
        }
        catch (Exception ex) when (ex is DomainException or GameException)
        {
            MessageBox.Show(this, ErrorText.From(ex), Localization.Text("L.App.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowGame(Guid gameId)
    {
        Navigate(
            new GameView(_gameService, _botAdvisor, gameId, GoMenu, OnGameCompleted, () => ShowHelp(1)),
            "L.Nav.Game");
    }

    private void OnGameCompleted()
    {
        Navigate(new LeaderboardView(_leaderboardService, GoMenu), "L.Nav.Leaderboard");
    }

    private void OnSettingsRequested()
    {
        Navigate(new SettingsView(GoMenu), "L.Nav.Settings");
    }

    private void OnLeaderboardRequested()
    {
        Navigate(new LeaderboardView(_leaderboardService, GoMenu), "L.Nav.Leaderboard");
    }

    private void OnHelpRequested()
    {
        ShowHelp(0);
    }

    private void ShowHelp(int page)
    {
        Navigate(new HelpView(GoMenu, page), "L.Nav.Help");
    }

    private void GoMenu()
    {
        _ = ShowMenuAsync();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (IsInteractive(e.OriginalSource as DependencyObject)) return;
        DragMove();
    }

    private static bool IsInteractive(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase or ScrollBar) return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _previousState = WindowState;
            WindowState = WindowState.Normal;
            Topmost = true;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
            FullscreenButton.Content = "\uE73F";
            FullscreenButton.ToolTip = Localization.Text("L.Main.ExitFullscreen");
            return;
        }

        Topmost = false;
        WindowState = WindowState.Normal;
        WindowState = _previousState;
        _isFullscreen = false;
        FullscreenButton.Content = "\uE740";
        FullscreenButton.ToolTip = Localization.Text("L.Main.Fullscreen");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
