using System.Windows;
using System.Windows.Controls;
using Yamb.Application.DTOs;
using Yamb.Domain.Enums;

namespace Yamb.Presentation.Wpf.Views;

public partial class NewGameView : UserControl
{
    private readonly Action<StartGameCommand> _onStart;
    private readonly Action _onBack;
    private int _step = 1;

    public NewGameView(Action<StartGameCommand> onStart, Action onBack)
    {
        InitializeComponent();
        _onStart = onStart;
        _onBack = onBack;
        Player1Box.Text = Localization.Text("L.New.DefaultPlayer1");
        Player2Box.Text = Localization.Text("L.New.DefaultPlayer2");
        UpdateStep();
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (Player2Group is null) return;
        bool twoPlayers = TwoRadio.IsChecked == true;
        Player2Box.IsEnabled = twoPlayers;
        Player2Group.Visibility = twoPlayers ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_step >= 3) return;
        _step++;
        UpdateStep();
    }

    private void OnBackStepClick(object sender, RoutedEventArgs e)
    {
        if (_step <= 1)
        {
            _onBack();
            return;
        }

        _step--;
        UpdateStep();
    }

    private void UpdateStep()
    {
        StepOnePanel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepTwoPanel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepThreePanel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        StepText.Text = Localization.Format("L.New.StepCount", _step);
        BackButton.Content = Localization.Text(_step == 1 ? "L.New.Back" : "L.New.Previous");
        NextButton.Visibility = _step < 3 ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (_step == 3)
        {
            OnModeChanged(this, new RoutedEventArgs());
        }
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        var mode = TwoRadio.IsChecked == true ? GameMode.TwoPlayer : GameMode.SinglePlayer;

        var names = new List<string>
        {
            Normalize(Player1Box.Text, Localization.Text("L.New.DefaultPlayer1"))
        };
        if (mode == GameMode.TwoPlayer)
        {
            names.Add(Normalize(Player2Box.Text, Localization.Text("L.New.DefaultPlayer2")));
        }

        _onStart(new StartGameCommand(mode, names, GetSelectedVariant(), WpfSettings.DiceCount));
    }

    private SheetVariant GetSelectedVariant()
    {
        if (ColumnFourRadio.IsChecked == true) return SheetVariant.Four;
        if (ColumnSixRadio.IsChecked == true) return SheetVariant.Six;
        if (ColumnEightRadio.IsChecked == true) return SheetVariant.Eight;
        if (ColumnNineRadio.IsChecked == true) return SheetVariant.Nine;
        if (ColumnTenRadio.IsChecked == true) return SheetVariant.Ten;
        return SheetVariant.Seven;
    }

    private static string Normalize(string value, string fallback)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? fallback : trimmed;
    }
}
