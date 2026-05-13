using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>
/// 变更文件摘要 — 文件路径 + +N/-N diff 统计
/// </summary>
public static class FileSummary
{
    /// <summary>构建文件变更摘要面板</summary>
    /// <param name="files">文件路径、新增行数、删除行数</param>
    public static Border Build(IEnumerable<(string FilePath, int Added, int Removed)> files)
    {
        var outer = new Border
        {
            Background = ThemeManager.GetColor("ThemeCodeBg"),
            BorderBrush = ThemeManager.GetColor("ThemeCodeBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(16, 4, 16, 4),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new StackPanel(),
        };

        var sp = (StackPanel)outer.Child;

        // ── Header ──
        var header = new TextBlock
        {
            Text = "修改文件",
            Foreground = ThemeManager.GetColor("ThemeTextPrimary"),
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        sp.Children.Add(header);

        // ── File rows ──
        foreach (var (filePath, added, removed) in files)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Margin = new Thickness(0, 2, 0, 2),
            };

            var pathTb = new TextBlock
            {
                Text = filePath,
                Foreground = ThemeManager.GetColor("ThemeTextSecondary"),
                FontSize = 13,
                FontFamily = new FontFamily("Consolas, Courier New"),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
            };

            // 点击文件路径展开/折叠 diff 视图
            string capturedPath = filePath;
            pathTb.MouseLeftButtonDown += (s, e) =>
            {
                var block = (TextBlock)s;
                DependencyObject parent = block;
                while (parent != null && !(parent is StackPanel))
                    parent = VisualTreeHelper.GetParent(parent);
                if (parent is StackPanel container)
                {
                    var existing = container.Children
                        .OfType<Border>()
                        .FirstOrDefault(b => b.Tag?.ToString()?.StartsWith("DiffPanel:") == true);
                    if (existing != null)
                        container.Children.Remove(existing);
                    else
                    {
                        var diffLines = new List<DiffLine>
                        {
                            new(DiffLineType.Removed, "原始代码行"),
                            new(DiffLineType.Added, "修改后的代码行"),
                        };
                        var diffPanel = DiffViewer.BuildInlineDiff(diffLines);
                        diffPanel.Tag = "DiffPanel:" + capturedPath;
                        container.Children.Add(diffPanel);
                    }
                }
            };

            var statsTb = new TextBlock
            {
                FontSize = 13,
                FontFamily = new FontFamily("Consolas, Courier New"),
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (added > 0)
            {
                statsTb.Inlines.Add(new Run($" +{added}")
                {
                    Foreground = ThemeManager.GetColor("ThemeDiffAdded"),
                });
            }

            if (removed > 0)
            {
                statsTb.Inlines.Add(new Run($" -{removed}")
                {
                    Foreground = ThemeManager.GetColor("ThemeDiffRemoved"),
                });
            }

            Grid.SetColumn(pathTb, 0);
            Grid.SetColumn(statsTb, 1);
            row.Children.Add(pathTb);
            row.Children.Add(statsTb);

            sp.Children.Add(row);
        }

        return outer;
    }
}
