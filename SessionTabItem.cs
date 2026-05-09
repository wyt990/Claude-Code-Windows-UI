using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ClaudeCodeGUI;

/// <summary>标签页数据模型 — 一个标签对应一个独立会话</summary>
public class SessionTabItem : INotifyPropertyChanged
{
    private string _name = "新会话";
    private bool _isActive;

    /// <summary>会话唯一标识</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>显示在标签上的名称</summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>会话状态（复用原有 SessionState）</summary>
    public SessionState State { get; }

    /// <summary>标签是否激活</summary>
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    // ── 聊天 UI（惰性创建，切换标签时交换） ──────────────

    private StackPanel? _chatPanel;

    /// <summary>聊天消息容器（惰性创建）</summary>
    public StackPanel ChatPanel
    {
        get
        {
            if (_chatPanel == null)
                _chatPanel = new StackPanel();
            return _chatPanel;
        }
    }

    /// <summary>聊天气泡 UI 元素（标签切换时保存/恢复）</summary>
    public List<UIElement> ChatBubbles { get; } = new();

    /// <summary>保存当前聊天气泡到此标签（同时确保 Timeline 中消息文本完整）</summary>
    public void SaveChatBubbles(UIElementCollection children)
    {
        ChatBubbles.Clear();
        foreach (UIElement child in children)
            ChatBubbles.Add(child);

        // 将流式条目中的文本回填到 Timeline（确保数据完整性）
        foreach (var entry in Timeline.Entries)
        {
            if (entry.IsStreaming)
                entry.IsStreaming = false;
        }
    }

    /// <summary>将此标签的聊天气泡恢复到指定容器</summary>
    /// <returns>是否有气泡被恢复</returns>
    public bool RestoreChatBubbles(UIElementCollection children)
    {
        children.Clear();
        foreach (UIElement child in ChatBubbles)
            children.Add(child);
        return ChatBubbles.Count > 0;
    }

    /// <summary>获取 Timeline 中的消息条目列表（用于持久化/恢复）</summary>
    public List<(string Role, string Text)> GetTimelineMessages()
    {
        var list = new List<(string, string)>();
        foreach (var entry in Timeline.Entries)
        {
            if (entry.Kind == TimelineEntryKind.Message && !string.IsNullOrEmpty(entry.Text))
                list.Add((entry.Role, entry.Text));
        }
        return list;
    }

    /// <summary>附加到输入框的图片路径（标签切换时保存/恢复）</summary>
    public string? AttachedImagePath { get; set; }

    /// <summary>时间线数据模型（取代 ChatBubbles 作为数据源）</summary>
    public TimelineModel Timeline { get; } = new();

    /// <summary>会话的聊天记录（序列化为字符串列表，用于持久化）</summary>
    public List<string> ChatHistory { get; } = new();

    // ── 进程相关（每个标签持有自己的进程） ──────────────

    /// <summary>Claude Code 进程</summary>
    public Process? Job { get; set; }

    /// <summary>取消令牌源</summary>
    public CancellationTokenSource? CTS { get; set; }

    /// <summary>是否为继续会话</summary>
    public bool HasContinue { get; set; }

    /// <summary>对话轮次</summary>
    public int Turns { get; set; }

    /// <summary>上次输出时间</summary>
    public DateTime LastOutputTime { get; set; } = DateTime.Now;

    // ── 构造 ───────────────────────────────────────────

    public SessionTabItem(string name, SessionState? state = null)
    {
        _name = name;
        State = state ?? new SessionState();
    }

    // ── INotifyPropertyChanged ─────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
