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

        _loaded = true;
        RenderPreview();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        var theme = ThemeLightRadio.IsChecked == true ? AppTheme.Light : AppTheme.Dark;
        if (WpfSettings.Theme == theme && ThemeManager.Current == theme) return;

        WpfSettings.Theme = theme;
        WpfSettings.Save();
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
        WpfSettings.Save();
        RenderPreview();
    }

    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        var language = LanguageEnglishRadio.IsChecked == true ? AppLanguage.English : AppLanguage.Serbian;
        if (WpfSettings.Language == language) return;

        WpfSettings.Language = language;
        WpfSettings.Save();
        Localization.Apply(language);
    }

    private void OnAutoCalculateChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        WpfSettings.AutoCalculate = AutoCalculateCheck.IsChecked == true;
        WpfSettings.Save();
    }

    private void OnDiceCountChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        WpfSettings.DiceCount = DiceFiveRadio.IsChecked == true ? 5 : 6;
        WpfSettings.Save();
        RenderPreview();
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;

        WpfSettings.DieSize = Math.Round(e.NewValue);
        if (SizeText is not null) SizeText.Text = $"{(int)WpfSettings.DieSize} px";
        WpfSettings.Save();
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

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _onBack();
    }
}
