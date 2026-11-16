using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Yamb.Presentation.Wpf;

public static class UiAnimations
{
    public static void FadeIn(FrameworkElement element, double seconds = 0.22)
    {
        element.Opacity = 1;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public static void PopIn(FrameworkElement element, double seconds = 0.26)
    {
        var transform = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = transform;
        element.Opacity = 1;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scale = new DoubleAnimation(0.92, 1, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds * 0.8))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale.Clone());
    }

    public static void Pulse(FrameworkElement element)
    {
        var transform = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = transform;

        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))));

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
    }

    public static void Shake(FrameworkElement element)
    {
        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;

        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(55))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(115))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(175))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(235))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(290))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    public static void SlideIn(FrameworkElement element)
    {
        var transform = new TranslateTransform(0, 0);
        element.RenderTransform = transform;
        element.Opacity = 1;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(230))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
