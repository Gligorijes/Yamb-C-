using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Yamb.Application.DTOs;
using Yamb.Application.Exceptions;
using Yamb.Application.Interfaces;
using Yamb.Domain.Enums;
using Yamb.Domain.Exceptions;

namespace Yamb.Presentation.Wpf.Views;

public partial class GameView : UserControl
{
    private const int MaxRollsPerTurn = 3;
    private const int DesiredFrameRate = 165;

    private static readonly IReadOnlyDictionary<Column, string> ColumnHeaders =
        new Dictionary<Column, string>
        {
            [Column.Downward] = "\u25BC",
            [Column.Free] = "\u25BC\u25B2",
            [Column.Upward] = "\u25B2",
            [Column.Announcement] = "N",
            [Column.Hand] = "R",
            [Column.BothEnds] = "\u25BC\n\u25B2",
            [Column.MiddleOut] = "\u25B2\n\u25BC",
            [Column.Directed] = "D",
            [Column.Obligatory] = "O",
            [Column.Maximum] = "M"
        };

    private static readonly Row[] Rows =
    {
        Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes,
        Row.Max, Row.Min, Row.Straight, Row.ThreeOfAKind, Row.FullHouse,
        Row.FourOfAKind, Row.Yamb
    };

    private static readonly Row[] NumberRows =
    {
        Row.Ones, Row.Twos, Row.Threes, Row.Fours, Row.Fives, Row.Sixes
    };

    private static readonly Row[] CombinationRows =
    {
        Row.Straight, Row.ThreeOfAKind, Row.FullHouse, Row.FourOfAKind, Row.Yamb
    };

    private static readonly string[] DieNameKeys =
    {
        "L.Game.Die.Magenta", "L.Game.Die.Yellow", "L.Game.Die.Blue",
        "L.Game.Die.Green", "L.Game.Die.Red", "L.Game.Die.White"
    };

    private static string L(string key) => Localization.Text(key);

    private static string ColumnName(Column column) => L($"L.Game.Column.{column}.Name");

    private static string ColumnTooltip(Column column) => L($"L.Game.Column.{column}.Tooltip");

    private static string RowLabel(Row row) => L($"L.Game.Row.{row}");

    private static string RowTooltip(Row row) => L($"L.Game.Row.{row}.Tooltip");

    private static string DieName(int index) => L(DieNameKeys[index % DieNameKeys.Length]);

    private sealed class SheetView
    {
        public int PlayerIndex;
        public Border Card = null!;
        public TextBlock NameText = null!;
        public StackPanel ActiveBadge = null!;
        public readonly Dictionary<(Column Column, Row Row), Button> Cells = new();
        public readonly Dictionary<BonusType, Dictionary<Column, TextBlock>> SectionTotals = new();
        public readonly Dictionary<Row, TextBlock> RowTotals = new();
        public readonly Dictionary<BonusType, TextBlock> SectionGrandTotals = new();
        public TextBlock TotalText = null!;
    }

    private readonly IGameService _gameService;
    private readonly IBotAdvisor _botAdvisor;
    private readonly Guid _gameId;
    private readonly Action _onExitToMenu;
    private readonly Action _onGameOver;
    private readonly Action _onHelp;

    private readonly Brush _panelGradientBrush;
    private readonly Brush _borderBrush;
    private readonly Brush _borderStrongBrush;
    private readonly Brush _sheetFrameBrush;
    private readonly Brush _headerBandBrush;
    private readonly Brush _sumBandBrush;
    private readonly Brush _textBrush;
    private readonly Brush _mutedBrush;
    private readonly Brush _accentBrush;
    private readonly Brush _accentTintBrush;
    private readonly Brush _blueBrush;
    private readonly Brush _greenBrush;
    private readonly Effect _dieShadow;
    private readonly Effect _cardShadow;
    private readonly Effect _accentGlow;
    private readonly Brush[] _dieBrushes;

    private readonly List<SheetView> _sheets = new();
    private IReadOnlyList<Column> _columns = Array.Empty<Column>();
    private readonly Dictionary<int, DiceSetDto> _diceCache = new();
    private readonly Dictionary<int, string> _diceViewSignatures = new();
    private readonly Dictionary<(int PlayerIndex, Column Column, Row Row), int> _availableMoves = new();
    private readonly List<StackPanel> _diceRows = new();
    private readonly Random _random = new();
    private readonly bool _autoCalculate = WpfSettings.AutoCalculate;

    private TextBlock? _statusProgress;
    private TextBlock? _statusRolls;
    private Button? _rollButton;
    private bool _layoutBuilt;
    private bool _gameOverHandled;

    private GameDto? _lastGame;
    private bool _busy;
    private bool _botRunning;
    private readonly CancellationTokenSource _botCts = new();

    public GameView(
        IGameService gameService,
        IBotAdvisor botAdvisor,
        Guid gameId,
        Action onExitToMenu,
        Action onGameOver,
        Action onHelp)
    {
        InitializeComponent();
        _gameService = gameService;
        _botAdvisor = botAdvisor;
        _gameId = gameId;
        _onExitToMenu = onExitToMenu;
        _onGameOver = onGameOver;
        _onHelp = onHelp;

        _panelGradientBrush = (Brush)FindResource("PanelGradientBrush");
        _borderBrush = (Brush)FindResource("BorderBrush");
        _borderStrongBrush = (Brush)FindResource("BorderStrongBrush");
        _sheetFrameBrush = (Brush)FindResource("SheetFrameBrush");
        _headerBandBrush = (Brush)FindResource("HeaderBandBrush");
        _sumBandBrush = (Brush)FindResource("SumBandBrush");
        _textBrush = (Brush)FindResource("TextBrush");
        _mutedBrush = (Brush)FindResource("MutedTextBrush");
        _accentBrush = (Brush)FindResource("AccentBrush");
        _accentTintBrush = MakeAccentTint(_accentBrush);
        _blueBrush = (Brush)FindResource("BlueBrush");
        _greenBrush = (Brush)FindResource("GreenBrush");
        _dieShadow = (Effect)FindResource("DieShadow");
        _cardShadow = (Effect)FindResource("CardShadow");
        _accentGlow = (Effect)FindResource("AccentGlow");
        _dieBrushes = new[]
        {
            (Brush)FindResource("DieMagentaBrush"),
            (Brush)FindResource("DieYellowBrush"),
            (Brush)FindResource("DieBlueBrush"),
            (Brush)FindResource("DieGreenBrush"),
            (Brush)FindResource("DieRedBrush"),
            (Brush)FindResource("DieWhiteBrush")
        };

        Loaded += OnViewLoaded;
        Unloaded += (_, _) => _botCts.Cancel();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        Keyboard.Focus(this);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (PopupOverlay.Visibility == Visibility.Visible) return;

        if (e.Key is Key.R or Key.Space)
        {
            if (_rollButton?.IsEnabled == true)
            {
                OnRollClick(this, new RoutedEventArgs());
                e.Handled = true;
            }

            return;
        }

        int? dieIndex = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            _ => null
        };

        if (dieIndex is int index)
        {
            var game = _lastGame;
            if (game is not null &&
                game.RollNumber >= 1 &&
                game.RollNumber < MaxRollsPerTurn &&
                index < game.Dice.Dice.Count)
            {
                _ = OnDieClick(game.CurrentPlayerIndex, index);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var game = _lastGame;
            if (game?.AnnouncedRow is { } row && game.RollNumber >= MaxRollsPerTurn)
            {
                _ = OnCellClick(game.CurrentPlayerIndex, Column.Announcement, row);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.F1)
        {
            _onHelp();
            e.Handled = true;
        }
    }

    private async void OnRollClick(object sender, RoutedEventArgs e)
    {
        var game = _lastGame;
        if (game is null || IsBotTurn(game)) return;
        await ExecuteAsync(async () =>
        {
            int playerIndex = game.CurrentPlayerIndex;

            var fresh = await _gameService.RollDiceAsync(_gameId, playerIndex);

            await AnimateRollAsync(playerIndex, fresh.Dice);
            await RefreshAsync(fresh);
        });
    }

    private async Task AnimateRollAsync(int playerIndex, DiceSetDto dice)
    {
        var row = _diceRows.ElementAtOrDefault(playerIndex);
        if (row is null) return;

        _diceViewSignatures.Remove(playerIndex);
        row.Children.Clear();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        for (int i = 0; i < dice.Dice.Count; i++)
        {
            var die = dice.Dice[i];
            var container = BuildRollingDie(i, die.Value, die.IsHeld);
            row.Children.Add(container);

            var transforms = (TransformGroup)container.RenderTransform;
            var translate = (TranslateTransform)transforms.Children[0];
            var scale = (ScaleTransform)transforms.Children[1];
            var rotate = (RotateTransform)transforms.Children[2];
            int delay = i * 28;
            double startAngle = die.IsHeld ? 0 : _random.NextDouble() * 30 - 15;

            var drop = AtDisplayRate(new DoubleAnimation(-12, 0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            });
            translate.BeginAnimation(TranslateTransform.YProperty, drop);

            var spin = AtDisplayRate(new DoubleAnimation(startAngle, 0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            });
            rotate.BeginAnimation(RotateTransform.AngleProperty, spin);

            var bounce = AtDisplayRate(new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                FillBehavior = FillBehavior.Stop
            });
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0.82, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(190)))
            {
                EasingFunction = ease
            });
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
            {
                EasingFunction = ease
            });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce.Clone());

            var opacity = AtDisplayRate(new DoubleAnimation(
                die.IsHeld ? 0.7 : 0.35,
                die.IsHeld ? 0.55 : 1,
                TimeSpan.FromMilliseconds(220))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            });
            container.BeginAnimation(UIElement.OpacityProperty, opacity);
        }

        await Task.Delay(300 + Math.Max(0, dice.Dice.Count - 1) * 28);
    }

    private FrameworkElement BuildRollingDie(int dieIndex, int value, bool held)
    {
        double size = WpfSettings.DieSize;
        var face = new Border
        {
            Width = size,
            Height = size,
            Background = _dieBrushes[dieIndex % _dieBrushes.Length],
            CornerRadius = new CornerRadius(size * 0.18),
            Opacity = held ? 0.55 : 1.0,
            Child = DiceVisuals.BuildFace(value, size, WpfSettings.FaceStyle)
        };

        var container = new Grid
        {
            Width = size,
            Height = size,
            Margin = new Thickness(4, 0, 4, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false
        };
        container.Children.Add(face);

        if (held)
        {
            container.Children.Add(new Border
            {
                BorderBrush = _accentBrush,
                BorderThickness = new Thickness(2.5),
                CornerRadius = new CornerRadius(size * 0.18),
                Background = _accentTintBrush
            });
        }

        var transforms = new TransformGroup();
        transforms.Children.Add(new TranslateTransform());
        transforms.Children.Add(new ScaleTransform(1, 1));
        transforms.Children.Add(new RotateTransform());
        container.RenderTransform = transforms;
        container.Opacity = held ? 0.55 : 1;
        return container;
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        ShowPopup(
            L("L.Game.PausedTitle"),
            L("L.Game.PausedText"),
            (L("L.Game.Yes"), () => { _botCts.Cancel(); HidePopup(); _onExitToMenu(); }),
            (L("L.Game.No"), HidePopup));
    }

    private async Task OnCellClick(int playerIndex, Column column, Row row)
    {
        var game = _lastGame;
        if (game is null || playerIndex != game.CurrentPlayerIndex || IsBotTurn(game)) return;

        await ExecuteAsync(async () =>
        {
            var sheet = game.Players[playerIndex].Scoresheet;
            if (sheet.Cells[column].TryGetValue(row, out var current) && current.HasValue) return;

            if (column == Column.Announcement && game.RollNumber == 1 && game.AnnouncedRow is null)
            {
                try
                {
                    await _gameService.AnnounceAsync(_gameId, playerIndex, row);
                    await RefreshAsync();
                    PulseCell(playerIndex, column, row);
                }
                catch (DomainException)
                {
                    ShowPopup("Jamb", L("L.Game.NotAvailable"), (L("L.Game.Ok"), HidePopup));
                }

                return;
            }

            try
            {
                await _gameService.WriteScoreAsync(_gameId, playerIndex, column, row);

                if (column == Column.Hand && row == Row.Yamb)
                {
                    ShowPopup(
                        L("L.Game.HandYambTitle"),
                        L("L.Game.HandYambText"),
                        (L("L.Game.Continue"), () =>
                        {
                            HidePopup();
                            _ = ContinueAfterHandYambAsync(playerIndex, column, row);
                        }));
                    return;
                }

                await RefreshAsync();
                PulseCell(playerIndex, column, row);
            }
            catch (DomainException)
            {
                var message = game.AnnouncedRow is not null ? L("L.Game.CannotWrite") : L("L.Game.NotAvailable");
                ShowPopup("Jamb", message, (L("L.Game.Ok"), HidePopup));
            }
        });
    }

    private async Task ContinueAfterHandYambAsync(int playerIndex, Column column, Row row)
    {
        await ExecuteAsync(async () =>
        {
            await RefreshAsync();
            PulseCell(playerIndex, column, row);
        });
    }

    private void ShowPreview(int playerIndex, Column column, Row row, Button button)
    {
        var game = _lastGame;
        if (_busy || game is null || game.IsGameOver) return;
        if (game.CurrentPlayerIndex != playerIndex) return;
        if (button.Tag is "filled") return;
        if (!_availableMoves.TryGetValue((playerIndex, column, row), out int score)) return;

        button.Content = score.ToString();
    }

    private static void HidePreview(Button button)
    {
        if (button.Tag is "filled") return;
        button.Content = "";
    }

    private void PulseCell(int playerIndex, Column column, Row row)
    {
        if (playerIndex < _sheets.Count && _sheets[playerIndex].Cells.TryGetValue((column, row), out var cell))
        {
            UiAnimations.Pulse(cell);
        }
    }

    private async Task OnDieClick(int playerIndex, int dieIndex)
    {
        var game = _lastGame;
        if (game is null || playerIndex != game.CurrentPlayerIndex || IsBotTurn(game)) return;
        if (game.RollNumber < 1 || game.RollNumber >= MaxRollsPerTurn) return;

        await ExecuteAsync(async () =>
        {
            await _gameService.ToggleHoldAsync(_gameId, playerIndex, dieIndex);
            await RefreshAsync();
        });
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is DomainException or GameException)
        {
            MessageBox.Show(ErrorText.From(ex), Localization.Text("L.App.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool IsBotTurn(GameDto game) =>
        game.Mode == GameMode.VersusComputer && game.CurrentPlayerIndex == 1;

    private async Task RunBotTurnAsync()
    {
        if (_botRunning || _botCts.IsCancellationRequested) return;
        _botRunning = true;

        try
        {
            await Task.Delay(420, _botCts.Token);

            while (!_botCts.IsCancellationRequested)
            {
                var game = await _gameService.GetGameAsync(_gameId);
                if (game.IsGameOver || !IsBotTurn(game)) break;

                if (game.RollNumber == 0)
                {
                    var rolled = await _gameService.RollDiceAsync(_gameId, 1);
                    await AnimateBotRollAsync(rolled.Dice);
                    await RefreshAsync(rolled);
                    await TryBotAnnounceAsync(rolled);
                    await Task.Delay(480, _botCts.Token);
                    continue;
                }

                if (game.RollNumber < MaxRollsPerTurn)
                {
                    var moves = await _gameService.GetAvailableMovesAsync(_gameId, 1);
                    bool finishNow = await Task.Run(
                        () => _botAdvisor.ShouldFinishTurn(game, moves),
                        _botCts.Token);
                    if (finishNow)
                    {
                        var early = await Task.Run(
                            () => _botAdvisor.ChooseMove(game, moves),
                            _botCts.Token);
                        if (early is not null)
                        {
                            await _gameService.WriteScoreAsync(_gameId, 1, early.Column, early.Row);
                            await RefreshAsync();
                            PulseCell(1, early.Column, early.Row);
                            await Task.Delay(320, _botCts.Token);
                            continue;
                        }
                    }

                    var diceToToggle = await Task.Run(
                        () => _botAdvisor.ChooseDiceToHold(game, moves),
                        _botCts.Token);
                    foreach (int dieIndex in diceToToggle)
                        await _gameService.ToggleHoldAsync(_gameId, 1, dieIndex);

                    var rolled = await _gameService.RollDiceAsync(_gameId, 1);
                    await AnimateBotRollAsync(rolled.Dice);
                    await RefreshAsync(rolled);
                    await Task.Delay(480, _botCts.Token);
                    continue;
                }

                var finalMoves = await _gameService.GetAvailableMovesAsync(_gameId, 1);
                var best = await Task.Run(
                    () => _botAdvisor.ChooseMove(game, finalMoves),
                    _botCts.Token);
                if (best is null) break;

                await _gameService.WriteScoreAsync(_gameId, 1, best.Column, best.Row);
                await RefreshAsync();
                await Task.Delay(320, _botCts.Token);
            }
        }
        catch (OperationCanceledException) when (_botCts.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_botCts.IsCancellationRequested) return;
            MessageBox.Show(ErrorText.From(ex), Localization.Text("L.App.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _botRunning = false;
        }
    }

    private async Task TryBotAnnounceAsync(GameDto game)
    {
        if (game.AnnouncedRow.HasValue || game.RollNumber == 0) return;

        var moves = await _gameService.GetAvailableMovesAsync(_gameId, 1);
        var candidates = await _gameService.GetAnnouncementCandidatesAsync(_gameId, 1);
        var row = await Task.Run(
            () => _botAdvisor.ChooseAnnouncement(game, moves, candidates),
            _botCts.Token);
        if (!row.HasValue) return;

        await _gameService.AnnounceAsync(_gameId, 1, row.Value);
        await RefreshAsync();
    }

    private async Task AnimateBotRollAsync(DiceSetDto dice)
    {
        await AnimateRollAsync(1, dice);
    }

    private async Task RefreshAsync(GameDto? knownGame = null)
    {
        var game = knownGame ?? await _gameService.GetGameAsync(_gameId);
        _lastGame = game;
        _diceCache[game.CurrentPlayerIndex] = game.Dice;

        _availableMoves.Clear();
        if (!game.IsGameOver && game.RollNumber > 0)
        {
            var moves = await _gameService.GetAvailableMovesAsync(_gameId, game.CurrentPlayerIndex);
            foreach (var move in moves)
            {
                _availableMoves[(game.CurrentPlayerIndex, move.Column, move.Row)] = move.PreviewScore;
            }
        }

        if (!_layoutBuilt)
        {
            BuildLayout(game);
            _layoutBuilt = true;
        }

        UpdateAll(game);

        if (game.IsGameOver)
        {
            await HandleGameOverAsync(game);
            return;
        }

        if (IsBotTurn(game)) _ = RunBotTurnAsync();
    }

    private async Task HandleGameOverAsync(GameDto game)
    {
        if (_gameOverHandled) return;
        _gameOverHandled = true;
        _botCts.Cancel();

        var message = new StringBuilder();
        foreach (var player in game.Players)
        {
            message.AppendLine(Localization.Format(
                "L.Game.PlayerResult",
                player.Name,
                player.Scoresheet.FinalScore ?? 0));
        }

        message.AppendLine();
        message.Append(game.Players.Count switch
        {
            2 => game.Result switch
            {
                GameResult.PlayerTwoWins => Localization.Format("L.Game.Winner", game.Players[1].Name),
                GameResult.Draw => L("L.Game.Draw"),
                _ => Localization.Format("L.Game.Winner", game.Players[0].Name)
            },
            _ => L("L.Game.SheetComplete")
        });

        await _gameService.DeleteGameAsync(_gameId);

        ShowPopup(L("L.Game.GameOver"), message.ToString(), (L("L.Game.Ok"), OnGameOverConfirmed));
    }

    private void OnGameOverConfirmed()
    {
        HidePopup();
        _onGameOver();
    }

    private void ShowPopup(string title, string message, params (string Label, Action Handler)[] buttons)
    {
        PopupTitle.Text = title;
        PopupText.Text = message;
        PopupButtons.Children.Clear();

        foreach (var (label, handler) in buttons)
        {
            var button = new Button
            {
                Content = label,
                Style = (Style)FindResource("SmallButtonStyle"),
                Height = 36,
                MinWidth = 92,
                Margin = new Thickness(6, 0, 6, 0)
            };
            var action = handler;
            button.Click += (_, _) => action();
            PopupButtons.Children.Add(button);
        }

        AutomationProperties.SetAutomationId(PopupTitle, "PopupTitle");
        PopupOverlay.Visibility = Visibility.Visible;
        UiAnimations.FadeIn(PopupOverlay, 0.16);
        UiAnimations.PopIn(PopupPanel);
    }

    private void HidePopup()
    {
        PopupOverlay.Visibility = Visibility.Collapsed;
    }

    private void BuildLayout(GameDto game)
    {
        bool twoPlayers = game.Players.Count > 1;
        HeaderText.Text = game.Mode switch
        {
            GameMode.VersusComputer => L("L.Game.Bot"),
            GameMode.TwoPlayer => L("L.Game.TwoPlayer"),
            _ => L("L.Game.SinglePlayer")
        };
        _columns = game.Columns;
        ContentGrid.Children.Clear();
        _sheets.Clear();
        _diceRows.Clear();
        _diceViewSignatures.Clear();

        var content = twoPlayers ? BuildTwoPlayerLayout(game) : BuildSingleLayout(game);
        ContentGrid.Children.Add(content);
        UiAnimations.FadeIn((FrameworkElement)content, 0.22);

        for (int i = 0; i < game.Players.Count; i++)
        {
            _diceCache[i] = game.Dice;
        }
    }

    private UIElement BuildSingleLayout(GameDto game)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(460) });

        var sheet = BuildSheetCard(0);
        var sheetHost = BuildSheetHost(sheet, SingleSheetWidth, SheetNaturalHeight);
        sheetHost.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(sheetHost, 0);
        grid.Children.Add(sheetHost);

        var rail = BuildControlRail();
        Grid.SetColumn(rail, 2);
        grid.Children.Add(rail);
        return grid;
    }

    private UIElement BuildTwoPlayerLayout(GameDto game)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var sheets = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        sheets.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sheets.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        sheets.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        for (int i = 0; i < 2; i++)
        {
            var card = BuildSheetCard(i);
            Grid.SetColumn(card, i * 2);
            sheets.Children.Add(card);
        }

        Grid.SetRow(sheets, 0);
        var sheetHost = BuildSheetHost(sheets, TwoSheetWidth, SheetNaturalHeight);
        root.Children.Add(sheetHost);

        var command = BuildCommandBar();
        Grid.SetRow(command, 1);
        command.Margin = new Thickness(0, 16, 0, 0);
        root.Children.Add(command);
        return root;
    }

    private const double SheetMaxScale = 1.3;
    private const double SheetNaturalHeight = 544;

    private double SingleSheetWidth => 196 + 47 * _columns.Count;

    private double TwoSheetWidth => 2 * SingleSheetWidth + 22;

    private static Viewbox BuildSheetHost(UIElement child, double naturalWidth, double naturalHeight) => new()
    {
        Child = child,
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.Both,
        MaxWidth = naturalWidth * SheetMaxScale,
        MaxHeight = naturalHeight * SheetMaxScale,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private Border BuildControlRail()
    {
        var tray = BuildDiceTray();
        tray.Margin = new Thickness(0, 12, 0, 0);

        _statusProgress = MakeStatusText("ProgressText", _accentBrush, 18);
        _statusRolls = MakeStatusText("RollsText", _mutedBrush, 12.5);
        _rollButton = MakeRollButton();

        var stack = new StackPanel();
        stack.Children.Add(MakeSectionLabel(L("L.Game.Status")));
        stack.Children.Add(_statusProgress);
        stack.Children.Add(_statusRolls);
        stack.Children.Add(_rollButton);
        stack.Children.Add(MakeSeparator(new Thickness(0, 18, 0, 18)));
        stack.Children.Add(MakeSectionLabel(L("L.Game.Dice")));
        stack.Children.Add(tray);
        stack.Children.Add(new TextBlock
        {
            Text = L("L.Game.DiceHint"),
            FontSize = 11.5,
            Foreground = _mutedBrush,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        });

        return MakeCard(stack, new Thickness(22, 20, 22, 20));
    }

    private Border BuildCommandBar()
    {
        var leftTray = BuildDiceTray();
        var rightTray = BuildDiceTray();

        _statusProgress = MakeStatusText("ProgressText", _accentBrush, 18);
        _statusRolls = MakeStatusText("RollsText", _mutedBrush, 12.5);
        _rollButton = MakeRollButton();

        var center = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0, 18, 0)
        };
        center.Children.Add(_statusProgress);
        center.Children.Add(_statusRolls);
        center.Children.Add(_rollButton);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(leftTray, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(rightTray, 2);
        grid.Children.Add(leftTray);
        grid.Children.Add(center);
        grid.Children.Add(rightTray);

        return MakeCard(grid, new Thickness(20, 14, 20, 14));
    }

    private Viewbox BuildDiceTray()
    {
        var tray = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _diceRows.Add(tray);

        return new Viewbox
        {
            Child = tray,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private Border BuildSheetCard(int playerIndex)
    {
        var view = new SheetView { PlayerIndex = playerIndex };
        _sheets.Add(view);

        var nameText = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = _textBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAutomationId(nameText, $"PlayerBarName{playerIndex}");
        view.NameText = nameText;

        var badge = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        badge.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = _accentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        badge.Children.Add(new TextBlock
        {
            Text = L("L.Game.Active"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = _accentBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        view.ActiveBadge = badge;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(nameText, 0);
        Grid.SetColumn(badge, 1);
        header.Children.Add(nameText);
        header.Children.Add(badge);

        var sheetGrid = BuildSheetGrid(view);

        var frame = new Border
        {
            Background = _sheetFrameBrush,
            BorderBrush = _borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(4),
            Child = sheetGrid
        };

        var totalText = new TextBlock
        {
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = _greenBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAutomationId(totalText, $"PlayerTotal_{playerIndex}");
        view.TotalText = totalText;

        var footer = new Grid();
        footer.Children.Add(new TextBlock
        {
            Text = L("L.Game.Result"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = _mutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        footer.Children.Add(totalText);

        var resultStrip = new Border
        {
            Background = _sumBandBrush,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 10, 0, 0),
            Child = footer
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(frame);
        stack.Children.Add(resultStrip);

        var card = MakeCard(stack, new Thickness(18, 16, 18, 14));
        card.HorizontalAlignment = HorizontalAlignment.Center;
        view.Card = card;
        return card;
    }

    private Grid BuildSheetGrid(SheetView view)
    {
        var grid = new Grid();

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        for (int c = 0; c < _columns.Count; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(47) });
        }
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        for (int r = 0; r < 6; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        for (int r = 0; r < 2; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        for (int r = 0; r < 5; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

        var headerBand = new Border
        {
            Background = _headerBandBrush,
            BorderBrush = _borderStrongBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(0),
            Margin = new Thickness(0)
        };
        Grid.SetRow(headerBand, 0);
        Grid.SetColumnSpan(headerBand, _columns.Count + 2);
        grid.Children.Add(headerBand);

        var divider = new Border
        {
            BorderBrush = _borderStrongBrush,
            BorderThickness = new Thickness(2, 0, 0, 0),
            Margin = new Thickness(0, 2, 0, 2),
            IsHitTestVisible = false
        };
        int totalColumn = _columns.Count + 1;
        Grid.SetRow(divider, 1);
        Grid.SetRowSpan(divider, grid.RowDefinitions.Count - 1);
        Grid.SetColumn(divider, totalColumn);
        grid.Children.Add(divider);

        AddHeaderText(grid, 0, 0, "");
        for (int c = 0; c < _columns.Count; c++)
        {
            var column = _columns[c];
            AddHeaderText(
                grid,
                0,
                c + 1,
                ColumnHeaders[column],
                $"{ColumnName(column)} â€“ {ColumnTooltip(column)}");
        }
        AddHeaderText(grid, 0, totalColumn, "YAMB", null, true);

        int row = 1;
        for (int r = 0; r < 6; r++)
        {
            AddRowLabel(grid, row, RowLabel(Rows[r]), RowTooltip(Rows[r]));
            AddValueRow(grid, view, row, Rows[r]);
            row++;
        }

        AddSumaRow(grid, view, row, L("L.Game.Sum"), BonusType.NumbersBonus);
        row++;

        AddRowLabel(grid, row, RowLabel(Row.Max), RowTooltip(Row.Max));
        AddValueRow(grid, view, row, Row.Max);
        row++;

        AddRowLabel(grid, row, RowLabel(Row.Min), RowTooltip(Row.Min));
        AddValueRow(grid, view, row, Row.Min);
        row++;

        AddSumaRow(grid, view, row, L("L.Game.Sum"), BonusType.MaxMinBonus);
        row++;

        for (int r = 8; r < 13; r++)
        {
            AddRowLabel(grid, row, RowLabel(Rows[r]), RowTooltip(Rows[r]));
            AddValueRow(grid, view, row, Rows[r]);
            row++;
        }

        AddSumaRow(grid, view, row, L("L.Game.Sum"), BonusType.CombinationsBonus);

        return grid;
    }

    private void AddValueRow(Grid grid, SheetView view, int row, Row rowValue)
    {
        for (int c = 0; c < _columns.Count; c++)
        {
            var column = _columns[c];
            var button = new Button
            {
                Style = (Style)FindResource("SheetCellStyle"),
                Tag = "empty",
                Focusable = true
            };
            AutomationProperties.SetAutomationId(button, $"Cell_{view.PlayerIndex}_{column}_{rowValue}");
            int playerIndex = view.PlayerIndex;
            button.Click += async (_, _) => await OnCellClick(playerIndex, column, rowValue);
            button.MouseEnter += (_, _) => ShowPreview(playerIndex, column, rowValue, button);
            button.MouseLeave += (_, _) => HidePreview(button);
            button.GotFocus += (_, _) => ShowPreview(playerIndex, column, rowValue, button);
            button.LostFocus += (_, _) => HidePreview(button);
            Grid.SetRow(button, row);
            Grid.SetColumn(button, c + 1);
            grid.Children.Add(button);
            view.Cells[(column, rowValue)] = button;
        }

        var rowTotal = new TextBlock
        {
            Foreground = _greenBrush,
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(rowTotal, row);
        Grid.SetColumn(rowTotal, _columns.Count + 1);
        grid.Children.Add(rowTotal);
        view.RowTotals[rowValue] = rowTotal;
    }

    private void AddSumaRow(Grid grid, SheetView view, int row, string label, BonusType bonusType)
    {
        var band = new Border
        {
            Background = _sumBandBrush,
            BorderBrush = _borderStrongBrush,
            BorderThickness = new Thickness(0, 1, 0, 1),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0)
        };
        Grid.SetRow(band, row);
        Grid.SetColumn(band, 0);
        Grid.SetColumnSpan(band, _columns.Count + 2);
        grid.Children.Add(band);

        AddSumaText(grid, row, 0, label, _textBrush, HorizontalAlignment.Left, new Thickness(10, 0, 0, 0));

        var cells = new Dictionary<Column, TextBlock>();
        for (int c = 0; c < _columns.Count; c++)
        {
            var column = _columns[c];
            var text = AddSumaText(grid, row, c + 1, "", _blueBrush, HorizontalAlignment.Center, new Thickness(0));
            AutomationProperties.SetAutomationId(text, $"SectionTotal_{view.PlayerIndex}_{column}_{SectionKey(bonusType)}");
            cells[column] = text;
        }

        var total = AddSumaText(
            grid,
            row,
            _columns.Count + 1,
            "",
            _greenBrush,
            HorizontalAlignment.Center,
            new Thickness(0));
        AutomationProperties.SetAutomationId(total, $"SectionGrandTotal_{view.PlayerIndex}_{SectionKey(bonusType)}");

        view.SectionTotals[bonusType] = cells;
        view.SectionGrandTotals[bonusType] = total;
    }

    private static string SectionKey(BonusType type) => type switch
    {
        BonusType.NumbersBonus => "Numbers",
        BonusType.MaxMinBonus => "MaxMin",
        BonusType.CombinationsBonus => "Combinations",
        _ => type.ToString()
    };

    private static TextBlock AddSumaText(Grid grid, int row, int column, string text, Brush foreground, HorizontalAlignment alignment, Thickness margin)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
        return block;
    }

    private void AddRowLabel(Grid grid, int row, string text, string? toolTip = null)
    {
        var band = new Border
        {
            Background = (Brush)FindResource("LabelBrush"),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0)
        };
        Grid.SetRow(band, row);
        Grid.SetColumn(band, 0);
        grid.Children.Add(band);

        var block = new TextBlock
        {
            Text = text,
            Foreground = _textBrush,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            ToolTip = toolTip
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, 0);
        grid.Children.Add(block);
    }

    private void AddHeaderText(Grid grid, int row, int column, string text, string? toolTip = null, bool accent = false)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = accent ? _accentBrush : _mutedBrush,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = toolTip
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private TextBlock MakeSectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.Bold,
        Foreground = _mutedBrush,
        Margin = new Thickness(0, 2, 0, 8)
    };

    private static Border MakeSeparator(Thickness margin) => new()
    {
        Height = 1,
        Background = (Brush)System.Windows.Application.Current.FindResource("BorderBrush"),
        Margin = margin
    };

    private Border MakeCard(UIElement content, Thickness padding) => new()
    {
        Background = _panelGradientBrush,
        BorderBrush = _borderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(18),
        Padding = padding,
        Effect = _cardShadow,
        Child = content,
        VerticalAlignment = VerticalAlignment.Top
    };

    private static TextBlock MakeStatusText(string automationId, Brush foreground, double fontSize)
    {
        var text = new TextBlock
        {
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        AutomationProperties.SetAutomationId(text, automationId);
        return text;
    }

    private Button MakeRollButton()
    {
        var button = new Button
        {
            Content = L("L.Game.Roll"),
            Style = (Style)FindResource("RollButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        };
        AutomationProperties.SetAutomationId(button, "RollButton");
        button.Click += OnRollClick;
        return button;
    }

    private void UpdateAll(GameDto game)
    {
        for (int i = 0; i < _sheets.Count; i++)
        {
            UpdateSheet(_sheets[i], game.Players[i], game);
        }

        TurnText.Text = Localization.Format("L.Game.Turn", game.Players[game.CurrentPlayerIndex].Name);
        ThinkingPill.Visibility = IsBotTurn(game) ? Visibility.Visible : Visibility.Collapsed;

        var current = game.Players[game.CurrentPlayerIndex].Scoresheet;
        if (_statusProgress is not null)
        {
            _statusProgress.Text = Localization.Format("L.Game.Progress", current.FilledCells, current.TotalCells);
        }

        if (_statusRolls is not null)
        {
            _statusRolls.Text = IsBotTurn(game) ? L("L.Game.BotThinking") : RollsText(game.RollNumber);
        }

        if (_rollButton is not null)
        {
            _rollButton.IsEnabled = game.RollNumber < MaxRollsPerTurn
                && !game.IsGameOver
                && !IsBotTurn(game);
        }

        UpdateDice(game);
    }

    private void UpdateSheet(SheetView view, PlayerDto player, GameDto game)
    {
        var sheet = player.Scoresheet;
        bool active = view.PlayerIndex == game.CurrentPlayerIndex;
        bool interactive = active && !game.IsGameOver && !IsBotTurn(game);

        view.NameText.Text = player.Name;
        view.Card.BorderBrush = active ? _accentBrush : _borderBrush;
        view.Card.BorderThickness = new Thickness(active ? 2 : 1);
        view.Card.Effect = active ? _accentGlow : _cardShadow;
        view.Card.Opacity = active ? 1.0 : 0.72;
        view.ActiveBadge.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

        foreach (var ((column, row), button) in view.Cells)
        {
            bool filled = sheet.Cells[column].TryGetValue(row, out var value) && value.HasValue;
            bool announced = active && game.AnnouncedRow == row && column == Column.Announcement && !filled;

            if (filled)
            {
                button.Content = value!.Value.ToString();
                button.Tag = "filled";
                button.IsEnabled = false;
            }
            else if (announced)
            {
                button.Content = "";
                button.Tag = "announced";
                button.IsEnabled = interactive;
            }
            else
            {
                button.Content = "";
                button.Tag = "empty";
                button.IsEnabled = interactive;
            }
        }

        foreach (var (bonusType, cells) in view.SectionTotals)
        {
            int? grandTotal = null;
            foreach (var (column, text) in cells)
            {
                var sectionTotal = GetSectionTotal(sheet, column, bonusType);
                text.Text = sectionTotal?.ToString() ?? "";
                if (sectionTotal is int value)
                    grandTotal = (grandTotal ?? 0) + value;
            }

            view.SectionGrandTotals[bonusType].Text = grandTotal?.ToString() ?? "";
        }

        foreach (var (row, text) in view.RowTotals)
        {
            text.Text = sheet.RowTotals[row]?.ToString() ?? "";
        }

        view.TotalText.Text = _autoCalculate
            ? sheet.CurrentScore.ToString()
            : (sheet.FinalScore?.ToString() ?? "");
    }

    private static int? GetSectionTotal(ScoresheetDto sheet, Column column, BonusType type)
    {
        var bonuses = sheet.Bonuses[column];
        var cells = sheet.Cells[column];
        return type switch
        {
            BonusType.NumbersBonus => bonuses.NumbersBonus ?? SumFilled(cells, NumberRows),
            BonusType.MaxMinBonus => bonuses.MaxMinBonus,
            BonusType.CombinationsBonus => bonuses.CombinationsBonus ?? SumFilled(cells, CombinationRows),
            _ => null
        };
    }

    private static int? SumFilled(IReadOnlyDictionary<Row, int?> cells, IReadOnlyList<Row> rows)
    {
        int total = 0;
        bool any = false;
        foreach (var row in rows)
        {
            if (cells.TryGetValue(row, out var value) && value.HasValue)
            {
                total += value.Value;
                any = true;
            }
        }

        return any ? total : null;
    }

    private void UpdateDice(GameDto game)
    {
        for (int p = 0; p < _diceRows.Count; p++)
        {
            var stack = _diceRows[p];
            var dice = _diceCache.TryGetValue(p, out var cached) ? cached : game.Dice;
            bool canHold = p == game.CurrentPlayerIndex
                && game.RollNumber >= 1
                && game.RollNumber < MaxRollsPerTurn
                && !game.IsGameOver
                && !IsBotTurn(game);
            string signature = $"{WpfSettings.DieSize}|{WpfSettings.FaceStyle}|{canHold}|" +
                string.Join(',', dice.Dice.Select(die => $"{die.Value}:{die.IsHeld}"));
            if (_diceViewSignatures.TryGetValue(p, out string? previous) && previous == signature)
                continue;
            _diceViewSignatures[p] = signature;

            stack.Children.Clear();
            for (int i = 0; i < dice.Dice.Count; i++)
            {
                stack.Children.Add(CreateDie(p, i, dice.Dice[i], canHold));
            }
        }
    }

    private FrameworkElement CreateDie(int playerIndex, int dieIndex, DieDto die, bool canHold)
    {
        double size = WpfSettings.DieSize;
        var face = new Border
        {
            Background = _dieBrushes[dieIndex % _dieBrushes.Length],
            CornerRadius = new CornerRadius(size * 0.18),
            Opacity = die.IsHeld ? 0.55 : 1.0,
            Effect = _dieShadow,
            Child = DiceVisuals.BuildFace(die.Value, size, WpfSettings.FaceStyle)
        };

        var content = new Grid { Width = size, Height = size };
        content.Children.Add(face);
        if (die.IsHeld)
        {
            content.Children.Add(new Border
            {
                BorderBrush = _accentBrush,
                BorderThickness = new Thickness(2.5),
                CornerRadius = new CornerRadius(size * 0.18),
                Background = _accentTintBrush
            });
        }

        var button = new Button
        {
            Style = (Style)FindResource("DieButtonStyle"),
            Content = content,
            Width = size,
            Height = size,
            Margin = new Thickness(4, 0, 4, 0),
            IsEnabled = canHold,
            Cursor = canHold ? Cursors.Hand : Cursors.Arrow,
            ToolTip = canHold ? L(die.IsHeld ? "L.Game.HoldTooltip" : "L.Game.HoldHint") : null
        };
        AutomationProperties.SetAutomationId(button, $"Die{playerIndex}_{dieIndex}");
        AutomationProperties.SetName(button, $"{DieName(dieIndex)}: {die.Value}|{die.IsHeld}");
        button.Click += async (_, _) => await OnDieClick(playerIndex, dieIndex);
        return button;
    }

    private static T AtDisplayRate<T>(T timeline) where T : Timeline
    {
        timeline.SetValue(Timeline.DesiredFrameRateProperty, DesiredFrameRate);
        return timeline;
    }

    private static Brush MakeAccentTint(Brush accent)
    {
        if (accent is not SolidColorBrush solid) return Brushes.Transparent;

        var tint = new SolidColorBrush(solid.Color) { Opacity = 0.18 };
        tint.Freeze();
        return tint;
    }

    private static string RollsText(int rolls) => rolls switch
    {
        0 => L("L.Game.Rolls0"),
        1 => L("L.Game.Rolls1"),
        _ => Localization.Format("L.Game.RollsN", rolls)
    };
}
