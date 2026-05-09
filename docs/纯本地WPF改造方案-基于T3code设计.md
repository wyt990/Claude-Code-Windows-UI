# 纯本地 WPF 改造方案（基于 T3code 设计）

> 将 Claude Code GUI 改造为类似 T3code 的完整功能聊天界面
> 纯本地实现，无 WebSocket、无 Node.js、无后端服务

---

## 一、核心思路

T3code 的 SDK 本质上是对 `claudecode` 进程的封装，通过 stdin/stdout 传输 JSON-RPC。我们**直接解析 claudecode 的流式输出**，提取结构化事件，在本地实现类似的事件系统。

---

## 二、claudecode 的结构化流格式

claudecode 使用 `--output-format stream-json --verbose --include-partial-messages` 参数输出结构化事件（实测验证，`--json` 参数不存在）：

```bash
# 验证命令
claudecode -p "只回复OK" --session-id "11111111-1111-4111-8111-111111111111" --output-format stream-json --verbose --include-partial-messages
```

### 顶层事件类型（每行一个 JSON）

| 类型 | 说明 |
|------|------|
| `system` | 系统初始化信息（tools, model, session_id, version） |
| `stream_event` | 流式事件（包裹 Messages API 事件） |
| `assistant` | 完整的 Assistant 消息（流式结束后发送） |
| `user` | 工具执行结果（发送给模型） |
| `result` | 最终结果（含 usage 统计） |

### stream_event 内部事件（Anthropic Messages API 格式）

```json
// 消息开始
{"type":"stream_event","event":{"type":"message_start","message":{"id":"msg_xxx","content":[],"model":"claude-3-opus-...","role":"assistant"}}}

// 文本块开始 → 流式文本增量 → 文本块结束
{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}}
{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"好的，我来帮你"}}}
{"type":"stream_event","event":{"type":"content_block_stop","index":0}}

// 工具调用开始 → 流式 JSON 输入 → 工具块结束
{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"call_xxx","name":"Read","input":""}}}
{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"file_path\":\"src/app.ts\"}"}}}
{"type":"stream_event","event":{"type":"content_block_stop","index":0}}

// 消息增量（含 stop_reason 和 usage）
{"type":"stream_event","event":{"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"output_tokens":123}}}

// 消息结束
{"type":"stream_event","event":{"type":"message_stop"}}

// 完整 assistant 消息（流式结束后汇总）
{"type":"assistant","message":{"id":"msg_xxx","content":[...],"usage":{"input_tokens":100,"output_tokens":50}}}

// 工具执行结果
{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"call_xxx","content":"..."}]},"tool_use_result":{...}}

// 最终结果（含 modelUsage 统计）
{"type":"result","subtype":"success","usage":{...},"modelUsage":{"contextWindow":200000,"inputTokens":100,"outputTokens":50}}
```

---

## 三、架构设计（纯本地）

```
┌─────────────────────────────────────────────────────────┐
│                      WPF 前端                            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │
│  │   Composer  │  │   Timeline  │  │  PlanSidebar    │  │
│  │  (输入区)   │──│  (时间线)   │──│   (任务清单)     │  │
│  └─────────────┘  └─────────────┘  └─────────────────┘  │
│                           │                              │
│                    ┌────────┴────────┐                    │
│                    │   EventBus      │                    │
│                    │  (本地事件总线)   │                    │
│                    └────────┬────────┘                    │
│                           │                              │
│  ┌────────────────────────┴───────────────────────────┐  │
│  │              ClaudeProcessManager                 │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌──────────┐  │  │
│  │  │ ProcessPool │  │EventParser  │  │SessionMgr│  │  │
│  │  │ (进程池)     │──│(JSON解析器)  │──│(会话管理)│  │  │
│  │  └─────────────┘  └─────────────┘  └──────────┘  │  │
│  │                                                   │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │           ToolApprovalQueue                 │  │
│  │  │      (工具审批队列，模态对话框)               │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
│                           │                              │
│                    ┌──────┴──────┐                      │
│                    │  claudecode  │                      │
│                    │   子进程      │                      │
│                    └─────────────┘                        │
│                           │                              │
│  ┌────────────────────────┴───────────────────────────┐  │
│  │              SQLite (本地持久化)                     │
│  │  ┌─────────────┐  ┌─────────────┐  ┌──────────┐  │  │
│  │  │  Messages   │  │  Activities │  │ Sessions │  │  │
│  │  │  (消息)      │  │  (活动日志)  │  │ (会话状态)│  │  │
│  │  └─────────────┘  └─────────────┘  └──────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 四、关键改造点

### 1. 启动参数改造（MainWindow.Session.cs）

**现状**：
```csharp
// 简单文本流
_arguments = $"/c \"\"{_claudeExe}\" -p \"{safe}\"\"";
```

**目标**：
```csharp
// 结构化流
var args = new List<string>();
args.Add("--output-format"); args.Add("stream-json");   // 启用 JSON 流式输出
args.Add("--verbose");                                   // 必需：与 stream-json 搭配
args.Add("--include-partial-messages");                  // 启用流式内容块
args.Add("--session-id"); args.Add(_session.ClaudeSessionId);
if (_hasContinue) 
{
    args.Add("--resume"); args.Add(_session.ClaudeSessionId);
}
args.Add("-p"); args.Add(safe);

// 同时捕获 stdout (JSON 事件) 和 stderr (日志)
// 注意：必须设置 RedirectStandardInput = true，用于审批流程

// 关键差异：每轮对话启动一个新进程，而非每标签一个进程
// claudecode 不支持持久进程复用
```

---

### 2. 事件解析器（新增 ClaudeEventParser.cs）

```csharp
public class ClaudeEventParser
{
    // 将 claudecode 的结构化 JSON 行解析为统一事件
    public ClaudeEvent? ParseLine(string jsonLine)
    {
        var doc = JsonDocument.Parse(jsonLine);
        var type = doc.RootElement.GetProperty("type").GetString();
        
        // 顶层事件分派
        return type switch
        {
            "system" => ParseSystem(doc),                                    // 系统初始化
            "stream_event" => ParseStreamEvent(doc),                         // 流式事件（需解包）
            "assistant" => ParseAssistant(doc),                              // 完整 assistant 消息
            "user" => ParseUser(doc),                                        // 工具结果
            "result" => ParseResult(doc),                                    // 最终结果 + usage
            _ => null
        };
    }
    
    // stream_event 需解包 event 字段
    private ClaudeEvent? ParseStreamEvent(JsonDocument doc)
    {
        var inner = doc.RootElement.GetProperty("event");
        var eventType = inner.GetProperty("type").GetString();
        
        return eventType switch
        {
            "message_start" => ParseMessageStart(inner),
            "content_block_start" => ParseContentBlockStart(inner),
            "content_block_delta" => ParseContentBlockDelta(inner),
            "content_block_stop" => new ContentBlockStopEvent(),
            "message_delta" => ParseMessageDelta(inner),
            "message_stop" => new MessageStopEvent(),
            _ => null
        };
    }
}

// 统一事件类型
public abstract record ClaudeEvent;
public record SystemInitEvent(string SessionId, string Model) : ClaudeEvent;
public record MessageStartEvent(string MessageId) : ClaudeEvent;
public record TextDeltaEvent(string Text) : ClaudeEvent;                     // text_delta
public record ToolInputDeltaEvent(string PartialJson) : ClaudeEvent;        // input_json_delta
public record ToolBlockStartEvent(string ToolName, string ToolId) : ClaudeEvent;
public record ContentBlockStopEvent : ClaudeEvent;
public record MessageDeltaEvent(string? StopReason, int OutputTokens) : ClaudeEvent;
public record MessageStopEvent : ClaudeEvent;
public record AssistantCompleteEvent(JsonElement Message) : ClaudeEvent;     // 完整 assistant
public record ToolResultEvent(string ToolUseId, string Content) : ClaudeEvent;
public record ResultEvent(int ContextWindow, int InputTokens, int OutputTokens) : ClaudeEvent;
```

---

### 3. 本地事件总线（新增 EventBus.cs）

```csharp
public class EventBus
{
    public event EventHandler<ThinkingDeltaEvent>? ThinkingDeltaReceived;
    public event EventHandler<TextDeltaEvent>? TextDeltaReceived;
    public event EventHandler<ToolUseStartEvent>? ToolUseStarted;
    public event EventHandler<ToolUseCompleteEvent>? ToolUseCompleted;
    public event EventHandler<TurnPlanUpdatedEvent>? TurnPlanUpdated;
    public event EventHandler<ApprovalRequestedEvent>? ApprovalRequested;
    public event EventHandler<TokenUsageUpdatedEvent>? TokenUsageUpdated;
    
    public void Publish(ClaudeEvent evt)
    {
        // 分发到对应处理器
    }
}
```

---

### 4. 时间线模型重构（改造 SessionTabItem.cs）

**现状**：
```csharp
public List<UIElement> ChatBubbles { get; } = new();  // 纯 UI 元素
```

**目标**：
```csharp
public class TimelineModel
{
    // 数据模型（可持久化）
    public ObservableCollection<TimelineEntry> Entries { get; } = new();
    
    // 从 Entries 派生 UI（类似 MVVM）
    public void AddMessage(ChatMessage message);
    public void AddWorkLog(WorkLogEntry entry);
    public void UpdateMessageText(string messageId, string deltaText);
}

public record TimelineEntry(string Id, DateTime CreatedAt, TimelineKind Kind);
public record ChatMessageEntry(string Id, string Role, string Text, bool Streaming) : TimelineEntry;
public record WorkLogEntry(string Id, WorkTone Tone, string Label, string? Detail) : TimelineEntry;
```

---

### 5. 工具审批队列（新增 ToolApprovalQueue.cs）

```csharp
public class ToolApprovalQueue
{
    private readonly Queue<PendingApproval> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    // 当收到 tool_use_start 时调用
    public async Task<bool> RequestApprovalAsync(ToolUseStartEvent toolEvent)
    {
        var approval = new PendingApproval(toolEvent);
        _queue.Enqueue(approval);
        
        // 弹出审批对话框（必须在 UI 线程）
        var result = await _dispatcher.InvokeAsync(() => 
            ShowApprovalDialog(toolEvent));
        
        return result == ApprovalResult.Allowed;
    }
}

// 审批对话框 XAML
public partial class ApprovalDialog : Window
{
    public string ToolName { get; set; }  // "Bash", "ReadFile", "EditFile"
    public string Description { get; set; }  // 根据工具生成描述
    public JsonElement Input { get; set; }   // 工具参数
}
```

---

### 6. 流式渲染改造（改造 StreamTo）

**现状**：
```csharp
// 直接追加原始文本
tb.Text += clean;
```

**目标**：
```csharp
private async Task StreamEventsTo(CancellationToken tok)
{
    var parser = new ClaudeEventParser();
    string? currentToolId = null;
    var toolInputBuilder = new StringBuilder();
    
    while (!tok.IsCancellationRequested)
    {
        string? line = await _reader.ReadLineAsync();
        if (line == null) break;
        
        var evt = parser.ParseLine(line);
        if (evt == null) continue;
        
        // 通过事件总线分发（非直接 UI 更新）
        _eventBus.Publish(evt);
        
        // 流式事件处理
        switch (evt)
        {
            case SystemInitEvent sys:
                _sessionId = sys.SessionId;
                break;
                
            case TextDeltaEvent textDelta:
                await Dispatcher.InvokeAsync(() => 
                    _timeline.UpdateMessageText(_currentMessageId, textDelta.Text));
                break;
                
            case ToolBlockStartEvent toolStart:
                currentToolId = toolStart.ToolId;
                toolInputBuilder.Clear();
                await Dispatcher.InvokeAsync(() => 
                    _timeline.AddWorkLog(CreateToolStartEntry(toolStart)));
                break;
                
            case ToolInputDeltaEvent toolInput:
                toolInputBuilder.Append(toolInput.PartialJson);
                break;
                
            case ContentBlockStopEvent when currentToolId != null:
                // 工具参数完整，触发审批
                var fullInput = toolInputBuilder.ToString();
                var approved = await _approvalQueue.RequestApprovalAsync(
                    currentToolId, fullInput);
                // 向 claudecode stdin 写审批结果
                await WriteApprovalAsync(approved);
                currentToolId = null;
                break;
                
            case MessageDeltaEvent msgDelta:
                await Dispatcher.InvokeAsync(() => 
                    _tokenTracker.UpdateOutputTokens(msgDelta.OutputTokens));
                break;
                
            case ResultEvent result:
                await Dispatcher.InvokeAsync(() =>
                    _tokenTracker.UpdateFinalUsage(result));
                break;
        }
    }
}
```

---

### 7. Token 用量实时显示（复用 SessionState.cs）

```csharp
// SessionState 已有 UsedTokens / MaxTokens / TokensPercentage 属性
// 只需补充更新逻辑，从 result 和 message_delta 事件中提取

public class TokenUsageTracker
{
    private readonly SessionState _sessionState;
    
    public void UpdateFromMessageDelta(int outputTokens)
    {
        // message_delta 提供实时 output_tokens
        _sessionState.UsedTokens = outputTokens;
        _sessionState.MaxTokens = 200000;         // 固定：claudecode contextWindow
        _sessionState.NotifyTokenChanged();
    }
    
    public void UpdateFromResult(int contextWindow, int inputTokens, int outputTokens)
    {
        // result 事件提供最终准确值
        _sessionState.UsedTokens = inputTokens + outputTokens;
        _sessionState.MaxTokens = contextWindow;
        _sessionState.NotifyTokenChanged();
    }
}

// 数据来源：
// - 实时：stream_event → message_delta → usage.output_tokens
// - 最终：result → modelUsage → {contextWindow, inputTokens, outputTokens}
```

---

## 五、数据流示例（单轮对话）

```
用户发送消息
    ↓
构建 claudecode 参数（--output-format stream-json --verbose --include-partial-messages ...）
    ↓
Process.Start() 启动进程（RedirectStandardInput=true 用于审批）
    ↓
逐行读取 stdout
    ↓
ClaudeEventParser 解析 JSON
    │  顶层 type 分派 → stream_event 解包 event 字段
    ↓
EventBus 分发事件
    ├─→ SystemInitEvent → 初始化会话信息（模型名、session_id）
    ├─→ MessageStartEvent → 标记消息开始
    ├─→ TextDeltaEvent → 追加到 AI 回复文本（实时流式渲染）
    ├─→ ToolBlockStartEvent (ReadFile) → 显示"👁️ 读取文件 src/app.ts"
    ├─→ ToolInputDeltaEvent → 累加工具参数 JSON
    ├─→ ContentBlockStopEvent → 工具参数完整，弹出审批对话框
    │       ↓ 用户点击"允许"
    │   向 claudecode 的 stdin 写入审批结果（JSON 格式）
    ├─→ ToolResultEvent → 显示工具执行结果
    ├─→ MessageDeltaEvent → 更新 Token 进度条（实时 output_tokens）
    ├─→ AssistantCompleteEvent → 持久化完整 assistant 消息
    ├─→ ResultEvent → 更新最终 Token 用量（contextWindow/inputTokens/outputTokens）
    └─→ MessageStopEvent → 标记流式结束，显示完成分割线
    ↓
持久化到 SQLite（消息 + 会话状态）
```

---

## 六、文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `ClaudeEventParser.cs` | 新增 | JSON-RPC 事件解析器 |
| `EventBus.cs` | 新增 | 本地事件总线 |
| `TimelineModel.cs` | 新增 | 时间线数据模型 |
| `ToolApprovalQueue.cs` | 新增 | 工具审批队列 + 对话框 |
| `TokenUsageTracker.cs` | 新增 | Token 用量跟踪 |
| `MainWindow.Session.cs` | 大幅改造 | 从文本流改为事件流 + RedirectStandardInput |
| `SessionTabItem.cs` | 改造 | ChatBubbles → TimelineModel |
| `MainWindow.Chat.cs` | 改造 | 新增工作日志、审批对话框渲染 |
| `PlanSidebar.xaml.cs` | 改造 | 从 TodoItem 改为 TurnPlan |
| `ConversationStore.cs` | 扩展 | 新增 Activities 表 |
| `CodeBlock.cs` | 新增 | 代码块控件（语法高亮 + 行号 + 复制） |
| `StreamingRenderer.cs` | 新增 | 逐字流式渲染器 |
| `ToolCallItem.cs` | 新增 | 工具调用卡片 |
| `CollapsibleGroup.cs` | 新增 | 可折叠面板 |
| `ApprovalDialog.xaml` | 新增 | 审批对话框 |
| `MainWindow.Chat.cs` | 重写 | 组件化气泡渲染 |
| `MainWindow.xaml` | 微调 | 布局 IDE 化

---

## 七、验证步骤

1. **验证 JSON 输出**：`claudecode --output-format stream-json --verbose --include-partial-messages -p "hello"` 确认有结构化输出
2. **事件解析**：单元测试解析各类型事件
3. **审批流程**：触发文件修改，验证对话框弹出
4. **Token 跟踪**：验证进度条实时更新
5. **持久化恢复**：关闭后重开，验证时间线完整恢复
6. **UI 验证**：
   - 流式文本逐字出现（确认 StreamingRenderer 帧率稳定）
   - 代码块语法高亮正确着色（关键字/字符串/注释区分）
   - 工具调用卡片实时更新状态（进行中→完成）
   - 审批对话框 JSON 参数渲染正确
   - 气泡悬停出现操作按钮（复制/反馈）
   - Token 指示器颜色随使用率变化（绿→黄→红）

---

## 八、与 T3code 的功能对比

| 功能 | T3code | 本方案 | 差距 |
|------|--------|--------|------|
| 流式文本 | ✅ | ✅ | 无 |
| 思考过程显示 | ✅ | ❌ | stream-json 无 thinking 事件（见限制） |
| 工具调用可视化 | ✅ | ✅ | 无 |
| 审批对话框 | ✅ | ✅ | 无 |
| Token 实时进度 | ✅ | ✅ | 无 |
| 任务清单 | ✅ | ✅ | 无 |
| 变更文件摘要 | ✅ | ⚠️ | 需手动解析 diff |
| 代码块语法高亮 | ✅ (Shiki) | ⚠️ | 可用 AvalonEdit 替代 |
| 多会话复用进程 | ✅ | ❌ | 每标签独立进程 |
| WebSocket 实时同步 | ✅ | ❌ | 纯本地无需 |

---

## 九、关键限制

1. **无进程复用**：claudecode 不支持持久进程，每轮对话必须启动新进程（非每标签，而是每次发送消息）
2. **审批阻塞**：claudecode 的审批是阻塞式，工具参数完整后通过 stdin 发送 JSON 审批结果
3. **跨标签状态**：切换标签时进程必须终止，依赖 SQLite 恢复对话状态
4. **无 thinking 事件**：实测 stream-json 格式不输出思考过程（区别于 T3code 的自定义 SDK）
5. **stream-json 依赖 --verbose**：`--output-format stream-json` 必须搭配 `--verbose` 使用

---

## 十一、UI/UX 设计方案

### 整体设计语言

以 **Visual Studio / VS Code** 的暗色 IDE 风格为基调，结合 **ChatGPT / T3code** 的聊天界面清晰度。核心原则：

| 原则 | 说明 |
|------|------|
| **信息密度适中** | 不浪费空间，但也不拥挤。行距 1.5、气泡间距 8px |
| **视觉层次分明** | 标题/代码/工具调用/文本块使用不同的背景色和字体 |
| **内容优先** | 减小边框和装饰元素占比，让对话内容占 80%+ 宽度 |
| **实时反馈** | 流式文本逐字出现，工具调用即时显示状态变化 |

### 1. 聊天气泡系统重构

#### 当前问题（MainWindow.Chat.cs）

```
┌──────────────────────────────────────────┐
│  Claude  14:30                           │
│  以下是修改后的代码：                    │  ← 纯文本，无语法高亮
│  ```python                              │
│  def hello():                            │    代码块和普通文本
│      print("world")                      │    视觉上无区别
│  ```                                     │
│                                          │
│  ─── 工具调用 ───                        │  ← 和文本混在一起
│  👁️ 读取文件 src/app.ts                  │
│  ✅ 执行命令 npm run build                │
└──────────────────────────────────────────┘
```

#### 目标设计

```
┌──────────────────────────────────────────────┐
│  ┌─── 用户气泡（右对齐，蓝色调） ──────────┐ │
│  │  你  14:30     [上下文]                  │ │  ← 圆角 12px，右对齐
│  │  帮我把 main 函数改成 async              │ │     浅蓝背景 (#0A1F38)
│  │  然后在里面加错误处理                    │ │     最大宽度 75%
│  └──────────────────────────────────────────┘ │
│                                               │
│  ┌─── AI 气泡（左对齐，灰色调） ──────────┐  │
│  │  Claude  14:30     [0.3s]  [⏳ 2.1k]   │  │  ← 显示耗时 + token
│  │                                         │  │     深灰背景 (#1C1C1E)
│  │  ┌─ 文本块 ──────────────────────────┐  │  │     左边框高亮色条
│  │  │ 以下是修改后的代码：               │  │  │
│  │  └───────────────────────────────────┘  │  │
│  │                                         │  │
│  │  ┌─ 代码块（Monaco 风格） ───────────┐  │  │  ← 等宽字体 Consolas
│  │  │  python   [📋 复制]               │  │  │     行号 + 语法高亮
│  │  │  1  def hello():                  │  │  │     深色背景 (#0D1117)
│  │  │  2      print("world")            │  │  │     圆角 8px
│  │  │  3                                │  │  │
│  │  └───────────────────────────────────┘  │  │
│  │                                         │  │
│  │  ┌─ 工具调用（折叠面板） ────────────┐  │  │  ← 可折叠，默认展开
│  │  │  ▼ 工具调用 (2)         0.8s      │  │  │     浅灰背景
│  │  │  ├─ 👁️ Read  src/app.ts   ✅     │  │  │     每个工具一行
│  │  │  └─ ⚡ Bash  npm run build  ✅    │  │  │     耗时 + 状态
│  │  └───────────────────────────────────┘  │  │
│  │                                         │  │
│  │  ┌─ 变更文件摘要 ────────────────────┐  │  │  ← 新增：显示改了什么
│  │  │  📝 修改文件 (2)                  │  │  │
│  │  │  ├─ src/app.ts    +12 -3          │  │  │     绿色 + 红色数字
│  │  │  └─ src/utils.ts  +5 -1           │  │  │
│  │  └───────────────────────────────────┘  │  │
│  └──────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

#### 气泡组件树

```
ChatBubble (Border)
├── BubbleHeader (Grid)
│   ├── Avatar (Ellipse, 20x20)        ← 首字母头像
│   ├── RoleName ("你" / "Claude")
│   ├── Timestamp ("14:30")
│   ├── Elapsed ("0.3s")               ← 仅 AI 气泡
│   └── TokenCount ("⏳ 2.1k")         ← 仅 AI 气泡
├── ContentArea (StackPanel)
│   ├── TextBlock                       ← 普通文本
│   ├── CodeBlock (Border)              ← 代码块（语法高亮）
│   │   ├── CodeHeader ("python  📋")
│   │   └── CodeBody (行号 + 代码)
│   ├── ToolCallGroup (Collapsible)     ← 工具调用组
│   │   ├── GroupHeader ("▼ 工具调用 (2)")
│   │   └── ToolCallItem × N
│   ├── FileSummary (Border)            ← 变更文件摘要
│   └── ThinkingBlock (Collapsible)     ← 思考过程（可折叠）
└── BubbleStatusBar (Border)            ← 底部状态
    ├── StreamingDot (Ellipse)          ← 闪烁点（流式进行中）
    └── StopReason ("tool_use" / "end_turn")
```

#### 自定义控件：CodeBlock（代码块渲染器）

```csharp
// CodeBlock.xaml — 代码块控件
public class CodeBlock : Control
{
    public string Code { get; set; }
    public string Language { get; set; }  // "csharp", "python", "xml"...
    public bool ShowLineNumbers { get; set; } = true;
    
    // 简单关键字高亮（不使用 Shiki，纯本地正则匹配）
    // 至少支持：csharp, python, javascript, xml, json, bash
    public FlowDocument Render()
    {
        // 1. 按语言加载关键字规则
        // 2. 逐行分析，关键字着不同颜色
        // 3. 行号用浅色 (#484F58) 渲染在左侧
        // 4. 返回 FlowDocument 用于 RichTextBox 展示
    }
}

// 语法高亮颜色（暗色主题）:
// ─────────────────────────────────
// 关键字 (if/for/class)  →  #569CD6  蓝
// 字符串 "..."           →  #CE9178  橙
// 注释 // /*            →  #6A9955  绿
// 数字 123              →  #B5CEA8  浅绿
// 类型名 string/int      →  #4EC9B0  青
// 方法调用 Hello()       →  #DCDCAA  黄
```

---

### 2. 工具调用可视化（ToolCallItem）

每个工具调用显示为独立卡片，实时更新状态：

```
┌────────────────────────────────────────────┐
│  ┌─ 进行中 ────────────────────────────┐   │
│  │  ⠋  Bash  npm run build   12.3s     │   │  ← 旋转动画
│  │  output: Building...                 │   │     半透明背景
│  │  [■■■■■■■□□□]                       │   │     进度条（如有）
│  └──────────────────────────────────────┘   │
│                                             │
│  ┌─ 已完成 ────────────────────────────┐   │
│  │  ✅  Read  src/app.ts     0.02s     │   │  ← 绿色勾
│  │  ── 文件内容 ──                     │   │     点击可展开内容
│  │  (点击展开内容)                      │   │
│  └──────────────────────────────────────┘   │
│                                             │
│  ┌─ 等待审批 ──────────────────────────┐   │
│  │  ⏸  Bash  npm install axios  0.0s   │   │  ← 黄色暂停图标
│  │  [参数预览]                           │   │     虚线边框
│  │  cwd: /project                       │   │
│  │  command: npm install axios          │   │
│  │  ┌──────┐ ┌────────┐                │   │
│  │  │ 允许 │ │ 拒绝   │                │   │  ← 两个按钮
│  │  └──────┘ └────────┘                │   │
│  └──────────────────────────────────────┘   │
└────────────────────────────────────────────┘
```

---

### 3. 审批对话框（ApprovalDialog）

模态对话框，展示工具参数并提供批准/拒绝/全部批准选项：

```
┌──────────────────────────────────────────────┐
│  ⚡  工具调用需要审批                          │  ← 标题栏
│                                              │
│  ┌── 工具信息 ──────────────────────────┐    │
│  │  工具:  Bash                          │    │
│  │  描述:  npm install axios             │    │
│  └───────────────────────────────────────┘    │
│                                              │
│  ┌── 参数详情（JSON 渲染） ─────────────┐    │
│  │  {                                    │    │  ← 语法高亮 JSON
│  │    "command": "npm install axios",    │    │     等宽字体
│  │    "cwd": "/project/src"              │    │     行号
│  │    "timeout": 30000                   │    │
│  │  }                                    │    │
│  └───────────────────────────────────────┘    │
│                                              │
│  ─── 本次会话使用此选项 ───                  │
│  ☐ 记住此次选择 (仅本次会话)                  │
│                                              │
│         [拒绝]    [允许本次]    [全部允许]    │
│                   (主要按钮，蓝色)             │
└──────────────────────────────────────────────┘
```

**XAML 结构**：
```xml
<Window x:Class="ClaudeCodeGUI.ApprovalDialog" ...>
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>     <!-- Header -->
            <RowDefinition Height="Auto"/>     <!-- Tool info -->
            <RowDefinition Height="*"/>        <!-- JSON params -->
            <RowDefinition Height="Auto"/>     <!-- Remember checkbox -->
            <RowDefinition Height="Auto"/>     <!-- Buttons -->
        </Grid.RowDefinitions>

        <!-- JSON 参数渲染使用 RichTextBox + 语法高亮 -->
        <RichTextBox Grid.Row="2" FontFamily="Consolas" FontSize="12"
                     Background="#0D1117" IsReadOnly="True"/>
    </Grid>
</Window>
```

---

### 4. 时间线/活动日志（Streaming Timeline）

实时显示当前轮次的活动流，位于 ThinkingBar 区域：

```
┌──────────────────────────────────────────────┐
│  ⠋  正在处理...                    12.3s  ⏹ │  ← ThinkingBar
│  ████████████░░░░░░░░  65%                  │  ← Token 进度条
│                                              │
│  ┌── 活动日志 ───────────────────────────┐   │
│  │  ✓ 准备参数                            │   │  ← 已完成项（绿色）
│  │  ✓ 发送请求                            │   │
│  │  ✓ 接收响应                            │   │
│  │  ✓ Read  src/app.ts          0.02s    │   │
│  │  ⏳ Bash  npm run build       3.5s    │   │  ← 当前项（黄色）
│  └───────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

**行为规则**：
- 活动日志最多显示最近 5 条（防滚动溢出）
- 当前活动项显示旋转动画 + 已耗时
- 已完成项打勾后 2 秒变灰
- 鼠标悬停在活动日志区域可滚动查看全部

---

### 5. Token 用量指示器

当前设计用了 Path 画弧线，但不够直观。改为组合显示：

```
┌────────────────┐
│  ⏣  45%        │  ← 圆形进度环（arc 实现）
│     200k       │     颜色渐变：绿 → 黄 → 红（>80%）
│     90k/200k   │     下方小字：已用 / 总量
└────────────────┘

鼠标悬停 ToolTip:
  输入: 35,000 tokens
  输出: 55,000 tokens
  总计: 90,000 / 200,000 tokens
  上下文: 200,000 tokens
```

**进度环颜色规则**：
| 使用率 | 颜色 | 色值 |
|--------|------|------|
| 0-60% | 绿 | `#22C55E` |
| 60-80% | 黄 | `#F59E0B` |
| 80-95% | 橙 | `#F97316` |
| 95%+ | 红 | `#EF4444`（闪烁动画） |

---

### 6. 流式文本渲染动画

当前：`tb.Text += clean` — 整段文本一次性出现。

**目标**：逐字/逐块出现，但有策略：

| 内容类型 | 渲染方式 | 速度 |
|---------|---------|------|
| 普通文本 | 逐字出现 | 30-50 chars/帧 |
| 代码块 | 整行出现 | 逐行，每行 50ms |
| 工具调用 | 整块出现（状态变化即更新） | 即时 |
| 思考过程 | 折叠状态，可选展开查看 | — |

```csharp
// 流式渲染调度器
public class StreamingRenderer
{
    private readonly StringBuilder _buffer = new();
    private readonly DispatcherTimer _timer;    // 16ms 帧率 (~60fps)
    private bool _isDirty;
    
    // 文本增量到达时追加到 buffer，标记脏
    public void AppendText(string delta)
    {
        _buffer.Append(delta);
        _isDirty = true;
    }
    
    // 每帧从 buffer 取固定字符数追加到 UI
    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isDirty) return;
        
        int charsToRender = Math.Min(40, _buffer.Length);
        var chunk = _buffer.ToString(0, charsToRender);
        _buffer.Remove(0, charsToRender);
        
        // 追加到 TextBlock（在 UI 线程）
        _textBlock.Text += chunk;
        _isDirty = _buffer.Length > 0;
    }
}
```

**注意**：流式渲染器只在文本增量流入期间工作。当 `MessageStopEvent` 到达时，剩余的 buffer 全部刷出。不影响普通文本的复制粘贴（复制时读取完整文本而非显示文本）。

---

### 7. 气泡的默认和悬停状态

```
┌── 用户气泡 ─────────────────────────┐
│  你  14:30                          │
│  帮我把 main 函数改成 async         │
│  [📋] [🔗]                     ←  │  ← 悬停才显示操作按钮
└────────────────────────────────────┘
                                       ← 悬停时：
┌── AI 气泡 ──────────────────────────┐   背景轻微变亮
│  Claude  14:30  [0.3s]             │   操作按钮淡入
│  以下是修改后的代码：               │
│  ┌─ 代码块 ──────────────────────┐  │
│  │  python   [📋 复制]           │  │  ← 始终显示
│  └───────────────────────────────┘  │
│                                     │
│  [📋 复制]  [👍]  [👎]  [🔄 重试]  │  ← 悬停出现
└────────────────────────────────────┘
```

---

### 8. 布局微调（IDE 化改造）

| 区域 | 当前 | 目标 |
|------|------|------|
| **侧边栏** | 230px 固定 | 可拖动调节宽度（已实现 MinWidth/MaxWidth），增加折叠按钮 |
| **标题栏** | 包含标签栏 | 保持当前布局，微调间距 |
| **输入框** | 居中，44px 最小高度 | 底部对齐，宽 90%，与发送按钮合并区域 |
| **ThinkingBar** | 简单 spinner + 文本 | 增加活动日志 + 实时 token 进度条 |
| **状态栏** | 单行文本 | 增加左右分栏：左=工作目录，右=模型名 |
| **底部工具栏** | 所有按钮平铺 | 分组：左=模型，中=模式，右=操作 |
| **AI 气泡** | 无宽度限制 | 最大宽度 80%，留出右侧呼吸空间 |

**具体尺寸改动**：

```
ChatPanel 留白:
  ┌──────┬────────────────────────────┬──────┐
  │ 12px │     气泡区域 (max 80%)     │  ←*  │  ← 右侧留白空间
  │      │                            │      │     让视觉聚焦左侧
  └──────┴────────────────────────────┴──────┘
  
  用户气泡：向右对齐（Margin.Left Auto）
  AI 气泡：向左对齐（Margin.Right 可调）
  
  代码块圆角: 8px（比气泡小 2px，分层感）
  行号宽度: 40px，灰色背景
```

---

### 9. 关键 UI 代码变更清单

| 文件 | 变更 | 说明 |
|------|------|------|
| `MainWindow.Chat.cs` | 重写 | 气泡 → 组件化渲染，支持 CodeBlock/ToolCallGroup/FileSummary |
| `CodeBlock.cs` | 新增 | 代码块控件（语法高亮、行号、复制按钮） |
| `StreamingRenderer.cs` | 新增 | 流式文本逐字渲染器（60fps 帧率控制） |
| `ToolCallItem.cs` | 新增 | 工具调用卡片控件 |
| `CollapsibleGroup.cs` | 新增 | 可折叠面板控件 |
| `ApprovalDialog.xaml` | 新增 | 审批对话框（JSON 渲染 + 记住选择） |
| `TokenIndicator.xaml` | 改造 | 圆形进度环 + 颜色分级 + ToolTip |
| `MainWindow.xaml` | 微调 | 侧边栏可折叠、输入框布局优化 |
| `Theme.cs` | 扩展 | 新增 CodeBg/Syntax* / ToolCall* / Bubble* / Approval* / Diff* 属性 |
| `ThemeManager.cs` | 扩展 | ApplyTheme() 新增 20+ 行资源设置 |
| `TimelineModel.cs` | 扩展 | 新增工具调用状态跟踪、耗时记录 |

### 10. 主题集成（核心：所有颜色通过 Theme 系统）

**⚠️ 关键设计决策：所有新增颜色必须添加到 Theme.cs + ThemeManager.cs，通过 `{DynamicResource}` 绑定，禁止任何硬编码色值。** 否则浅色主题（如 WarmPaper）下代码块背景 `#0D1117` 会一片漆黑，文字完全不可见。

#### 10a. Theme.cs 新增属性

```csharp
// 追加到 Theme.cs 类中
public class Theme
{
    // ... 现有属性 ...

    // ═══ 新增：代码块颜色 ═══
    public SolidColorBrush CodeBg { get; set; }       // 代码块背景
    public SolidColorBrush CodeLineNum { get; set; }  // 行号颜色
    public SolidColorBrush CodeHeaderBg { get; set; } // 代码块头部背景
    public SolidColorBrush CodeBorder { get; set; }   // 代码块边框

    // ═══ 新增：语法高亮颜色 ═══
    public SolidColorBrush SyntaxKeyword { get; set; }  // 关键字(if/for/class)
    public SolidColorBrush SyntaxString { get; set; }   // 字符串 "..." 
    public SolidColorBrush SyntaxComment { get; set; }  // 注释 // /*
    public SolidColorBrush SyntaxNumber { get; set; }   // 数字 123
    public SolidColorBrush SyntaxType { get; set; }     // 类型名 string/int
    public SolidColorBrush SyntaxMethod { get; set; }   // 方法调用 Hello()
    public SolidColorBrush SyntaxOperator { get; set; } // 运算符 + - =

    // ═══ 新增：工具调用颜色 ═══
    public SolidColorBrush ToolCallBg { get; set; }     // 工具调用卡片背景
    public SolidColorBrush ToolCallBorder { get; set; } // 工具调用边框
    public SolidColorBrush ToolCallRunning { get; set; }// 进行中状态色
    public SolidColorBrush ToolCallPending { get; set; }// 等待审批状态色
    public SolidColorBrush ToolCallDone { get; set; }   // 已完成状态色

    // ═══ 新增：气泡增强颜色 ═══
    public SolidColorBrush BubbleHeaderFg { get; set; } // 气泡头部姓名颜色
    public SolidColorBrush BubbleTimeFg { get; set; }   // 气泡时间戳颜色
    public SolidColorBrush BubbleAccentBorder { get; set; } // AI 气泡左边条

    // ═══ 新增：审批对话框颜色 ═══
    public SolidColorBrush ApprovalBg { get; set; }     // 对话框背景
    public SolidColorBrush ApprovalBorder { get; set; } // 对话框边框

    // ═══ 新增：变更文件摘要颜色 ═══
    public SolidColorBrush DiffAdded { get; set; }      // 新增行 +N 绿色
    public SolidColorBrush DiffRemoved { get; set; }    // 删除行 -N 红色
}
```

#### 10b. ThemeManager.ApplyTheme 新增行

```csharp
// 在 ApplyTheme() 方法末尾追加
private void ApplyTheme(Theme theme)
{
    // ... 现有代码 ...

    // 代码块
    app.Resources["ThemeCodeBg"] = theme.CodeBg;
    app.Resources["ThemeCodeLineNum"] = theme.CodeLineNum;
    app.Resources["ThemeCodeHeaderBg"] = theme.CodeHeaderBg;
    app.Resources["ThemeCodeBorder"] = theme.CodeBorder;

    // 语法高亮
    app.Resources["SyntaxKeyword"] = theme.SyntaxKeyword;
    app.Resources["SyntaxString"] = theme.SyntaxString;
    app.Resources["SyntaxComment"] = theme.SyntaxComment;
    app.Resources["SyntaxNumber"] = theme.SyntaxNumber;
    app.Resources["SyntaxType"] = theme.SyntaxType;
    app.Resources["SyntaxMethod"] = theme.SyntaxMethod;
    app.Resources["SyntaxOperator"] = theme.SyntaxOperator;

    // 工具调用
    app.Resources["ThemeToolCallBg"] = theme.ToolCallBg;
    app.Resources["ThemeToolCallBorder"] = theme.ToolCallBorder;
    app.Resources["ThemeToolCallRunning"] = theme.ToolCallRunning;
    app.Resources["ThemeToolCallPending"] = theme.ToolCallPending;
    app.Resources["ThemeToolCallDone"] = theme.ToolCallDone;

    // 气泡增强
    app.Resources["ThemeBubbleHeaderFg"] = theme.BubbleHeaderFg;
    app.Resources["ThemeBubbleTimeFg"] = theme.BubbleTimeFg;
    app.Resources["ThemeBubbleAccentBorder"] = theme.BubbleAccentBorder;

    // 审批对话框
    app.Resources["ThemeApprovalBg"] = theme.ApprovalBg;
    app.Resources["ThemeApprovalBorder"] = theme.ApprovalBorder;

    // 变更文件摘要
    app.Resources["ThemeDiffAdded"] = theme.DiffAdded;
    app.Resources["ThemeDiffRemoved"] = theme.DiffRemoved;
}
```

#### 10c. 各主题色值对照表

每个主题的工厂方法必须为所有新增属性赋值。以下是跨主题的色值映射：

| Theme 属性 | Default | GitHub | Apple | Monokai | Dracula | WarmPaper |
|-----------|---------|--------|-------|---------|---------|-----------|
| **CodeBg** | `#0D1117` | `#010409` | `#000000` | `#272822` | `#282A36` | `#F6F2E8` |
| **CodeLineNum** | `#484F58` | `#21262D` | `#48484A` | `#75715E` | `#6272A4` | `#C4B8A0` |
| **CodeHeaderBg** | `#161B22` | `#0D1117` | `#1C1C1E` | `#3E3D32` | `#44475A` | `#EDE8DC` |
| **CodeBorder** | `#21262D` | `#21262D` | `#2C2C2E` | `#49483E` | `#44475A` | `#D4C9B5` |
| **SyntaxKeyword** | `#569CD6` | `#58A6FF` | `#0A84FF` | `#66D9EF` | `#FF79C6` | `#4A7C59` |
| **SyntaxString** | `#CE9178` | `#A5D6FF` | `#FF9F0A` | `#E6DB74` | `#F1FA8C` | `#8B4513` |
| **SyntaxComment** | `#6A9955` | `#8B949E` | `#636366` | `#75715E` | `#6272A4` | `#8A8478` |
| **SyntaxNumber** | `#B5CEA8` | `#79C0FF` | `#BF5AF2` | `#AE81FF` | `#BD93F9` | `#4682B4` |
| **SyntaxType** | `#4EC9B0` | `#3FB950` | `#30D158` | `#A6E22E` | `#50FA7B` | `#5A9469` |
| **SyntaxMethod** | `#DCDCAA` | `#D2A8FF` | `#FFD60A` | `#F8F8F2` | `#8BE9FD` | `#B8860B` |
| **SyntaxOperator** | `#D4D4D4` | `#E6EDF3` | `#EBEBF0` | `#F8F8F2` | `#F8F8F2` | `#2D2A24` |
| **ToolCallBg** | `#161B22` | `#0D1117` | `#1C1C1E` | `#3E3D32` | `#44475A` | `#EDE8DC` |
| **ToolCallBorder** | `#30363D` | `#21262D` | `#3A3A3C` | `#49483E` | `#6272A4` | `#C4B8A0` |
| **ToolCallRunning** | `#3B82F6` | `#58A6FF` | `#0A84FF` | `#66D9EF` | `#8BE9FD` | `#4682B4` |
| **ToolCallPending** | `#F59E0B` | `#D29922` | `#FFD60A` | `#E6DB74` | `#F1FA8C` | `#B8860B` |
| **ToolCallDone** | `#22C55E` | `#3FB950` | `#30D158` | `#A6E22E` | `#50FA7B` | `#4A7C59` |
| **BubbleHeaderFg** | `#E8E8E8` | `#E6EDF3` | `#EBEBF0` | `#F8F8F2` | `#F8F8F2` | `#2D2A24` |
| **BubbleTimeFg** | `#6B7280` | `#6E7681` | `#636366` | `#75715E` | `#6272A4` | `#8A8478` |
| **BubbleAccentBorder** | `#3B82F6` | `#58A6FF` | `#0A84FF` | `#66D9EF` | `#BD93F9` | `#4A7C59` |
| **ApprovalBg** | `#1C1C1E` | `#161B22` | `#2C2C2E` | `#49483E` | `#44475A` | `#EDE8DC` |
| **ApprovalBorder** | `#30363D` | `#21262D` | `#3A3A3C` | `#55554F` | `#6272A4` | `#D4C9B5` |
| **DiffAdded** | `#22C55E` | `#3FB950` | `#30D158` | `#A6E22E` | `#50FA7B` | `#4A7C59` |
| **DiffRemoved** | `#EF4444` | `#F85149` | `#FF453A` | `#F92672` | `#FF5555` | `#8B4513` |

#### 10d. XAML 使用方式（禁止硬编码）

```xml
<!-- ✅ 正确：通过 DynamicResource 绑定主题颜色 -->
<Border Background="{DynamicResource ThemeCodeBg}"
        BorderBrush="{DynamicResource ThemeCodeBorder}"
        CornerRadius="8">
    <TextBlock Foreground="{DynamicResource SyntaxKeyword}"
               FontFamily="Consolas" FontSize="13"/>
</Border>

<!-- ❌ 错误：硬编码颜色 — 浅色主题下不可见 -->
<Border Background="#0D1117" ...>
    <TextBlock Foreground="#569CD6" .../>
</Border>
```

#### 10e. 代码块语法高亮器的主题感知

CodeBlock 控件从 `Application.Current.Resources` 读取语法高亮色：

```csharp
public class CodeBlock : Control
{
    // 从当前主题动态获取语法颜色
    private SolidColorBrush GetSyntaxColor(string key)
    {
        return Application.Current.Resources[key] as SolidColorBrush
               ?? new SolidColorBrush(Colors.Gray);
    }

    public FlowDocument Render()
    {
        // 每次渲染时读取主题色，而非缓存
        var keywordColor = GetSyntaxColor("SyntaxKeyword");
        var stringColor = GetSyntaxColor("SyntaxString");
        var commentColor = GetSyntaxColor("SyntaxComment");
        // ... 逐行分析 + 着色
    }
}
```

**注意**：由于 WPF 的 `FlowDocument`/`RichTextBox` 不支持 `DynamicResource` 直接用于内联文本的 `Foreground`，所以在渲染代码块时**必须**在代码中从 `Application.Current.Resources` 读取当前主题色。当用户切换主题时，需要调用 `CodeBlock.InvalidateVisual()` 触发重绘。

#### 10f. Token 进度环颜色 — 也使用主题资源

```csharp
// 进度环颜色从主题资源读取，而非硬编码
private SolidColorBrush GetTokenRingColor(double percentage)
{
    var resources = Application.Current.Resources;
    if (percentage >= 0.95) return resources["ThemeError"] as SolidColorBrush;
    if (percentage >= 0.80) return resources["ThemeWarning"] as SolidColorBrush;
    if (percentage >= 0.60) return resources["ThemeInfo"] as SolidColorBrush;
    return resources["ThemeSuccess"] as SolidColorBrush;
}
```

---



## 十二、分阶段构建计划（整合版）

将全部功能划分为 6 个阶段，每阶段完成后应用可正常工作，后续阶段只增加功能不修改前一阶段代码。

```
阶段 1 ─ 基础设施 ✅（2026-05-08 完成）
 ├─ Theme.cs + ThemeManager.cs 扩展（所有新颜色属性 + 6 主题色值）
 ├─ ClaudeEventParser.cs + EventBus.cs（JSON-RPC 事件解析）
 ├─ TokenUsageTracker.cs（token 用量跟踪）
 └─ SessionState.cs 扩展（新增 InputTokens/OutputTokens 属性）
     → 验证：切换 6 套主题无颜色冲突，JSON 行可正确解析

阶段 2 ─ 核心聊天引擎 ✅（2026-05-08 完成）
 ├─ MainWindow.Session.cs 改造（--output-format stream-json + RedirectStandardInput + 事件流）
 ├─ TimelineModel.cs（取代 ChatBubbles List<UIElement>）
 ├─ MainWindow.Chat.cs 气泡组件化重构（基于 TimelineModel 渲染 + 新增 RenderTimelineToPanel/LoadMessagesToTimeline）
 └─ ConversationStore.cs 扩展（kind 列 + SaveTimelineMessages + SaveEntry）
     → 验证：发送消息，流式结束，持久化到 SQLite，重启后恢复

阶段 3 ─ 实时流式体验 ✅（2026-05-08 完成）
 ├─ StreamingRenderer 逐字渲染器（60fps 帧率控制）
 ├─ Token 指示器改造（进度环 + 颜色分级 + ToolTip）
 └─ ThinkingBar 活动日志（实时显示当前轮次活动）
     → 验证：文本逐字出现，Token 环实时更新，日志滚动正确

阶段 4 ─ 工具交互闭环 ✅（2026-05-08 完成）
 ├─ ApprovalDialog 审批对话框（JSON 参数 + 记住选择）
 ├─ ToolCallItem 工具调用卡片（三态：进行中/等待审批/完成）
 └─ MainWindow.Session.cs 工具事件订阅 + stdin 写入 + 时间线集成
     → 验证：触发工具调用 → 弹出审批 → 允许 → 工具执行 → 状态更新

阶段 5 ─ 内容增强 ✅（2026-05-08 完成）
 ├─ CodeBlock 代码语法高亮（6 种语言 + 行号 + 复制按钮）
 ├─ CollapsibleGroup 可折叠面板（工具组/文件摘要/思考过程）
 └─ FileSummary 变更文件摘要（+N/-N 统计）
     → 验证：代码块着色正确，折叠展开动画流畅，diff 数字颜色区分

阶段 6 ─ 细节打磨 ✅（2026-05-08 完成）
 ├─ 气泡悬停操作按钮（复制/反馈/重试）
 ├─ 气泡过渡动画（出现/切换/状态变化）
 ├─ ApprovalDialog 动画（淡入/滑入）
 └─ 全局布局微调（侧边栏折叠动画、间距优化）
     → 验证：悬停按钮淡入，气泡滑入动画，无布局抖动
```

### 阶段依赖关系

```
阶段 1 ────→ 阶段 2 ────→ 阶段 3 ────→ 阶段 4 ────→ 阶段 5 ────→ 阶段 6
  (基础)      (骨架)      (实时性)      (工具交互)    (内容增强)    (打磨)
                  │                          │
                  ↓                          ↓
            ConversationStore          ApprovalDialog
            可滞后到阶段 3 完善          依赖阶段 3 的 StreamingRenderer
```

### 核心原则

1. **每阶段结束后代码可编译、可运行** — 不允许半截代码合并到主分支
2. **前一阶段不依赖后一阶段** — 阶段 5 的 CodeBlock 不需要等待阶段 6
3. **Theme 属性在设计时一次性定义** — 阶段 1 就定义所有颜色属性，各主题工厂方法也一次性赋完值，避免后续反复修改 Theme.cs
4. **ConversationStore 兼容** — 消息格式从纯文本改为结构化后，旧消息仍可正常显示（role 字段不变）

---

## 附录：claudecode 结构化事件完整列表

### 顶层事件（外层 type）

| 顶层 type | 说明 | 重要字段 |
|-----------|------|---------|
| `system` | 系统初始化 | `subtype: "init"`, `session_id`, `model`, `tools[]`, `version` |
| `stream_event` | 流式事件（需解包 event） | `event.type` 见下方 |
| `assistant` | 完整 Assistant 消息 | `message.id`, `message.content[]`, `message.usage` |
| `user` | 工具执行结果 | `message.role: "user"`, `tool_use_result` |
| `result` | 最终结果（含用量） | `subtype: "success"`, `modelUsage.contextWindow/inputTokens/outputTokens` |

### stream_event 内部事件（event.type）

| 内部 type | 说明 | 关键子字段 |
|-----------|------|-----------|
| `message_start` | 消息开始 | `message.id`, `message.model` |
| `content_block_start` | 内容块开始 | `index`, `content_block.type`: `"text"` 或 `"tool_use"` |
| `content_block_delta` | 内容增量 | `index`, `delta.type`: `"text_delta"`/`"input_json_delta"` |
| `content_block_stop` | 内容块结束 | `index` |
| `message_delta` | 消息增量 | `delta.stop_reason`, `usage.output_tokens` |
| `message_stop` | 消息结束 | 无 |

### 关键区别（vs 原文档假设）

1. **无单独的 tool_use_start/complete** — 工具调用作为 content_block（type=tool_use），通过 `content_block_start` + `input_json_delta` + `content_block_stop` 三段式表达
2. **无 thinking 事件** — claudecode 的 stream-json 格式不输出思考过程（仅在 T3code 的自定义 SDK 中存在）
3. **usage 在 message_delta 和 result 中** — 实时用量在 `message_delta.usage.output_tokens`，最终用量在 `result.modelUsage`
4. **完整消息在流结束** — `assistant` 事件包含完整消息内容（可用于回填持久化）
5. **工具结果通过 `user` 事件** — 发送工具执行结果给模型时触发
