using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeCodeGUI;

/// <summary>聊天气泡构建方法 — 基于 TimelineModel 数据渲染</summary>
public partial class MainWindow : Window
{
    private void ShowWelcome()
    {
        var b = Bubble(BrBgAst, BrBdrA, false);
        var p = GetContentPanel(b);
        p.Children.Add(new TextBlock
        {
            Text = "Claude Code  ·  GUI Shell",
            Foreground = BrOrange, FontSize = 15, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 0, 0, 8)
        });
        foreach (var (t, c) in new (string, SolidColorBrush)[]
        {
            ("Enter  →  发送   ·   Shift+Enter  →  换行", BrDim),
            ("技能面板  →  点击 +File / +Dir 创建技能", BrDim),
            ("右键点击技能  →  使用技能 插入到输入框", BrDim),
        })
            p.Children.Add(new TextBlock
            {
                Text = t, Foreground = c, FontSize = 13,
                FontFamily = new FontFamily("Consolas, Courier New"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1)
            });
        ChatPanel.Children.Add(b);
        SetBubbleMaxWidth(b);
        HookBubbleWidthResize();
    }

    // ── 基于 TimelineModel 的渲染 ─────────────────────────

    /// <summary>从 TimelineModel 重新渲染整个聊天面板</summary>
    public void RenderTimelineToPanel(TimelineModel timeline)
    {
        ChatPanel.Children.Clear();
        foreach (var entry in timeline.Entries)
        {
            var element = CreateBubbleFromEntry(entry);
            if (element != null)
            {
                ChatPanel.Children.Add(element);
                if (element is Border b)
                    SetBubbleMaxWidth(b);
            }
        }
        HookBubbleWidthResize();
        OutputScroller.ScrollToBottom();
    }

    /// <summary>根据 TimelineEntry 创建 UI 元素</summary>
    public UIElement? CreateBubbleFromEntry(TimelineEntry entry)
    {
        switch (entry.Kind)
        {
            case TimelineEntryKind.Message:
                return BuildMessageBubble(entry);
            case TimelineEntryKind.ToolCall:
                return BuildToolCallBubble(entry);
            case TimelineEntryKind.Divider:
                return BuildDivider();
            case TimelineEntryKind.System:
                return new TextBlock
                {
                    Text = entry.Text, Foreground = BrMuted, FontSize = 12,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 1, 16, 1)
                };
            default:
                return null;
        }
    }

    /// <summary>构建消息气泡</summary>
    private Border BuildMessageBubble(TimelineEntry entry)
    {
        bool isUser = entry.Role == "user";
        var b = Bubble(
            isUser ? BrBgUser : BrBgAst,
            isUser ? BrBdrH : BrBdrA, isUser);
        var p = GetContentPanel(b);

        // 头部：角色 + 时间
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        hdr.Children.Add(new TextBlock
        {
            Text = isUser ? "你" : "Claude",
            Foreground = isUser ? BrUser : BrAst,
            FontSize = 12, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold
        });
        hdr.Children.Add(new TextBlock
        {
            Text = $"  {entry.CreatedAt:HH:mm}", Foreground = BrMuted,
            FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
        });
        if (!isUser && _hasContinue)
            hdr.Children.Add(new TextBlock
            {
                Text = "  ·  继续中", Foreground = BrMuted,
                FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
            });
        p.Children.Add(hdr);

        // 内容（支持代码围栏分段）
        BuildMessageContent(p, entry.Text);

        // 恢复的气泡也加上悬停复制按钮（气泡不在视觉树中，需传入 b 作为 bubbleBorder）
        BubbleActions.AddActions(p, () => entry.Text, isUser, bubbleBorder: b);

        return b;
    }

    /// <summary>构建分割线</summary>
    private static Border BuildDivider()
    {
        return new Border
        {
            BorderBrush = H("#21262D"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(12, 6, 12, 6)
        };
    }

    /// <summary>构建工具调用气泡</summary>
    private static Border BuildToolCallBubble(TimelineEntry entry)
    {
        var fileChanges = ExtractFileChanges(entry);
        return ToolCallItem.Build(
            entry.ToolName ?? "tool",
            entry.ToolStatus,
            entry.Text,
            fileChanges);
    }

    /// <summary>从已完成文件编辑工具的结果中提取文件变更信息</summary>
    private static IEnumerable<(string FilePath, int Added, int Removed)>? ExtractFileChanges(TimelineEntry entry)
    {
        if (entry.ToolStatus != ToolCallStatus.Completed || string.IsNullOrEmpty(entry.Text))
            return null;

        string name = entry.ToolName ?? "";
        if (name is not ("edit_file" or "write_file"))
            return null;

        var paths = new List<string>();
        string text = entry.Text;

        // Try JSON path format: "path":"..." or "file_path":"..."
        var jsonMatch = Regex.Match(text, """["'](?:path|file_path)["']\s*:\s*["']([^"']+)["']""");
        if (jsonMatch.Success)
        {
            paths.Add(jsonMatch.Groups[1].Value);
        }
        else
        {
            // Try free-text format: edited/created/modified ... file/path
            var textMatch = Regex.Match(text,
                @"(?:edited|created|modified|wrote|writes?)\s+(?:file\s+)?(?:(?:`?)([^\s,;.`]+(?:\\|\/)[^\s,;.`]+))",
                RegexOptions.IgnoreCase);
            if (textMatch.Success)
                paths.Add(textMatch.Groups[1].Value);
        }

        if (paths.Count == 0)
        {
            // Fallback: keep the truncated detail text as a textual reference only
            return null;
        }

        return paths.Select(p => (p, 0, 0));
    }

    // ── 传统方法（兼容 — 同时写入 TimelineModel） ─────────

    /// <summary>添加用户气泡（同时写入 TimelineModel）</summary>
    private void AddUserBubble(string text)
    {
        // 写入 TimelineModel
        var entry = _sessionTabTimeline?.AddUserMessage(text);

        // 渲染
        var b = Bubble(BrBgUser, BrBdrH, true);
        var p = GetContentPanel(b);
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        hdr.Children.Add(new TextBlock
        {
            Text = "你", Foreground = BrUser,
            FontSize = 12, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold
        });
        hdr.Children.Add(new TextBlock
        {
            Text = $"  {DateTime.Now:HH:mm}", Foreground = BrMuted,
            FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
        });
        p.Children.Add(hdr);
        foreach (var ln in text.Split('\n'))
            p.Children.Add(new TextBlock
            {
                Text = ln.TrimEnd('\r'), Foreground = BrDefault,
                FontSize = 14, FontFamily = new FontFamily("Consolas, Courier New"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0)
            });
        ChatPanel.Children.Add(b); OutputScroller.ScrollToBottom();
        SetBubbleMaxWidth(b);
        HookBubbleWidthResize();

        // 操作按钮 + 入场动画
        BubbleActions.AddActions(p, () => text, true, () => { InputBox.Text = text; _ = RunAsync(); });
        AnimationHelper.Entrance(b);
    }

    private void AddImageBubble(string path)
    {
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelWidth = 320;
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit(); bmp.Freeze();

            var img = new Image
            {
                Source = bmp,
                MaxWidth = 320,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(16, 4, 16, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Margin = new Thickness(16, 2, 16, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = img
            };
            ChatPanel.Children.Add(border);
            OutputScroller.ScrollToBottom();
        }
        catch { /* image display failed, no-op */ }
    }

    /// <summary>添加 AI 气泡（同时写入 TimelineModel），返回 bubble + TextBlock</summary>
    private (Border, StackPanel, TextBlock) AddAssistantBubble()
    {
        // 写入 TimelineModel（流式状态）
        var entry = _sessionTabTimeline?.AddAssistantMessage();

        var b = Bubble(BrBgAst, BrBdrA, false);
        var p = GetContentPanel(b);
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        hdr.Children.Add(new TextBlock
        {
            Text = "Claude", Foreground = BrAst,
            FontSize = 12, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold
        });
        hdr.Children.Add(new TextBlock
        {
            Text = $"  {DateTime.Now:HH:mm}", Foreground = BrMuted,
            FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
        });
        if (_hasContinue)
            hdr.Children.Add(new TextBlock
            {
                Text = "  ·  继续中", Foreground = BrMuted,
                FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
            });
        p.Children.Add(hdr);
        var tb = new TextBlock
        {
            Foreground = BrDefault, FontSize = 14,
            FontFamily = new FontFamily("Consolas, Courier New"),
            TextWrapping = TextWrapping.Wrap, LineHeight = 22
        };
        p.Children.Add(tb);
        ChatPanel.Children.Add(b); OutputScroller.ScrollToBottom();
        SetBubbleMaxWidth(b);
        HookBubbleWidthResize();

        // 将 entry ID 保存在 TextBlock Tag 中，方便流式完成后回填
        if (entry != null)
            tb.Tag = entry.Id;

        // 操作按钮（仅复制）+ 入场动画
        BubbleActions.AddActions(p, () => tb.Text, false);
        AnimationHelper.Entrance(b);

        return (b, p, tb);
    }

    /// <summary>流式完成后更新 TimelineModel 中的条目文本</summary>
    private void FinalizeAssistantEntry(TextBlock tb)
    {
        if (tb.Tag is string entryId && _sessionTabTimeline != null)
        {
            _sessionTabTimeline.SetEntryText(entryId, tb.Text);
            _sessionTabTimeline.SetStreaming(entryId, false);
        }
    }

    // ── 恢复消息 ─────────────────────────────────────────

    /// <summary>从 SQLite 恢复聊天记录到指定标签的 TimelineModel</summary>
    private void AddStoredBubble(SessionTabItem tab, string role, string content)
    {
        if (tab.Timeline.Entries.Count == 0)
        {
            // 首次恢复：批量加载到 Timeline
            var entry = new TimelineEntry(TimelineEntryKind.Message, role, content);
            tab.Timeline.Entries.Add(entry);
        }

        var b = Bubble(
            role == "user" ? BrBgUser : BrBgAst,
            role == "user" ? BrBdrH : BrBdrA, role == "user");
        var p = GetContentPanel(b);

        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        hdr.Children.Add(new TextBlock
        {
            Text = role == "user" ? "你" : "Claude",
            Foreground = role == "user" ? BrUser : BrAst,
            FontSize = 12, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold
        });
        p.Children.Add(hdr);

        var tb = new TextBlock
        {
            Text = content,
            Foreground = BrDefault, FontSize = 14,
            FontFamily = new FontFamily("Consolas, Courier New"),
            TextWrapping = TextWrapping.Wrap, LineHeight = 22
        };
        p.Children.Add(tb);

        // 历史消息也加上悬停复制按钮（气泡不在视觉树中，需传入 b 作为 bubbleBorder）
        BubbleActions.AddActions(p, () => content, role == "user", bubbleBorder: b);

        tab.ChatBubbles.Add(b);
    }

    /// <summary>向标签的 TimelineModel 批量加载历史消息（来自 SQLite）</summary>
    private void LoadMessagesToTimeline(SessionTabItem tab, System.Collections.Generic.List<(string Role, string Content, DateTime Timestamp)> messages)
    {
        tab.Timeline.Entries.Clear();
        foreach (var (role, content, _) in messages)
        {
            if (role == "user")
                tab.Timeline.AddUserMessage(content);
            else if (role == "assistant")
            {
                var entry = tab.Timeline.AddAssistantMessage();
                entry.IsStreaming = false;
                entry.Text = content;
            }
        }
    }

    /// <summary>分割线</summary>
    private void AddDivider() =>
        ChatPanel.Children.Add(BuildDivider());

    /// <summary>系统提示行</summary>
    private void SysLine(string text, SolidColorBrush fg)
    {
        ChatPanel.Children.Add(new TextBlock
        {
            Text = text, Foreground = fg, FontSize = 12,
            FontFamily = new FontFamily("Consolas, Courier New"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 1, 16, 1)
        });
        OutputScroller.ScrollToBottom();
    }

    // ── 代码围栏分段 ─────────────────────────────────────

    private readonly record struct TextSegment(string Text, bool IsCode, string Code, string Language);

    /// <summary>将文本按代码围栏（``` ... ```）拆分为段</summary>
    private static List<TextSegment> ParseCodeFences(string text)
    {
        var segments = new List<TextSegment>();
        var regex = new Regex(@"```(\w*)\n([\s\S]*?)```", RegexOptions.Compiled);
        int pos = 0;
        foreach (Match m in regex.Matches(text))
        {
            if (m.Index > pos)
                segments.Add(new TextSegment(text[pos..m.Index], false, "", ""));
            segments.Add(new TextSegment("", true, m.Groups[2].Value, m.Groups[1].Value));
            pos = m.Index + m.Length;
        }
        if (pos < text.Length)
            segments.Add(new TextSegment(text[pos..], false, "", ""));
        return segments;
    }

    /// <summary>将分段内容渲染到 StackPanel（纯文本 + 代码块）</summary>
    private void BuildMessageContent(StackPanel container, string text)
    {
        var segments = ParseCodeFences(text);
        foreach (var seg in segments)
        {
            if (seg.IsCode)
            {
                container.Children.Add(CodeBlock.Build(seg.Code, seg.Language));
            }
            else if (!string.IsNullOrEmpty(seg.Text))
            {
                container.Children.Add(new TextBlock
                {
                    Text = seg.Text,
                    Foreground = BrDefault, FontSize = 14,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    TextWrapping = TextWrapping.Wrap, LineHeight = 22,
                });
            }
        }
    }

    // ── 气泡工厂 ─────────────────────────────────────────

    private static Border Bubble(SolidColorBrush bg, SolidColorBrush bdr, bool isUser = false)
    {
        var grid = new Grid();
        var contentPanel = new StackPanel();
        grid.Children.Add(contentPanel);
        return new Border
        {
            Background = bg, BorderBrush = bdr, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Margin = new Thickness(12, 4, 12, 4),
            Padding = new Thickness(14, 10, 14, 10), Child = grid,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
    }

    private static StackPanel GetContentPanel(Border b)
    {
        if (b.Child is Grid g && g.Children[0] is StackPanel sp)
            return sp;
        return (StackPanel)b.Child; // fallback
    }

    private void SetBubbleMaxWidth(Border b)
    {
        double w = ChatPanel.ActualWidth;
        if (w > 0)
            b.MaxWidth = w * 0.9;
    }

    private void UpdateAllBubbleMaxWidths()
    {
        double w = ChatPanel.ActualWidth * 0.9;
        foreach (var child in ChatPanel.Children)
        {
            if (child is Border b)
                b.MaxWidth = w;
        }
    }

    private bool _bubbleWidthHooked;

    private void HookBubbleWidthResize()
    {
        if (_bubbleWidthHooked) return;
        _bubbleWidthHooked = true;
        OutputScroller.SizeChanged += (_, _) => UpdateAllBubbleMaxWidths();
    }
}
