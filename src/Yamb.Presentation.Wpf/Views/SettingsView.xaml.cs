using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Yamb.Presentation.Wpf.Views;

public partial class SettingsView : UserControl
{
    private readonly Action _onBack;
    private readonly Effect _dieShadow;
    private readonly Thickness _dieMargin = new(4);
    private bool _loaded;

    public SettingsView(Action onBack)
    {
        InitializeComponent();
        _onBack = onBack;

        _dieShadow = (Effect)FindResource("DieShadow");
        SetControlsFromSettings();

        _loaded = true;
        RenderPreview();

        if (WpfSettings.LastError is not null)
        {
            Loaded += (_, _) => MessageBox.Show(
                Window.GetWindow(this),
                Localization.Format("L.Settings.LoadError", WpfSettings.LastError),
                Localization.Text("L.Settings.LoadErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SetControlsFromSettings()
    {
        ThemeDarkRadio.IsChecked = WpfSettings.Theme == AppTheme.Dark;
        ThemeLightRadio.IsChecked = WpfSettings.Theme == AppTheme.Light;
        PipsRadio.IsChecked = WpfSettings.FaceStyle == DiceFaceStyle.Pips;
        NumbersRadio.IsChecked = WpfSettings.FaceStyle == DiceFaceStyle.Numbers;
        RomanRadio.IsChecked = WpfSettings.FaceStyle == DiceFaceStyle.Roman;
        LanguageSerbianRadio.IsChecked = WpfSettings.Language == AppLanguage.Serbian;
        LanguageEnglishRadio.IsChecked = WpfSettings.Language == AppLanguage.English;
        AutoCalculateCheck.IsChecked = WpfSettings.AutoCalculate;
        DiceFiveRadio.IsChecked = WpfSettings.DiceCount == 5;
        DiceSixRadio.IsChecked = WpfSettings.DiceCount == 6;
        SizeSlider.Value = WpfSettings.DieSize;
        SizeText.Text = $"{(int)WpfSettings.DieSize} px";
    }

    private void SaveSettings()
    {
        if (WpfSettings.Save()) return;

        MessageBox.Show(
            Window.GetWindow(this),
            Localization.Format("L.Settings.SaveError", WpfSettings.LastError ?? ""),
            Localization.Text("L.Settings.SaveErrorTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        var theme = ThemeLightRadio.IsChecked == true ? AppTheme.Light : AppTheme.Dark;
        if (WpfSettings.Theme == theme && ThemeManager.Current == theme) return;

        WpfSettings.Theme = theme;
        SaveSettings();
        ThemeManager.Apply(theme);
        RenderPreview();
    }

    private void OnFaceStyleChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        WpfSettings.FaceStyle = NumbersRadio.IsChecked == true
            ? DiceFaceStyle.Numbers
            : RomanRadio.IsChecked == true
                ? DiceFaceStyle.Roman
                : DiceFaceStyle.Pips;
        SaveSettings();
        RenderPreview();
    }

    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        var language = LanguageEnglishRadio.IsChecked == true ? AppLanguage.English : AppLanguage.Serbian;
        if (WpfSettings.Language == language) return;

        WpfSettings.Language = language;
        SaveSettings();
        Localization.Apply(language);
    }

    private void OnAutoCalculateChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        WpfSettings.AutoCalculate = AutoCalculateCheck.IsChecked == true;
        SaveSettings();
    }

    private void OnDiceCountChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        WpfSettings.DiceCount = DiceFiveRadio.IsChecked == true ? 5 : 6;
        SaveSettings();
        RenderPreview();
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;

        WpfSettings.DieSize = Math.Round(e.NewValue);
        if (SizeText is not null) SizeText.Text = $"{(int)WpfSettings.DieSize} px";
        SaveSettings();
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (PreviewPanel is null) return;

        PreviewPanel.Children.Clear();
        double size = Math.Min(WpfSettings.DieSize, 52);

        var brushes = new[]
        {
            (Brush)FindResource("DieMagentaBrush"),
            (Brush)FindResource("DieYellowBrush"),
            (Brush)FindResource("DieBlueBrush"),
            (Brush)FindResource("DieGreenBrush"),
            (Brush)FindResource("DieRedBrush"),
            (Brush)FindResource("DieWhiteBrush")
        };

        for (int value = 1; value <= WpfSettings.DiceCount; value++)
        {
            var face = new Border
            {
                Width = size,
                Height = size,
                Margin = _dieMargin,
                CornerRadius = new CornerRadius(size * 0.18),
                Background = brushes[(value - 1) % brushes.Length],
                Effect = _dieShadow,
                Child = DiceVisuals.BuildFace(value, size, WpfSettings.FaceStyle)
            };
            PreviewPanel.Children.Add(face);
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var confirmed = MessageBox.Show(
            Window.GetWindow(this),
            Localization.Text("L.Settings.ResetConfirm"),
            Localization.Text("L.Settings.ResetTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        if (!confirmed) return;

        _loaded = false;
        WpfSettings.Reset();
        var saved = WpfSettings.Save();
        ThemeManager.Apply(WpfSettings.Theme);
        Localization.Apply(WpfSettings.Language);
        SetControlsFromSettings();
        _loaded = true;
        RenderPreview();

        if (!saved)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                Localization.Format("L.Settings.SaveError", WpfSettings.LastError ?? ""),
                Localization.Text("L.Settings.SaveErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _onBack();
    }
}
