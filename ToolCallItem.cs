using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>
/// 工具调用卡片 — 三态视觉：运行中（蓝）/ 等待审批（黄）/ 已完成（绿）
/// </summary>
public static class ToolCallItem
{
    /// <summary>构建工具调用卡片</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="status">运行状态：运行中 / 等待审批 / 已完成</param>
    /// <param name="detail">工具输入的 JSON 内容或执行结果摘要</param>
    /// <param name="fileChanges">文件变更列表（路径、新增行数、删除行数），用于在已完成文件编辑工具卡片下方展示变更摘要</param>
    public static Border Build(
        string toolName,
        ToolCallStatus status,
        string? detail = null,
        IEnumerable<(string FilePath, int Added, int Removed)>? fileChanges = null)
    {
        var bg = ThemeManager.GetColor("ThemeToolCallBg");
        var border = ThemeManager.GetColor("ThemeToolCallBorder");

        var card = new Border
        {
            Background = bg,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(16, 2, 16, 2),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new StackPanel(),
        };

        var sp = (StackPanel)card.Child;

        // Top row: status icon + tool name + status label
        var topRow = new StackPanel { Orientation = Orientation.Horizontal };
        topRow.Children.Add(BuildStatusIcon(status));
        topRow.Children.Add(new TextBlock
        {
            Text = $"  {toolName}",
            Foreground = GetStatusColor(status),
            FontSize = 13,
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        string statusLabel = status switch
        {
            ToolCallStatus.Running => "  运行中…",
            ToolCallStatus.PendingApproval => "  等待审批",
            ToolCallStatus.Completed => "  已完成",
            _ => "",
        };
        if (!string.IsNullOrEmpty(statusLabel))
        {
            topRow.Children.Add(new TextBlock
            {
                Text = statusLabel,
                Foreground = ThemeManager.GetColor("ThemeTextMuted"),
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            });
        }

        sp.Children.Add(topRow);

        // Detail row (input JSON or result)
        if (!string.IsNullOrEmpty(detail))
        {
            var detailBox = new TextBlock
            {
                Text = detail,
                Foreground = ThemeManager.GetColor("ThemeTextSecondary"),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Courier New"),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 80,
                Margin = new Thickness(24, 4, 0, 0),
            };
            sp.Children.Add(detailBox);
        }

        // File changes summary (for completed edit/write tools)
        if (fileChanges != null)
        {
            var summary = FileSummary.Build(fileChanges);
            summary.Margin = new Thickness(24, 4, 0, 0);
            sp.Children.Add(summary);
        }

        return card;
    }

    private static Border BuildStatusIcon(ToolCallStatus status)
    {
        string text = status switch
        {
            ToolCallStatus.Running => "\u23F3",        // ⏳ hourglass
            ToolCallStatus.PendingApproval => "\u23F3", // ⏳
            ToolCallStatus.Completed => "\u2705",       // ✅ checkmark
            _ => "\u2753",                              // ❓
        };

        return new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = ThemeManager.GetColor("ThemeSkillChipBg"),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    private static SolidColorBrush GetStatusColor(ToolCallStatus status)
    {
        return status switch
        {
            ToolCallStatus.Running => ThemeManager.GetColor("ThemeToolCallRunning"),
            ToolCallStatus.PendingApproval => ThemeManager.GetColor("ThemeToolCallPending"),
            ToolCallStatus.Completed => ThemeManager.GetColor("ThemeToolCallDone"),
            _ => ThemeManager.GetColor("ThemeTextPrimary"),
        };
    }
}
