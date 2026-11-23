using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Yamb.Presentation.Wpf;

public static class UiAnimations
{
    public static void FadeIn(FrameworkElement element, double seconds = 0.2)
    {
        element.Opacity = 1;
        var animation = AtDisplayRate(new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public static void PopIn(FrameworkElement element, double seconds = 0.24)
    {
        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;
        element.Opacity = 1;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var bounce = AtDisplayRate(new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop });
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1.015, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds * 0.7)))
        {
            EasingFunction = ease
        });
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)))
        {
            EasingFunction = ease
        });

        var fade = AtDisplayRate(new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds * 0.75))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        });

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce.Clone());
    }

    public static void Pulse(FrameworkElement element)
    {
        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = AtDisplayRate(new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(85)))
        {
            EasingFunction = ease
        });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))
        {
            EasingFunction = ease
        });

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
    }

    public static void Shake(FrameworkElement element)
    {
        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;

        var animation = AtDisplayRate(new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(225))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(290))));

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    public static void SlideIn(FrameworkElement element)
    {
        var translate = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = group;
        element.Opacity = 1;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = AtDisplayRate(new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        });
        var slide = AtDisplayRate(new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        });
        var grow = AtDisplayRate(new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        });
        var growY = grow.Clone();

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, growY);
    }

    public static T AtDisplayRate<T>(T timeline) where T : Timeline
    {
        timeline.SetValue(Timeline.DesiredFrameRateProperty, WpfSettings.AnimationFps);
        return timeline;
    }
}
