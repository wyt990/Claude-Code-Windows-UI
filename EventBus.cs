using System;

namespace ClaudeCodeGUI;

/// <summary>
/// 本地事件总线 — 将 ClaudeEvent 分发给各 UI 组件。
/// 单例模式，全局唯一实例。
/// </summary>
public class EventBus
{
    private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
    public static EventBus Instance => _instance.Value;

    // ── 事件定义 ─────────────────────────────────────────────

    /// <summary>系统初始化完成</summary>
    public event EventHandler<SystemInitEvent>? SystemInit;
    /// <summary>流式文本增量到达</summary>
    public event EventHandler<TextDeltaEvent>? TextDeltaReceived;
    /// <summary>工具块开始（工具名称 + ID）</summary>
    public event EventHandler<ToolBlockStartEvent>? ToolUseStarted;
    /// <summary>工具输入增量到达（partial_json）</summary>
    public event EventHandler<ToolInputDeltaEvent>? ToolInputDelta;
    /// <summary>工具块结束（参数完整）</summary>
    public event EventHandler<ContentBlockStopEvent>? ToolBlockCompleted;
    /// <summary>消息增量到达（stop_reason + output_tokens）</summary>
    public event EventHandler<MessageDeltaEvent>? MessageDelta;
    /// <summary>消息结束</summary>
    public event EventHandler<MessageStopEvent>? MessageStopped;
    /// <summary>完整 assistant 消息就绪</summary>
    public event EventHandler<AssistantCompleteEvent>? AssistantComplete;
    /// <summary>工具执行结果返回</summary>
    public event EventHandler<ToolResultEvent>? ToolResultReceived;
    /// <summary>最终结果（Token 用量统计）</summary>
    public event EventHandler<ResultEvent>? ResultReceived;
    /// <summary>消息开始</summary>
    public event EventHandler<MessageStartEvent>? MessageStarted;

    // ── 发布方法 ─────────────────────────────────────────────

    public void Publish(ClaudeEvent evt)
    {
        switch (evt)
        {
            case SystemInitEvent e:
                SystemInit?.Invoke(this, e);
                break;
            case MessageStartEvent e:
                MessageStarted?.Invoke(this, e);
                break;
            case TextDeltaEvent e:
                TextDeltaReceived?.Invoke(this, e);
                break;
            case ToolBlockStartEvent e:
                ToolUseStarted?.Invoke(this, e);
                break;
            case ToolInputDeltaEvent e:
                ToolInputDelta?.Invoke(this, e);
                break;
            case ContentBlockStopEvent e:
                ToolBlockCompleted?.Invoke(this, e);
                break;
            case MessageDeltaEvent e:
                MessageDelta?.Invoke(this, e);
                break;
            case MessageStopEvent e:
                MessageStopped?.Invoke(this, e);
                break;
            case AssistantCompleteEvent e:
                AssistantComplete?.Invoke(this, e);
                break;
            case ToolResultEvent e:
                ToolResultReceived?.Invoke(this, e);
                break;
            case ResultEvent e:
                ResultReceived?.Invoke(this, e);
                break;
        }
    }

    /// <summary>清除所有订阅（用于重置/测试）</summary>
    public void ClearSubscriptions()
    {
        SystemInit = null;
        TextDeltaReceived = null;
        ToolUseStarted = null;
        ToolInputDelta = null;
        ToolBlockCompleted = null;
        MessageDelta = null;
        MessageStopped = null;
        AssistantComplete = null;
        ToolResultReceived = null;
        ResultReceived = null;
        MessageStarted = null;
    }
}
