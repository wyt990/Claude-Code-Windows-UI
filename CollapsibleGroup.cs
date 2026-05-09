using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>
/// 可折叠面板 — 带标题栏和展开/折叠切换的内容容器
/// </summary>
public static class CollapsibleGroup
{
    /// <summary>构建可折叠面板</summary>
    /// <param name="title">标题文本</param>
    /// <param name="content">子内容元素</param>
    /// <param name="count">可选的计数徽标（如工具调用数量）</param>
    /// <param name="expanded">初始展开状态，默认 true</param>
    public static Border Build(string title, UIElement content, int? count = null, bool expanded = true)
    {
        var bg = ThemeManager.GetColor("ThemeToolCallBg");
        var border = ThemeManager.GetColor("ThemeToolCallBorder");

        // ── Content area ──
        var contentBorder = new Border
        {
            Child = content,
            Padding = new Thickness(4, 0, 4, 4),
            Visibility = expanded ? Visibility.Visible : Visibility.Collapsed,
        };

        // ── Toggle arrow ──
        var arrow = new TextBlock
        {
            Text = expanded ? "\u25BC" : "\u25B6",  // ▼ / ▶
            FontSize = 10,
            Foreground = ThemeManager.GetColor("ThemeTextMuted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };

        // ── Title ──
        var titleBlock = new TextBlock
        {
            Text = title,
            Foreground = ThemeManager.GetColor("ThemeTextPrimary"),
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // ── Count badge ──
        TextBlock? countBlock = null;
        if (count.HasValue)
        {
            countBlock = new TextBlock
            {
                Text = $"({count.Value})",
                Foreground = ThemeManager.GetColor("ThemeTextMuted"),
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            };
        }

        // ── Header row ──
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
        headerStack.Children.Add(arrow);
        headerStack.Children.Add(titleBlock);
        if (countBlock != null)
            headerStack.Children.Add(countBlock);

        var headerBorder = new Border
        {
            Background = ThemeManager.GetColor("ThemeSurface2"),
            Padding = new Thickness(10, 6, 10, 6),
            Cursor = Cursors.Hand,
            Child = headerStack,
        };

        // ── Toggle on click ──
        headerBorder.MouseLeftButtonDown += (_, _) =>
        {
            bool isVisible = contentBorder.Visibility == Visibility.Visible;
            contentBorder.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            arrow.Text = isVisible ? "\u25B6" : "\u25BC";  // ▶ / ▼
        };

        // ── Outer container ──
        var stack = new StackPanel();
        stack.Children.Add(headerBorder);
        stack.Children.Add(contentBorder);

        return new Border
        {
            Background = bg,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(16, 4, 16, 4),
            ClipToBounds = true,
            Child = stack,
        };
    }
}
