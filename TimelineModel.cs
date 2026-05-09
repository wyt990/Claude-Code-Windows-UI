using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClaudeCodeGUI;

/// <summary>条目类型</summary>
public enum TimelineEntryKind
{
    /// <summary>用户或 AI 消息</summary>
    Message,
    /// <summary>工作日志（工具调用等）</summary>
    WorkLog,
    /// <summary>工具调用</summary>
    ToolCall,
    /// <summary>分割线</summary>
    Divider,
    /// <summary>系统提示</summary>
    System
}

/// <summary>时间线条目 — 数据模型，非 UI 元素</summary>
public class TimelineEntry : INotifyPropertyChanged
{
    private string _role = "";
    private string _text = "";
    private bool _isStreaming;

    /// <summary>条目标识（唯一）</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; } = DateTime.Now;

    /// <summary>条目类型</summary>
    public TimelineEntryKind Kind { get; }

    /// <summary>角色："user" / "assistant" / "system"</summary>
    public string Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(); }
    }

    /// <summary>文本内容</summary>
    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    /// <summary>是否正在流式接收中</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set { _isStreaming = value; OnPropertyChanged(); }
    }

    // ── 工具调用专用属性 ───────────────────────────

    /// <summary>工具名称（仅 ToolCall 条目使用）</summary>
    public string? ToolName { get; set; }

    /// <summary>工具调用 ID（仅 ToolCall 条目使用）</summary>
    public string? ToolId { get; set; }

    /// <summary>工具调用状态（仅 ToolCall 条目使用）</summary>
    public ToolCallStatus ToolStatus { get; set; } = ToolCallStatus.Running;

    public TimelineEntry(TimelineEntryKind kind, string role, string text = "")
    {
        Kind = kind;
        _role = role;
        _text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 时间线数据模型 — 取代 List&lt;UIElement&gt; ChatBubbles
/// 作为会话时间线的单一数据源，UI 从该模型派生
/// </summary>
public class TimelineModel
{
    /// <summary>时间线条目集合</summary>
    public ObservableCollection<TimelineEntry> Entries { get; } = new();

    // ── 事件 ───────────────────────────────────────────────

    /// <summary>新增条目后触发</summary>
    public event EventHandler<TimelineEntry>? EntryAdded;

    /// <summary>条目文本变更时触发</summary>
    public event EventHandler<TimelineEntry>? EntryTextUpdated;

    /// <summary>条目流式状态变更时触发</summary>
    public event EventHandler<TimelineEntry>? EntryStreamingChanged;

    // ── 添加条目 ─────────────────────────────────────────

    /// <summary>添加用户消息</summary>
    public TimelineEntry AddUserMessage(string text)
    {
        var entry = new TimelineEntry(TimelineEntryKind.Message, "user", text);
        Entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    /// <summary>添加 AI 消息（流式开始时）</summary>
    public TimelineEntry AddAssistantMessage()
    {
        var entry = new TimelineEntry(TimelineEntryKind.Message, "assistant")
        {
            IsStreaming = true
        };
        Entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    /// <summary>添加工具调用工作日志</summary>
    public TimelineEntry AddWorkLog(string label, string? detail = null)
    {
        var text = detail != null ? $"{label}: {detail}" : label;
        var entry = new TimelineEntry(TimelineEntryKind.WorkLog, "tool", text);
        Entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    /// <summary>添加分割线</summary>
    public TimelineEntry AddDivider()
    {
        var entry = new TimelineEntry(TimelineEntryKind.Divider, "");
        Entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    /// <summary>添加系统提示</summary>
    public TimelineEntry AddSystemMessage(string text)
    {
        var entry = new TimelineEntry(TimelineEntryKind.System, "system", text);
        Entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    /// <summary>添加工具调用条目</summary>
    public TimelineEntry AddToolCall(string toolName, string toolId, ToolCallStatus status = ToolCallStatus.Running)
    {
        var entry = new TimelineEntry(TimelineEntryKind.ToolCall, "tool", "")
        {
            ToolName = toolName,
            ToolId = toolId,
            ToolStatus = status,
        };
        Entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    // ── 更新条目 ─────────────────────────────────────────

    /// <summary>更新条目文本（追加）</summary>
    public void AppendToEntry(string entryId, string deltaText)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Id == entryId)
            {
                Entries[i].Text += deltaText;
                EntryTextUpdated?.Invoke(this, Entries[i]);
                return;
            }
        }
    }

    /// <summary>设置条目的完整文本</summary>
    public void SetEntryText(string entryId, string text)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Id == entryId)
            {
                Entries[i].Text = text;
                EntryTextUpdated?.Invoke(this, Entries[i]);
                return;
            }
        }
    }

    /// <summary>设置条目流式状态</summary>
    public void SetStreaming(string entryId, bool streaming)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Id == entryId)
            {
                Entries[i].IsStreaming = streaming;
                EntryStreamingChanged?.Invoke(this, Entries[i]);
                return;
            }
        }
    }

    // ── 查询 ─────────────────────────────────────────────

    /// <summary>获取最后一个 AI 消息条目（用于流式追加）</summary>
    public TimelineEntry? LastAssistantEntry()
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (Entries[i].Kind == TimelineEntryKind.Message && Entries[i].Role == "assistant")
                return Entries[i];
        }
        return null;
    }

    /// <summary>获取所有消息条目（用于持久化）</summary>
    public System.Collections.Generic.IEnumerable<TimelineEntry> GetMessages()
    {
        foreach (var e in Entries)
        {
            if (e.Kind == TimelineEntryKind.Message)
                yield return e;
        }
    }

    /// <summary>清空所有条目</summary>
    public void Clear()
    {
        Entries.Clear();
    }
}
