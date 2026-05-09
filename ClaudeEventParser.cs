using System;
using System.Text.Json;

namespace ClaudeCodeGUI;

// ══════════════════════════════════════════════════════════════
// 统一事件类型（不可变 record）
// ══════════════════════════════════════════════════════════════

/// <summary>所有 Claude 事件的基类</summary>
public abstract record ClaudeEvent;

/// <summary>系统初始化 — session_id, model, tools, version</summary>
public record SystemInitEvent(string SessionId, string Model) : ClaudeEvent;

/// <summary>消息开始 — message.id</summary>
public record MessageStartEvent(string MessageId) : ClaudeEvent;

/// <summary>流式文本增量 — text_delta</summary>
public record TextDeltaEvent(string Text) : ClaudeEvent;

/// <summary>工具输入增量 — input_json_delta</summary>
public record ToolInputDeltaEvent(string PartialJson) : ClaudeEvent;

/// <summary>工具块开始 — content_block type=tool_use</summary>
public record ToolBlockStartEvent(string ToolName, string ToolId) : ClaudeEvent;

/// <summary>内容块结束</summary>
public record ContentBlockStopEvent(int Index) : ClaudeEvent;

/// <summary>消息增量 — stop_reason + usage</summary>
public record MessageDeltaEvent(string? StopReason, int OutputTokens) : ClaudeEvent;

/// <summary>消息结束</summary>
public record MessageStopEvent : ClaudeEvent;

/// <summary>完整 assistant 消息（流式结束后汇总）</summary>
public record AssistantCompleteEvent(JsonElement Message) : ClaudeEvent;

/// <summary>工具执行结果</summary>
public record ToolResultEvent(string ToolUseId, string Content) : ClaudeEvent;

/// <summary>最终结果（含 modelUsage）</summary>
public record ResultEvent(int ContextWindow, int InputTokens, int OutputTokens) : ClaudeEvent;

// ══════════════════════════════════════════════════════════════
// 事件解析器
// ══════════════════════════════════════════════════════════════

/// <summary>
/// 将 claudecode --output-format stream-json 的结构化 JSON 行解析为统一事件。
/// 顶层 type: system / stream_event / assistant / user / result
/// stream_event 需解包 event 字段（Anthropic Messages API 格式）
/// </summary>
public class ClaudeEventParser
{
    /// <summary>解析一行 JSON 输出，返回 ClaudeEvent 或 null（无法识别时）</summary>
    public ClaudeEvent? ParseLine(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonLine);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp))
                return null;

            var type = typeProp.GetString();
            return type switch
            {
                "system" => ParseSystem(root),
                "stream_event" => ParseStreamEvent(root),
                "assistant" => ParseAssistant(root),
                "user" => ParseUser(root),
                "result" => ParseResult(root),
                _ => null
            };
        }
    }

    /// <summary>解析 system 事件: subtype=init, session_id, model</summary>
    private static ClaudeEvent? ParseSystem(JsonElement root)
    {
        if (!root.TryGetProperty("session_id", out var sidProp) ||
            !root.TryGetProperty("model", out var modelProp))
            return null;

        return new SystemInitEvent(
            sidProp.GetString() ?? "",
            modelProp.GetString() ?? "unknown");
    }

    /// <summary>解析 stream_event: 解包 event 字段，按内部 type 分派</summary>
    private static ClaudeEvent? ParseStreamEvent(JsonElement root)
    {
        if (!root.TryGetProperty("event", out var inner))
            return null;

        if (!inner.TryGetProperty("type", out var typeProp))
            return null;

        var eventType = typeProp.GetString();
        return eventType switch
        {
            "message_start" => ParseMessageStart(inner),
            "content_block_start" => ParseContentBlockStart(inner),
            "content_block_delta" => ParseContentBlockDelta(inner),
            "content_block_stop" => ParseContentBlockStop(inner),
            "message_delta" => ParseMessageDelta(inner),
            "message_stop" => new MessageStopEvent(),
            _ => null
        };
    }

    private static ClaudeEvent? ParseMessageStart(JsonElement evt)
    {
        if (!evt.TryGetProperty("message", out var msg))
            return null;
        var id = msg.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        return new MessageStartEvent(id ?? Guid.NewGuid().ToString());
    }

    private static ClaudeEvent? ParseContentBlockStart(JsonElement evt)
    {
        if (!evt.TryGetProperty("content_block", out var block))
            return null;

        var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : "";
        if (blockType == "tool_use")
        {
            var name = block.TryGetProperty("name", out var n) ? n.GetString() : "";
            var id = block.TryGetProperty("id", out var i) ? i.GetString() : "";
            return new ToolBlockStartEvent(name ?? "", id ?? "");
        }

        // text block start — 忽略，通过 text_delta 流式追加即可
        return null;
    }

    private static ClaudeEvent? ParseContentBlockDelta(JsonElement evt)
    {
        if (!evt.TryGetProperty("delta", out var delta))
            return null;

        var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : "";

        return deltaType switch
        {
            "text_delta" => ParseTextDelta(delta),
            "input_json_delta" => ParseInputJsonDelta(delta),
            _ => null
        };
    }

    private static TextDeltaEvent? ParseTextDelta(JsonElement delta)
    {
        var text = delta.TryGetProperty("text", out var t) ? t.GetString() : "";
        return new TextDeltaEvent(text ?? "");
    }

    private static ToolInputDeltaEvent? ParseInputJsonDelta(JsonElement delta)
    {
        var json = delta.TryGetProperty("partial_json", out var p) ? p.GetString() : "";
        return new ToolInputDeltaEvent(json ?? "");
    }

    private static ContentBlockStopEvent ParseContentBlockStop(JsonElement evt)
    {
        var index = evt.TryGetProperty("index", out var idx) ? idx.GetInt32() : 0;
        return new ContentBlockStopEvent(index);
    }

    private static ClaudeEvent? ParseMessageDelta(JsonElement evt)
    {
        string? stopReason = null;
        if (evt.TryGetProperty("delta", out var delta) &&
            delta.TryGetProperty("stop_reason", out var sr))
        {
            stopReason = sr.GetString();
        }

        int outputTokens = 0;
        if (evt.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("output_tokens", out var ot))
        {
            outputTokens = ot.GetInt32();
        }

        return new MessageDeltaEvent(stopReason, outputTokens);
    }

    /// <summary>解析 assistant 事件: 完整消息（流式结束后汇总）</summary>
    private static ClaudeEvent? ParseAssistant(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var msg))
            return null;
        return new AssistantCompleteEvent(msg.Clone());
    }

    /// <summary>解析 user 事件: 工具执行结果</summary>
    /// <remarks>
    /// claudecode 有两种形态：(1) <c>tool_use_result</c> 为对象，含 tool_use_id / content；
    /// (2) <c>tool_use_result</c> 为字符串摘要，id 与正文在 <c>message.content[]</c> 的 <c>tool_result</c> 块中。
    /// </remarks>
    private static ClaudeEvent? ParseUser(JsonElement root)
    {
        if (!root.TryGetProperty("tool_use_result", out var tur))
            return null;

        if (tur.ValueKind == JsonValueKind.Object)
        {
            var toolUseId = tur.TryGetProperty("tool_use_id", out var id)
                ? id.GetString() ?? ""
                : "";

            var content = tur.TryGetProperty("content", out var c)
                ? ToolResultContentToString(c)
                : "";

            return new ToolResultEvent(toolUseId, content);
        }

        if (tur.ValueKind == JsonValueKind.String)
        {
            string summary = tur.GetString() ?? "";
            if (TryParseToolResultBlockFromMessage(root, out var toolUseId, out var detail))
            {
                string body = string.IsNullOrEmpty(detail) ? summary : detail;
                return new ToolResultEvent(toolUseId, body);
            }

            return new ToolResultEvent("", summary);
        }

        return null;
    }

    /// <summary>从 user 事件的 message.content 中取首个 tool_result 块的 id 与 content。</summary>
    private static bool TryParseToolResultBlockFromMessage(JsonElement root, out string toolUseId, out string content)
    {
        toolUseId = "";
        content = "";
        if (!root.TryGetProperty("message", out var msg))
            return false;
        if (!msg.TryGetProperty("content", out var contentEl))
            return false;
        if (contentEl.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var block in contentEl.EnumerateArray())
        {
            var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : "";
            if (!string.Equals(blockType, "tool_result", StringComparison.Ordinal))
                continue;

            toolUseId = block.TryGetProperty("tool_use_id", out var id) ? id.GetString() ?? "" : "";
            if (block.TryGetProperty("content", out var c))
                content = ToolResultContentToString(c);
            return true;
        }

        return false;
    }

    /// <summary>tool_result.content 可能是字符串或结构化 JSON。</summary>
    private static string ToolResultContentToString(JsonElement c)
    {
        return c.ValueKind == JsonValueKind.String
            ? c.GetString() ?? ""
            : c.GetRawText();
    }

    /// <summary>解析 result 事件: 最终结果 + modelUsage 统计</summary>
    private static ClaudeEvent? ParseResult(JsonElement root)
    {
        if (!root.TryGetProperty("modelUsage", out var usage))
            return null;

        var ctx = usage.TryGetProperty("contextWindow", out var cw) ? cw.GetInt32() : 200000;
        var inp = usage.TryGetProperty("inputTokens", out var it) ? it.GetInt32() : 0;
        var outp = usage.TryGetProperty("outputTokens", out var ot) ? ot.GetInt32() : 0;

        return new ResultEvent(ctx, inp, outp);
    }
}
