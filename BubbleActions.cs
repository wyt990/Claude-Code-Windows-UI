using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>
/// 聊天气泡悬停操作按钮 — 复制 / 重试
/// </summary>
public static class BubbleActions
{
    private static readonly Thickness ButtonMargin = new(4, 0, 0, 0);
    private const double ButtonFontSize = 11;

    /// <summary>
    /// 在气泡的 StackPanel 底部添加操作按钮（初始隐藏，悬停显示）
    /// </summary>
    /// <param name="panel">气泡内部的 StackPanel</param>
    /// <param name="getText">获取待复制文本的回调（支持流式写入后返回最终文本）</param>
    /// <param name="isUser">是否为用户消息（用户消息额外显示重试按钮）</param>
    /// <param name="onRetry">重试回调（用户消息时使用）</param>
    /// <param name="bubbleBorder">气泡外层 Border。传入后可避免视觉树查找失败（气泡未挂载到视觉树时 FindParentBorder 会返回 null）</param>
    public static void AddActions(StackPanel panel, Func<string> getText, bool isUser, Action? onRetry = null, Border? bubbleBorder = null)
    {
        var actionsBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = isUser ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            Margin = new Thickness(0),
            Visibility = Visibility.Collapsed,
        };

        // 复制按钮
        Border copyBtn = null!;
        copyBtn = BuildActionButton("复制", (_, _) =>
        {
            try
            {
                Clipboard.SetText(getText());
                SetButtonText(copyBtn, "已复制");
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5),
                    IsEnabled = true,
                };
                timer.Tick += (_, _) =>
                {
                    SetButtonText(copyBtn, "复制");
                    timer.Stop();
                };
                timer.Start();
            }
            catch { }
        });
        actionsBar.Children.Add(copyBtn);

        // 重试按钮（仅用户消息）
        if (isUser && onRetry != null)
        {
            var retryBtn = BuildActionButton("重试", (_, _) => onRetry());
            actionsBar.Children.Add(retryBtn);
        }

        // 查找气泡的外层 Border
        bubbleBorder ??= FindParentBorder(panel);
        if (bubbleBorder == null) return;

        // 利用 Grid 单单元格 overlay 实现浮层按钮（不占用布局高度）
        bool useOverlay = false;
        if (panel.Parent is Grid grid && grid.Children.Count >= 1)
        {
            // 移除之前的 Margin.Top，确保按钮浮在左下角不改变气泡高度
            actionsBar.VerticalAlignment = VerticalAlignment.Bottom;
            actionsBar.Background = new SolidColorBrush(Color.FromArgb(220, 0x0D, 0x11, 0x17));
            grid.Children.Add(actionsBar);
            useOverlay = true;
        }

        // fallback：非 Grid 结构时追加到面板（会改变高度，但保持可用）
        if (!useOverlay)
            panel.Children.Add(actionsBar);

        // 悬停显示/隐藏
        bubbleBorder.MouseEnter += (_, _) => actionsBar.Visibility = Visibility.Visible;
        bubbleBorder.MouseLeave += (_, _) => actionsBar.Visibility = Visibility.Collapsed;
    }

    private static Border BuildActionButton(string text, RoutedEventHandler clickHandler)
    {
        var btn = new Border
        {
            Background = ThemeManager.GetColor("ThemeSurface3"),
            BorderBrush = ThemeManager.GetColor("ThemeBorder1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = ButtonMargin,
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = text,
                Foreground = ThemeManager.GetColor("ThemeTextSecondary"),
                FontSize = ButtonFontSize,
                FontFamily = new FontFamily("Segoe UI"),
            },
        };

        btn.MouseLeftButtonDown += (s, e) => clickHandler(s, e);

        // 悬停效果
        btn.MouseEnter += (_, _) =>
        {
            btn.Background = ThemeManager.GetColor("ThemeSurface4");
        };
        btn.MouseLeave += (_, _) =>
        {
            btn.Background = ThemeManager.GetColor("ThemeSurface3");
        };

        return btn;
    }

    /// <summary>更新 Border 内部 TextBlock 的文本</summary>
    private static void SetButtonText(Border btn, string text)
    {
        if (btn.Child is TextBlock tb)
            tb.Text = text;
    }

    /// <summary>从 StackPanel 向上查找父级 Border</summary>
    private static Border? FindParentBorder(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is Border border)
                return border;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
