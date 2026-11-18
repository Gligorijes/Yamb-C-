using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Yamb.Presentation.Wpf.Views;

public partial class SettingsView : UserControl
{
    private readonly Action _onBack;
    private readonly Effect _dieShadow;
    private readonly Thickness _dieMargin = new(4);
    private readonly Dictionary<AppTheme, RadioButton> _themeRadios = new();
    private bool _loaded;

    public SettingsView(Action onBack)
    {
        InitializeComponent();
        _onBack = onBack;

        _dieShadow = (Effect)FindResource("DieShadow");
        BuildThemeCards();
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

    private void BuildThemeCards()
    {
        ThemeGrid.Children.Clear();
        _themeRadios.Clear();

        for (int i = 0; i < ThemeManager.Themes.Count; i++)
        {
            var info = ThemeManager.Themes[i];
            var radio = new RadioButton
            {
                GroupName = "Theme",
                Height = 62,
                Padding = new Thickness(12, 9, 9, 9),
                Margin = new Thickness(0),
                Style = (Style)FindResource("ModeCardStyle"),
                Tag = info.Theme
            };
            AutomationProperties.SetAutomationId(radio, info.AutomationId);
            radio.Checked += OnThemeChanged;

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var accent = ParseBrush(info.AccentColor);
            var tile = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(12),
                Background = ParseBrush(info.SurfaceColor),
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center
            };

            var tileGrid = new Grid();
            tileGrid.Children.Add(new Border
            {
                Height = 4,
                Background = accent,
                VerticalAlignment = VerticalAlignment.Bottom
            });
            tileGrid.Children.Add(new Ellipse
            {
                Width = 16,
                Height = 16,
                Fill = accent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 5, 0)
            });
            tile.Child = tileGrid;

            var title = new TextBlock
            {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold
            };
            title.SetResourceReference(TextBlock.TextProperty, info.NameKey);

            var subtitle = new TextBlock
            {
                FontSize = 10.5,
                Foreground = (Brush)FindResource("MutedTextBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            };
            subtitle.SetResourceReference(TextBlock.TextProperty, info.DescriptionKey);

            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(title);
            labels.Children.Add(subtitle);

            Grid.SetColumn(tile, 0);
            Grid.SetColumn(labels, 2);
            content.Children.Add(tile);
            content.Children.Add(labels);
            radio.Content = content;

            Grid.SetColumn(radio, i % 3 == 0 ? 0 : i % 3 == 1 ? 2 : 4);
            Grid.SetRow(radio, i < 3 ? 0 : 2);
            ThemeGrid.Children.Add(radio);
            _themeRadios[info.Theme] = radio;
        }
    }

    private static Brush ParseBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private void SetControlsFromSettings()
    {
        foreach (var pair in _themeRadios)
        {
            pair.Value.IsChecked = WpfSettings.Theme == pair.Key;
        }

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
        if (!_loaded || sender is not RadioButton radio || radio.Tag is not AppTheme theme) return;
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
        double size = Math.Min(WpfSettings.DieSize, 48);

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
