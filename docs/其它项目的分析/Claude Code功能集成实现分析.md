# Claude Code 功能集成实现分析

> 分析 T3code 中与 Claude Code 集成的四个核心功能的实现方法。
> 涵盖：任务列表提取、实时 Token 用量跟踪、构建/计划模式传递、监督模式传递。

---

## 一、任务列表提取（TodoWrite → PlanSidebar）

### 整体思路

拦截 Claude Code 的 `TodoWrite` 工具调用，从流式事件中解析出结构化的任务步骤，通过 ProviderRuntimeEvent 事件系统传递到持久层，再通过 subscribeThread 流推送到前端渲染。

### 实现方法

1. **检测机制**：在 `input_json_delta` 流事件中，检测工具名是否包含 `"todowrite"`
2. **数据结构**：

```json
// Claude Code SDK TodoWrite 工具的输入格式
{
  "todos": [
    { "content": "步骤描述", "status": "pending|in_progress|completed", "activeForm": "进行时描述" }
  ]
}
```

3. **状态映射规则**：
   - `"in_progress"` → `"inProgress"`
   - `"completed"` → `"completed"`
   - 其他或缺失 → `"pending"`

4. **ProviderRuntimeEvent 格式**：

```json
{
  "type": "turn.plan.updated",
  "plan": [
    { "step": "步骤描述", "status": "pending|inProgress|completed" }
  ]
}
```

5. **持久化**：转为 `OrchestrationThreadActivity`，kind 为 `"turn.plan.updated"`，payload 包含 plan 数组
6. **前端消费**：反向遍历线程活动列表，找到最新的 `turn.plan.updated`，提取步骤数组渲染在 PlanSidebar 组件中

### 数据流

```
Claude Code SDK (TodoWrite 工具)
  → input_json_delta 流事件
    → 解析 todos → plan 步骤
      → emit "turn.plan.updated"
        → ProviderRuntimeIngestion → 活动存储
          → subscribeThread 流
            → deriveActivePlanState() → PlanSidebar UI
```

### 辅助机制

向 SDK 的 `additionalInstructions` 中注入中文指令（`T3_CODE_SIDEBAR_CHECKLIST_ZH_SUPPLEMENT`），要求 Claude Code 用简体中文编写步骤标题。

### 关键模块

| 模块 | 职责 |
|---|---|
| ClaudeAdapter.ts | 拦截 input_json_delta，提取 TodoWrite 数据 |
| extractPlanStepsFromTodoInput() | 将 todos 映射为标准 plan 步骤 |
| ProviderRuntimeIngestion.ts | 将 runtime event 转为持久化活动 |
| session-logic.ts (deriveActivePlanState) | 从活动列表中推导最新计划状态 |
| PlanSidebar.tsx | 前端计划展示组件 |

---

## 二、实时 Token 用量跟踪

### 整体思路

从 Claude Agent SDK 的 `ModelUsage` 数据中标准化 token 用量，在回合中和工作流进度事件中持续发出 `thread.token-usage.updated` 事件，持久化后推送到前端显示为进度条。

### 实现方法

1. **数据来源**：
   - SDK 的 `SDKResultMessage.modelUsage` — 提供每个模型的 context window
   - SDK 的 `task_progress` 消息内的 `usage` — 提供每个任务的精确用量快照
   - 回合完成时的 `result.usage` — 提供累积总量

2. **标准化后的数据结构**：

```json
{
  "usedTokens": 12345,
  "maxTokens": 200000,
  "inputTokens": 8000,
  "cachedInputTokens": 2000,
  "outputTokens": 3000,
  "reasoningOutputTokens": 500,
  "lastUsedTokens": 12345,
  "lastInputTokens": 8000,
  "lastOutputTokens": 3000,
  "totalProcessedTokens": 50000,
  "toolUses": 15,
  "durationMs": 45000,
  "compactsAutomatically": true
}
```

3. **标准化规则**：
   - `usedTokens` 被 cap 在 `maxTokens` 上限内（若已知 context window）
   - 原始字段（`input_tokens`、`output_tokens`、`cache_creation_input_tokens`、`cache_read_input_tokens`）汇总到规范字段
   - 回合完成时合并累积总量 + 最后一次任务快照，取最优值

4. **ProviderRuntimeEvent 格式**：

```json
{
  "type": "thread.token-usage.updated",
  "usage": { /* ThreadTokenUsageSnapshot */ }
}
```

5. **持久化**：转为 `OrchestrationThreadActivity`，kind 为 `"context-window.updated"`
6. **前端消费**：反向遍历找到最新的 `context-window.updated`，计算 `remainingTokens`、`usedPercentage`，渲染 ContextWindowMeter 进度条

### 数据流

```
Claude Code SDK (task_progress / turn result)
  → normalizeClaudeTokenUsage() 标准化
    → emit "thread.token-usage.updated"
      → ProviderRuntimeIngestion → "context-window.updated" 活动
        → subscribeThread 流
          → deriveLatestContextWindowSnapshot()
            → ContextWindowMeter UI (百分比进度条)
```

### 关键模块

| 模块 | 职责 |
|---|---|
| ClaudeAdapter.ts (normalizeClaudeTokenUsage) | 标准化 SDK 原始用量数据 |
| ClaudeSessionContext.lastKnownTokenUsage | 回合中持续更新的最近用量 |
| ProviderRuntimeIngestion.ts | 持久化 token 用量事件 |
| contextWindow.ts (deriveLatestContextWindowSnapshot) | 从活动推导最新快照 |
| ContextWindowMeter.tsx | 前端用量进度条组件 |

---

## 三、构建 / 计划模式传递

### 整体思路

通过 `interactionMode` 字段（`"default"` | `"plan"`）在线程级别持久化，每次发起回合时通过 `setPermissionMode()` API 传递给 Claude Agent SDK。

### 数据定义

```typescript
// packages/contracts/src/orchestration.ts
ProviderInteractionMode = "default" | "plan"
```

### 传递给 Claude Code 的方式

使用 Claude Agent SDK 的 `setPermissionMode()` API：

```typescript
// plan 模式
context.query.setPermissionMode("plan");

// default 模式 — 恢复基础权限
context.query.setPermissionMode(context.basePermissionMode);
// basePermissionMode 为 "acceptEdits" | "bypassPermissions" | "default"
```

- `"plan"` 是 Claude Agent SDK **内置**的权限模式
- `canUseTool` 回调中，plan 模式下对 `TodoWrite` 工具返回 `"deny"`，阻止代码执行，等待用户审查

### 传递入口（RPC）

```json
// ClientOrchestrationCommand: thread.turn.start
{
  "type": "thread.turn.start",
  "threadId": "...",
  "interactionMode": "plan|default",
  "runtimeMode": "approval-required|auto-accept-edits|full-access"
}
```

通过 WebSocket RPC `orchestration.dispatchCommand` 从客户端发送到服务端。

### 切换机制

```json
// ClientOrchestrationCommand: thread.interaction-mode.set
{
  "type": "thread.interaction-mode.set",
  "threadId": "...",
  "interactionMode": "plan|default"
}
```

### 持久化

- 数据库 migrations `012` 和 `013` 分别持久化 `interactionMode` 和 proposedPlans
- `OrchestrationThread` 模型包含 `interactionMode` 字段

---

## 四、监督模式传递

### 整体思路

通过 `runtimeMode` 字段（`"approval-required"` | `"auto-accept-edits"` | `"full-access"`）控制所有工具的权限策略，每种 Provider 适配器映射到各自 SDK 的权限模型。

### 数据定义

```typescript
// packages/contracts/src/orchestration.ts
RuntimeMode = "approval-required" | "auto-accept-edits" | "full-access"

// 底层权限枚举
ProviderApprovalPolicy = "untrusted" | "on-failure" | "on-request" | "never"
ProviderSandboxMode = "read-only" | "workspace-write" | "danger-full-access"
```

### 传递给 Claude Code 的方式

映射到 Claude Agent SDK 的 `PermissionMode`：

```typescript
const runtimeModeToPermission = {
  "auto-accept-edits": "acceptEdits",
  // SDK 内置：自动接受文件编辑，但 bash 等命令仍需确认
  "full-access": "bypassPermissions",
  // SDK 内置：跳过所有权限提示
};
// "approval-required" 不映射 → 使用 SDK 默认行为，询问每个操作
```

传递方式：

```typescript
// 创建 SDK query 时的选项
{
  permissionMode: "acceptEdits" | "bypassPermissions" | undefined,
  allowDangerouslySkipPermissions: true,  // 仅 full-access 时启用
}
```

`canUseTool` 回调兜底：

```typescript
// runtimeMode === "full-access" 时放行所有工具
if (runtimeMode === "full-access") return { behavior: "allow" };
```

### 切换命令

```json
{
  "type": "thread.runtime-mode.set",
  "threadId": "...",
  "runtimeMode": "approval-required|auto-accept-edits|full-access"
}
```

### 各 Provider 映射对照表

| Runtime Mode | Claude SDK | Codex approvalPolicy | Codex sandboxMode |
|---|---|---|---|
| `approval-required` | 默认（询问每个操作） | `"untrusted"` | `"read-only"` |
| `auto-accept-edits` | `"acceptEdits"` | `"on-request"` | `"workspace-write"` |
| `full-access` | `"bypassPermissions"` | `"never"` | `"danger-full-access"` |

### 持久化

- `OrchestrationSession` 和 `OrchestrationThread` 均包含 `runtimeMode` 字段
- 线程的 `runtimeMode` 可在会话中独立切换，不影响其他线程

---

## 通信架构总览

### RPC 方法（WebSocket）

| RPC 方法 | 方向 | 用途 |
|---|---|---|
| `orchestration.dispatchCommand` | 客户端 → 服务端 | 发送命令（起回合、切换模式等） |
| `orchestration.subscribeShell` | 服务端 → 客户端 | 壳级别更新（项目列表、线程列表） |
| `orchestration.subscribeThread` | 服务端 → 客户端 | 线程级别详情（活动、消息、状态） |

### 完整事件流

```
┌─────────────┐     dispatchCommand      ┌──────────────┐
│   Web 前端   │ ──────────────────────→ │  WebSocket   │
│  (React)    │ ←──────────────────────  │  服务端      │
└─────────────┘   subscribeShell /        └──────┬───────┘
                   subscribeThread                │
                                                  ↓
                                          ┌──────────────┐
                                          │ Orchestration │
                                          │   引擎        │
                                          └──────┬───────┘
                                                  │
                        ┌─────────────────────────┼──────────┐
                        │                         │          │
                        ↓                         ↓          │
                 ┌──────────────┐        ┌──────────────┐    │
                 │   Provider   │        │  SQLite 持久  │    │
                 │   适配器     │        │     化       │    │
                 │ (Claude/     │        └──────────────┘    │
                 │  Codex/...)  │                            │
                 └──────┬───────┘                            │
                        │                                    │
                        ↓                                    │
                 ┌──────────────┐                            │
                 │ Provider     │◄──── PubSub fan-out ───────┘
                 │ RuntimeEvent │
                 └──────────────┘
```

### 关键合约路径

| 合约文件 | 内容 |
|---|---|
| `packages/contracts/src/orchestration.ts` | RuntimeMode、InteractionMode、命令类型、RPC 方法签名 |
| `packages/contracts/src/providerRuntime.ts` | ThreadTokenUsageSnapshot、ProviderRuntimeEvent 类型 |
| `packages/contracts/src/rpc.ts` | RPC 方法定义 |
| `packages/contracts/src/ipc.ts` | IPC 通道定义 |

---

## 术语对照表

| 中文 | 英文（代码中） | 说明 |
|---|---|---|
| 计划模式 / 构建模式 | `interactionMode: "plan" / "default"` | 控制 Claude Code 是规划还是执行 |
| 监督模式 | `runtimeMode` | 控制工具权限级别 |
| 需批准 | `"approval-required"` | 每个操作都需要用户确认 |
| 自动接受编辑 | `"auto-accept-edits"` | 文件编辑自动批准，命令需确认 |
| 完全访问 | `"full-access"` | 跳过所有权限提示 |
| 任务计划 | `plan / ActivePlanState` | 从 TodoWrite 提取的步骤列表 |
| Token 用量 | `ThreadTokenUsageSnapshot` | 包含当前用量和累计用量 |
