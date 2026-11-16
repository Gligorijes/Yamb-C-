using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Yamb.Presentation.Wpf;

public static class DiceVisuals
{
    private static readonly Brush FaceInk = CreateInk();

    public static FrameworkElement BuildFace(int value, double size, DiceFaceStyle style)
    {
        if (style == DiceFaceStyle.Numbers)
            return MakeFaceText(value.ToString(), size * 0.44);
        if (style == DiceFaceStyle.Roman)
            return MakeFaceText(Roman(value), size * 0.32);

        var grid = new Grid { Margin = new Thickness(size * 0.16) };
        for (int i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
        }

        foreach (var (r, c) in PipPositions(value))
        {
            var dot = new Ellipse
            {
                Width = size * 0.155,
                Height = size * 0.155,
                Fill = FaceInk,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(dot, r);
            Grid.SetColumn(dot, c);
            grid.Children.Add(dot);
        }

        return grid;
    }

    private static TextBlock MakeFaceText(string text, double fontSize) => new()
    {
        Text = text,
        Foreground = FaceInk,
        FontWeight = FontWeights.Bold,
        FontSize = fontSize,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static string Roman(int value) => value switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        5 => "V",
        6 => "VI",
        _ => value.ToString()
    };

    private static IEnumerable<(int Row, int Column)> PipPositions(int value) => value switch
    {
        1 => new[] { (1, 1) },
        2 => new[] { (0, 0), (2, 2) },
        3 => new[] { (0, 0), (1, 1), (2, 2) },
        4 => new[] { (0, 0), (0, 2), (2, 0), (2, 2) },
        5 => new[] { (0, 0), (0, 2), (1, 1), (2, 0), (2, 2) },
        6 => new[] { (0, 0), (0, 2), (1, 0), (1, 2), (2, 0), (2, 2) },
        _ => Array.Empty<(int, int)>()
    };

    private static Brush CreateInk()
    {
        var brush = new SolidColorBrush(Color.FromRgb(20, 22, 30));
        brush.Freeze();
        return brush;
    }
}
