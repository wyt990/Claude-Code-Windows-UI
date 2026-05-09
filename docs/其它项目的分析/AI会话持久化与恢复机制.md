# AI 会话持久化与恢复机制

> 分析 T3code 中 AI 会话（Session）的持久化与恢复机制。
> 涵盖：会话 ID 的生成、获取、保存、恢复流程，消息历史的本地存储，以及完整的生命周期。
> 适用于在其它项目中复刻此模式的参考。

---

## 一、核心概念

| 概念 | 说明 | 生命周期 |
|---|---|---|
| **Thread** | 对话线程（UI 中的一个聊天） | 长期持久化（SQLite） |
| **Session** | AI 进程会话（= 一个 claudecode 进程） | 随进程启停 |
| **Turn** | 一次"用户发消息 → AI 回复"的交互 | 每次对话一个 turn |
| **ResumeCursor** | 用于恢复 session 的游标数据 | 随 session 持久化 |

### 关键原则

1. **消息历史存在本地 SQLite**，UI 渲染从不依赖 AI 进程
2. **AI 进程在同一个 thread 内被复用**，后续 turn 不重建进程
3. **会话恢复使用 `resume` + session UUID**，不重新注入历史消息
4. **会话 ID 由服务端生成**，通过 SDK 传递给 AI 进程

---

## 二、数据库表结构

### 2.1 事件存储表（source of truth）

```sql
CREATE TABLE IF NOT EXISTS orchestration_events (
  sequence INTEGER PRIMARY KEY AUTOINCREMENT,
  event_id TEXT UNIQUE,
  aggregate_kind TEXT,        -- "project" | "thread"
  stream_id TEXT,             -- projectId 或 threadId
  stream_version INTEGER,     -- 每个 aggregate 内递增
  event_type TEXT,            -- "thread.message-sent" | "thread.session-set" | ...
  occurred_at TEXT,           -- ISO 时间戳
  command_id TEXT,
  payload_json TEXT,          -- 事件负载（消息内容、会话状态等）
  metadata_json TEXT          -- 提供者元数据
);
```

### 2.2 消息投影表（read model）

```sql
CREATE TABLE IF NOT EXISTS projection_thread_messages (
  message_id TEXT PRIMARY KEY,
  thread_id TEXT NOT NULL,
  turn_id TEXT,
  role TEXT NOT NULL,             -- "user" | "assistant" | "system"
  text TEXT NOT NULL,              -- 消息全文
  is_streaming INTEGER NOT NULL,   -- 是否正在流式传输
  created_at TEXT,
  updated_at TEXT
);

CREATE TABLE IF NOT EXISTS projection_thread_activities (
  activity_id TEXT PRIMARY KEY,
  thread_id TEXT,
  turn_id TEXT,
  tone TEXT,          -- "info" | "error" | "tool" | "approval"
  kind TEXT,          -- "tool.started" | "tool.updated" | "task.progress" | ...
  summary TEXT,
  payload_json TEXT,
  created_at TEXT
);

CREATE TABLE IF NOT EXISTS projection_thread_sessions (
  thread_id TEXT PRIMARY KEY,
  status TEXT,             -- "running" | "ready" | "stopped" | "failed"
  provider_name TEXT,
  active_turn_id TEXT,
  last_error TEXT,
  updated_at TEXT
);

CREATE TABLE IF NOT EXISTS projection_turns (
  thread_id TEXT,
  turn_id TEXT,
  state TEXT               -- "started" | "completed" | "failed" | "interrupted"
  -- 还有其他字段：checkpoint 相关
);
```

### 2.3 Session 运行时表（恢复关键）

```sql
CREATE TABLE IF NOT EXISTS provider_session_runtime (
  thread_id TEXT PRIMARY KEY,
  provider_name TEXT NOT NULL,     -- "claude" | "codex" | "opencode"
  adapter_key TEXT NOT NULL,
  runtime_mode TEXT NOT NULL DEFAULT 'full-access',
  status TEXT NOT NULL,            -- "running" | "ready" | "stopped"
  last_seen_at TEXT NOT NULL,
  resume_cursor_json TEXT,         -- ← 恢复游标（JSON 字符串）
  runtime_payload_json TEXT        -- ← 运行时负载（cwd, modelSelection 等）
);

CREATE INDEX IF NOT EXISTS idx_provider_session_runtime_status
  ON provider_session_runtime(status);

CREATE INDEX IF NOT EXISTS idx_provider_session_runtime_provider
  ON provider_session_runtime(provider_name);
```

---

## 三、会话 ID 的生成

### 3.1 何时生成

在 `startSession()` 时生成，逻辑如下：

```
startSession(input)
  ├─ readClaudeResumeState(input.resumeCursor)
  │   如果存在 resumeCursor：
  │     提取 resumeState.resume（之前保存的 session UUID）
  │     → existingResumeSessionId = resumeState.resume
  │   如果不存在 resumeCursor：
  │     → existingResumeSessionId = undefined
  │
  ├─ 如果 existingResumeSessionId 存在（恢复场景）：
  │     newSessionId = undefined          ← 不生成新 UUID
  │     sessionId = existingResumeSessionId
  │
  └─ 如果 existingResumeSessionId 不存在（新建场景）：
        newSessionId = Random.nextUUIDv4  ← 生成新 UUID
        sessionId = newSessionId
```

### 3.2 生成方式

```csharp
// 对应 C# 实现
Guid sessionId = Guid.NewGuid().ToString("D");  // 格式: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
```

### 3.3 两个关键 ID 的区别

| ID | 生成时机 | 用途 | 谁维护 |
|---|---|---|---|
| **session UUID** | `startSession()` 时 | 传给 SDK 的 `sessionId` / `resume` 参数，用于 AI 进程内部恢复 | 服务端生成，SDK 消费 |
| **message ID** | `sendTurn()` 时 | 标识每条消息，用于 UI 渲染和流式文本追加 | 服务端生成，持久化到 DB |
| **turn ID** | `sendTurn()` 时 | 标识一次完整交互，用于关联消息和活动 | 服务端生成，持久化到 DB |
| **thread ID** | 创建线程时 | 标识一个对话线程 | 服务端生成，持久化到 DB |

---

## 四、ResumeCursor 数据结构

### 4.1 结构定义

```json
{
  "threadId": "thread-uuid",
  "resume": "claude-session-uuid",        // SDK 分配的 session UUID
  "resumeSessionAt": "last-assistant-uuid", // 上次最后一条 assistant 消息的 UUID
  "turnCount": 5                             // 已完成的 turn 数
}
```

### 4.2 构建时机（写入）

每次 turn 完成时更新：

```
completeTurn()
  ├─ ...（处理 token 用量、活动等）
  └─ updateResumeCursor(context)
      ├─ resumeCursor = {
      │     threadId: context.session.threadId,
      │     resume: context.resumeSessionId,     // SDK 分配的 session UUID
      │     resumeSessionAt: context.lastAssistantUuid,
      │     turnCount: context.turns.length
      │   }
      └─ context.session.resumeCursor = resumeCursor
```

### 4.3 持久化路径

```
updateResumeCursor → context.session.resumeCursor 更新
  ↓
sendTurn 返回 { threadId, turnId, resumeCursor }
  ↓
ProviderService.sendTurn()
  ├─ 从 turn 返回值中提取 turn.resumeCursor
  └─ directory.upsert({ threadId, provider, status, resumeCursor, runtimePayload })
      ↓
ProviderSessionDirectory.upsert()
  └─ repository.upsert({ threadId, providerName, adapterKey, resumeCursor, ... })
      ↓
SQL: INSERT INTO provider_session_runtime (... resume_cursor_json ...)
    VALUES (... JSON.stringify(resumeCursor) ...)
    ON CONFLICT (thread_id) DO UPDATE SET resume_cursor_json = excluded.resume_cursor_json
```

### 4.4 恢复路径（读取）

```
用户发送新消息（已有对话历史）
  ↓
ProviderService.sendTurn()
  └─ resolveRoutableSession({ allowRecovery: true })
      ↓
检查 adapter.hasSession(threadId)
  ├─ 有活跃 session → 直接复用
  └─ 没有活跃 session → recoverSessionForThread(binding)
       ↓
从 provider_session_runtime 表读取 resumeCursor：
  directory.getBinding(threadId) → { resumeCursor: {...} }
  ↓
adapter.startSession({
    threadId,
    resumeCursor: binding.resumeCursor,  ← 关键：传入恢复游标
    cwd,
    modelSelection,
    runtimeMode
  })
  ↓
ClaudeAdapter.startSession()
  ├─ readClaudeResumeState(input.resumeCursor)
  │    → { resume: "session-uuid", resumeSessionAt: "msg-uuid", turnCount: 5 }
  ├─ existingResumeSessionId = resumeState.resume  // "session-uuid"
  ├─ newSessionId = undefined  // 有现有 ID，不生成新的
  ├─ sessionId = existingResumeSessionId
  └─ query({ resume: sessionId, ... })
      ↓
SDK 内部：claudecode --resume "session-uuid"
  → claudecode 从自己的内部状态恢复之前的对话
```

---

## 五、完整生命周期图

### 5.1 首次对话（新建会话）

```
用户创建新线程并发送第一条消息
  │
  ├─ 1. 服务端创建 threadId (UUID)
  ├─ 2. startSession()
  │     ├─ resumeCursor = null（无恢复游标）
  │     ├─ newSessionId = Random.nextUUIDv4
  │     ├─ sessionId = newSessionId
  │     └─ query({ sessionId: newSessionId, ... })
  │          → SDK 内部 spawn claudecode 进程
  │          → --session-id "new-uuid"
  │
  ├─ 3. sendTurn() → SDK 处理 → turn.completed
  │     └─ updateResumeCursor()
  │          → resumeCursor = { threadId, resume: sessionId, turnCount: 1 }
  │          → 写入 provider_session_runtime 表
  │
  └─ 4. 消息通过事件系统持久化到 orchestration_events + projection_thread_messages
```

### 5.2 继续对话（复用进程）

```
同一线程中发送第二条消息
  │
  ├─ 1. resolveRoutableSession()
  │     └─ adapter.hasSession(threadId) → true（进程还在）
  │          → 直接复用！不调用 startSession()
  │
  ├─ 2. sendTurn() → 推入 promptQueue
  │     → 同一个 claudecode 进程处理
  │     → 进程不重启
  │
  └─ 3. turn.completed → updateResumeCursor()
       → turnCount: 2
       → 更新 provider_session_runtime
```

### 5.3 重新打开线程（恢复会话）

```
用户关闭线程后重新打开
  │
  ├─ 1. UI 调用 subscribeThread(threadId)
  │     └─ 服务端从 SQLite 投影表读取所有消息/活动
  │          → 返回完整 snapshot（含全部历史）
  │          → UI 立即显示历史消息（不依赖 AI 进程）
  │
  └─ 2. 用户发送新消息
       ├─ resolveRoutableSession({ allowRecovery: true })
       │    ├─ adapter.hasSession(threadId) → false（进程已退出）
       │    └─ recoverSessionForThread(binding)
       │         ├─ 从 SQLite 读取 resumeCursor
       │         │  { resume: "session-uuid", turnCount: 5 }
       │         └─ adapter.startSession({ resumeCursor: {...} })
       │              ├─ readClaudeResumeState → resume = "session-uuid"
       │              ├─ sessionId = "session-uuid"（复用旧 ID）
       │              └─ query({ resume: "session-uuid", ... })
       │                   → SDK: claudecode --resume "session-uuid"
       │                   → claudecode 恢复内部状态
       │
       └─ sendTurn() → 正常处理
```

### 5.4 服务器重启后

```
服务端重启
  │
  ├─ 1. reconcileStartupStaleRunningSessions()
  │     ├─ 遍历所有 session.status = "running" 的线程
  │     ├─ 检查是否有活跃的进程
  │     └─ 没有 → 分发 thread.session.stop
  │          → 投影表中状态改为 "stopped"
  │
  └─ 2. 用户发送新消息
       └─ 同 5.3 的恢复流程
            → 从 SQLite 读取 resumeCursor
            → startSession({ resumeCursor })
            → claudecode --resume "session-uuid"
```

---

## 六、传给 AI 进程的关键参数

### 6.1 SDK query() 选项

```typescript
// 给 Claude Agent SDK 的完整选项
{
  cwd: "/path/to/workspace",            // 工作目录
  model: "claude-sonnet-4-6",           // 模型 ID
  
  // === 会话标识（二选一）===
  sessionId: "new-uuid",                // 新建会话时：新 UUID
  resume: "existing-uuid",              // 恢复会话时：已有 session UUID
  
  // === 权限控制 ===
  permissionMode: "default"             // "default" | "acceptEdits" | "bypassPermissions"
            | "acceptEdits"
            | "bypassPermissions",
  allowDangerouslySkipPermissions: true, // 仅 bypassPermissions 时
  
  // === 行为控制 ===
  includePartialMessages: true,          // 包含部分消息
  additionalInstructions: "...",         // 补充指令
  effort: "high",                        // "low" | "medium" | "high" | "max" | "xhigh"
  settings: { alwaysThinkingEnabled: true, fastMode: false },
  
  // === 工具审批回调 ===
  canUseTool: (params) => { /* 审批逻辑 */ },
  
  // === 环境 ===
  env: { ANTHROPIC_AUTH_TOKEN: "...", ... },
  additionalDirectories: ["/path/to/workspace"],
  
  // === 高级 ===
  extraArgs: { /* 用户配置的额外 CLI 参数 */ },
  pathToClaudeCodeExecutable: "/path/to/claudecode",
  settingSources: ["user", "project", "local"],
}
```

### 6.2 SDKUserMessage 格式（sendTurn 发送）

```typescript
{
  type: "user",
  message: {
    role: "user",
    content: [
      { type: "text", text: "用户提示文本" },
      { type: "image", source: { type: "base64", media_type: "image/png", data: "base64..." } }
    ]
  }
}
```

### 6.3 通过 SDK 间接传递的 CLI 参数

SDK 内部调用 claudecode 时等效的命令行（示意）：

```bash
# 新建会话
claudecode \
  --session-id "550e8400-e29b-41d4-a716-446655440000" \
  --model "claude-sonnet-4-6" \
  --cwd "/path/to/workspace" \
  --permission-mode "bypassPermissions" \
  --include-partial-messages \
  --setting-source "user" \
  --setting-source "project" \
  --setting-source "local" \
  --effort "high"

# 恢复会话
claudecode \
  --resume "550e8400-e29b-41d4-a716-446655440000" \
  --model "claude-sonnet-4-6" \
  --cwd "/path/to/workspace" \
  --permission-mode "bypassPermissions" \
  --include-partial-messages \
  --setting-source "user" \
  --setting-source "project" \
  --setting-source "local" \
  --effort "high"
```

**注意**：实际项目中不直接构造命令行，而是通过 SDK 的 `query()` 函数传递参数，SDK 内部负责构建命令行并 spawn 进程。

---

## 七、消息历史的存储与查询

### 7.1 存储流程（事件溯源）

```
用户发送消息 / AI 生成回复
  ↓
编排引擎生成领域事件（domain event）
  ├─ thread.message-sent（用户消息或 AI 增量）
  ├─ thread.activity-appended（工具调用等）
  └─ thread.session-set（会话状态）
  ↓
事件存储 → orchestration_events（追加写入，不可变）
  ↓
投影器（Projector）读取事件 → 更新投影表
  ├─ projection_thread_messages   → 消息全文
  ├─ projection_thread_activities → 工具调用/活动
  ├─ projection_thread_sessions   → 会话状态
  └─ projection_turns             → turn 状态
```

### 7.2 读取流程（订阅线程）

```
客户端调用 subscribeThread(threadId)
  ↓
服务端执行 7 个并行查询：
  ├─ SELECT * FROM projection_threads WHERE thread_id = ?
  ├─ SELECT * FROM projection_thread_messages WHERE thread_id = ? ORDER BY created_at
  ├─ SELECT * FROM projection_thread_activities WHERE thread_id = ? ORDER BY created_at
  ├─ SELECT * FROM projection_proposed_plans WHERE thread_id = ? ORDER BY created_at
  ├─ SELECT * FROM projection_checkpoints WHERE thread_id = ? ORDER BY created_at
  ├─ SELECT * FROM projection_turns WHERE thread_id = ? ORDER BY created_at
  └─ SELECT * FROM projection_thread_sessions WHERE thread_id = ?
  ↓
组装为 OrchestrationThread 对象：
  { messages: [...], activities: [...], proposedPlans: [...],
    checkpoints: [...], session: {...} }
  ↓
返回初始快照给客户端 → UI 渲染
```

---

## 八、消息历史 vs AI 进程状态的关系

```
┌────────────────────────────────────────────────────────────┐
│                        服务端                                │
│                                                              │
│  ┌─────────────────┐    ┌──────────────────────────────┐   │
│  │    SQLite DB     │    │     claudecode 进程           │   │
│  │                  │    │                              │   │
│  │  orchestration_events  │  内部会话存储（不对外暴露）    │   │
│  │  projection_thread_    │  ~/.claude/sessions/        │   │
│  │  _messages (完整历史)   │  (AI 自己的对话记忆)         │   │
│  │  provider_session_     │                              │   │
│  │  _runtime (resumeCursor)│                              │   │
│  └─────────────────┘    └──────────────────────────────┘   │
│           ↑                         ↑                       │
│           │ 持久化所有事件            │ 仅用于恢复 AI 内部状态  │
│           │                         │ 通过 resume 参数连接   │
│  ┌────────┴─────────────────────────┴──────────────────┐  │
│  │                 WebSocket 推送                       │  │
│  │  subscribeThread → snapshot + events                │  │
│  └────────────────────┬────────────────────────────────┘  │
└───────────────────────┼────────────────────────────────────┘
                        │
                        ↓
┌───────────────────────────────────────────────────────────┐
│                     客户端（UI）                            │
│  从 snapshot 读取所有消息 → 直接渲染                        │
│  从 events 接收增量 → 追加/更新                             │
│  不依赖 AI 进程状态来显示历史消息                             │
└───────────────────────────────────────────────────────────┘
```

### 关键结论

| 项目 | 数据来源 | 是否依赖 AI 进程 |
|---|---|---|
| UI 显示的历史消息 | SQLite 投影表 | **否** — 即使 AI 进程不在也立即显示 |
| AI 的对话记忆 | AI 进程内部存储 | 是 — 通过 `resume` 参数恢复 |
| 会话恢复游标 | SQLite `provider_session_runtime` | 部分 — 读取时不依赖，恢复时使用 |
| 后续对话的连续性 | AI 进程内部存储 + 每次 turn 发送的消息 | 是 — 通过 `resume` + `sendTurn` |

---

## 九、其它语言（如 C# / WPF）的实现要点

### 9.1 数据库表

需要两张核心表：

```sql
-- 1. 消息历史表
CREATE TABLE conversation_messages (
    id TEXT PRIMARY KEY,
    thread_id TEXT NOT NULL,
    turn_id TEXT,
    role TEXT NOT NULL,          -- 'user' | 'assistant' | 'system'
    text TEXT NOT NULL,
    is_streaming INTEGER DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT
);

-- 2. 会话运行时表（用于恢复）
CREATE TABLE provider_session_runtime (
    thread_id TEXT PRIMARY KEY,
    provider_name TEXT NOT NULL,
    status TEXT NOT NULL,         -- 'running' | 'ready' | 'stopped'
    last_seen_at TEXT NOT NULL,
    resume_cursor_json TEXT,      -- JSON: { resume, turnCount }
    runtime_payload_json TEXT     -- JSON: { cwd, modelSelection }
);
```

### 9.2 会话 ID 管理

```csharp
// 1. 生成新 session ID
string GenerateNewSessionId() => Guid.NewGuid().ToString("D");

// 2. 从数据库读取 resume cursor
ResumeCursor GetResumeCursor(string threadId) {
    var row = db.QuerySingle("SELECT resume_cursor_json FROM provider_session_runtime WHERE thread_id = @id", threadId);
    return row == null ? null : JsonConvert.DeserializeObject<ResumeCursor>(row.resume_cursor_json);
}

// 3. 保存 resume cursor
void SaveResumeCursor(string threadId, ResumeCursor cursor) {
    db.Execute(@"
        INSERT INTO provider_session_runtime (thread_id, provider_name, status, last_seen_at, resume_cursor_json)
        VALUES (@threadId, @provider, @status, @now, @cursorJson)
        ON CONFLICT(thread_id) DO UPDATE SET
            status = @status,
            last_seen_at = @now,
            resume_cursor_json = @cursorJson",
        new { threadId, provider = "claude", status = "ready", now = DateTime.UtcNow.ToString("O"), cursorJson = JsonConvert.SerializeObject(cursor) });
}

// 4. 启动/恢复会话
// 伪代码 - 取决于你的 AI SDK
async Task StartOrResumeSession(string threadId, string cwd) {
    var resumeCursor = GetResumeCursor(threadId);
    var sessionId = resumeCursor?.resume ?? Guid.NewGuid().ToString("D");
    
    var options = new ClaudeQueryOptions {
        SessionId = sessionId,           // 新建时使用新 UUID
        Resume = resumeCursor?.resume,   // 恢复时使用旧 UUID
        // ... 其他选项
    };
    
    var query = ClaudeSdk.Query(options);
    // 保存 sessionId 供后续 updateResumeCursor 使用
}
```

### 9.3 服务器重启后的逻辑

```csharp
// 服务端启动时：标记所有 "running" 状态为 "stopped"
void ReconcileStaleSessions() {
    db.Execute(@"
        UPDATE provider_session_runtime
        SET status = 'stopped'
        WHERE status = 'running'"
    );
}
```

### 9.4 UI 加载历史消息

```csharp
// 打开已有线程时：直接从数据库读取
List<ChatMessage> LoadThreadMessages(string threadId) {
    return db.Query<ChatMessage>(@"
        SELECT id, role, text, is_streaming, created_at
        FROM conversation_messages
        WHERE thread_id = @threadId
        ORDER BY created_at ASC",
        new { threadId }
    ).ToList();
}
```

---

## 十、总结

| 步骤 | 做什么 | 关键数据 | 存储位置 |
|---|---|---|---|
| 新建会话 | 生成新 UUID → `query({ sessionId })` | `sessionId = Guid.NewGuid()` | 内存 + 后续持久化 |
| turn 完成 | 更新 resumeCursor | `{ resume, turnCount, ... }` | SQLite `provider_session_runtime` |
| 同线程继续 | 复用进程，不发新 session ID | 只调用 `sendTurn()` | — |
| 重新打开 | 从 DB 读消息 → UI 渲染 | `conversation_messages` 表 | SQLite |
| 发送新消息（需恢复） | 读 resumeCursor → `query({ resume })` | `{ resume: "旧 UUID" }` | SQLite → AI 进程 |
| 服务端重启 | 标记 stale session → 下次发送时恢复 | `resumeCursor` 仍在 DB | SQLite |
