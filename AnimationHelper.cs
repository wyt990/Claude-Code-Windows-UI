using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodeGUI;

/// <summary>
/// 动画辅助方法 — 入场动效、淡入、宽度动画
/// </summary>
public static class AnimationHelper
{
    /// <summary>淡入 + 上滑入场动画（聊天气泡）</summary>
    public static void Entrance(UIElement element, double delayMs = 0, double durationMs = 200)
    {
        element.Opacity = 0;
        element.RenderTransform = new TranslateTransform { Y = 10 };

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        // 淡入
        var fadeIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        element.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        // 上滑
        var slideUpTransform = element.RenderTransform as TranslateTransform;
        if (slideUpTransform != null)
        {
            var slideUp = new DoubleAnimation(10, 0, duration) { EasingFunction = ease };
            slideUpTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }
    }

    /// <summary>淡入动画（对话框）</summary>
    public static void FadeIn(UIElement element, double durationMs = 200)
    {
        element.Opacity = 0;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    /// <summary>宽度动画（侧边栏折叠/展开）</summary>
    public static void AnimateWidth(FrameworkElement element, double from, double to, double durationMs = 200)
    {
        element.Width = from;

        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };
        element.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }
}
