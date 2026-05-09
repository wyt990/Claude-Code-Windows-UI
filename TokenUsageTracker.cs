using System;

namespace ClaudeCodeGUI;

/// <summary>
/// Token 用量跟踪 — 从 message_delta 和 result 事件更新 SessionState 的 token 用量。
/// 实时：message_delta → usage.output_tokens
/// 最终：result → modelUsage → {contextWindow, inputTokens, outputTokens}
/// </summary>
public class TokenUsageTracker
{
    private readonly SessionState _sessionState;
    private int _lastReportedOutput;

    public TokenUsageTracker(SessionState sessionState)
    {
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
    }

    /// <summary>从 message_delta 事件更新实时 output_tokens</summary>
    public void UpdateFromMessageDelta(int outputTokens)
    {
        if (outputTokens < 0) return;

        // 只更新增量（output_tokens 是累计值）
        _lastReportedOutput = outputTokens;
        _sessionState.OutputTokens = outputTokens;
        _sessionState.UsedTokens = _sessionState.InputTokens + outputTokens;
    }

    /// <summary>从 result 事件更新最终准确值</summary>
    public void UpdateFromResult(int contextWindow, int inputTokens, int outputTokens)
    {
        _sessionState.InputTokens = inputTokens;
        _sessionState.OutputTokens = outputTokens;
        _sessionState.MaxTokens = contextWindow > 0 ? contextWindow : _sessionState.MaxTokens;
        _sessionState.UsedTokens = inputTokens + outputTokens;
    }

    /// <summary>重置（新一轮对话开始前调用）</summary>
    public void Reset()
    {
        _lastReportedOutput = 0;
        _sessionState.InputTokens = 0;
        _sessionState.OutputTokens = 0;
        _sessionState.UsedTokens = 0;
    }

    /// <summary>最后一次上报的 output_tokens 值</summary>
    public int LastReportedOutput => _lastReportedOutput;
}
