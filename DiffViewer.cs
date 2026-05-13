using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

public enum DiffLineType { Added, Removed, Context }

public class DiffLine
{
    public DiffLineType Type { get; set; }
    public string Content { get; set; } = "";

    public DiffLine(DiffLineType type, string content)
    {
        Type = type;
        Content = content;
    }
}

public static class DiffViewer
{
    public static Border BuildInlineDiff(List<DiffLine> lines)
    {
        var container = new StackPanel
        {
            Background = ThemeManager.GetColor("ThemeCodeBg"),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var addedBg = ThemeManager.GetColor("ThemeDiffAdded");
        var removedBg = ThemeManager.GetColor("ThemeDiffRemoved");

        // Create semi-transparent backgrounds for diff lines
        var addedBrush = new SolidColorBrush(addedBg.Color);
        addedBrush.Opacity = 0.15;
        var removedBrush = new SolidColorBrush(removedBg.Color);
        removedBrush.Opacity = 0.15;

        var textPrimary = ThemeManager.GetColor("ThemeTextPrimary");
        var textMuted = ThemeManager.GetColor("ThemeTextMuted");

        foreach (var line in lines)
        {
            var prefix = line.Type switch
            {
                DiffLineType.Added => "+ ",
                DiffLineType.Removed => "- ",
                _ => "  "
            };

            var textBlock = new TextBlock
            {
                Text = prefix + line.Content,
                FontFamily = new FontFamily("Consolas, monospace"),
                FontSize = 12,
                Padding = new Thickness(8, 1, 8, 1),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            switch (line.Type)
            {
                case DiffLineType.Added:
                    textBlock.Background = addedBrush;
                    textBlock.Foreground = addedBg;
                    break;
                case DiffLineType.Removed:
                    textBlock.Background = removedBrush;
                    textBlock.Foreground = removedBg;
                    break;
                default:
                    textBlock.Foreground = textPrimary;
                    break;
            }

            container.Children.Add(textBlock);
        }

        var border = new Border
        {
            Child = container,
            Background = ThemeManager.GetColor("ThemeCodeBg"),
            BorderBrush = ThemeManager.GetColor("ThemeCodeBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0)
        };

        return border;
    }
}
