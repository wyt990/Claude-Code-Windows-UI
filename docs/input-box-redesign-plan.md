# Claude Code GUI 输入框重构开发计划

> 创建日期：2026-05-07
> 目标：实现类似 T3code 的底部工具栏输入框，支持多标签扩展

---

## 一、需求概述

### 1.1 UI 布局

输入框底部工具栏从左到右：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [模型▼] │构│建│ [权限▼] │         │ [📋任务] │ [◐100%] │ [▶发送] │
└─────────────────────────────────────────────────────────────────────────────┘
```

| 元素 | 说明 |
|------|------|
| 模型选择 | 下拉列表，显示可用模型，带刷新按钮 |
| 构建/计划 | 切换按钮，互斥模式，默认"构建" |
| 权限下拉 | 向上弹出：监督模式/自动接受编辑/完全访问 |
| 任务按钮 | 仅当会话有任务时显示，点击展开右侧任务侧边栏 |
| Tokens 圆图 | 实时显示用量百分比，默认 200K 上限 |
| 发送按钮 | 发送后变红色停止按钮，响应完成恢复 |

### 1.2 多标签前瞻性

未来支持多标签同时打开多个会话，每个标签独立维护：
- 输入框草稿内容
- 选中的模型、模式、权限
- Tokens 消耗和最大值
- 任务列表
- 执行状态（发送/停止按钮状态）

---

## 二、技术方案

### 2.1 Session 状态对象

```csharp
public class SessionState
{
    // 输入状态
    public string DraftInput { get; set; } = "";
    public bool IsExecuting { get; set; } = false;
    
    // 模式与权限
    public InteractionMode Mode { get; set; } = InteractionMode.Build;
    public RuntimeMode PermissionLevel { get; set; } = RuntimeMode.ApprovalRequired;
    
    // 模型
    public string SelectedModel { get; set; } = "";
    public List<string> AvailableModels { get; private set; } = new();
    
    // Tokens
    public long UsedTokens { get; set; } = 0;
    public long MaxTokens { get; set; } = 200_000;
    
    // 任务
    public List<TodoItem> Tasks { get; set; } = new();
    public bool HasTasks => Tasks.Count > 0;
    
    // 工作目录
    public string WorkingDirectory { get; set; } = "";
}

public enum InteractionMode { Build, Plan }
public enum RuntimeMode { ApprovalRequired, AutoAcceptEdits, FullAccess }

public class TodoItem
{
    public string Content { get; set; } = "";
    public TodoStatus Status { get; set; } = TodoStatus.Pending;
    public string? ActiveForm { get; set; }
}

public enum TodoStatus { Pending, InProgress, Completed }
```

### 2.2 核心功能实现

#### 2.2.1 任务列表提取（TodoWrite → PlanSidebar）

**实现方法**：
1. 监听 `Process.StandardOutput` 流事件
2. 检测工具调用是否包含 `"todowrite"`（不区分大小写）
3. 从 JSON 中提取 `todos` 数组
4. 状态映射：
   - `"in_progress"` → `InProgress`
   - `"completed"` → `Completed`
   - 其他 → `Pending`

**数据流**：
```
Claude Code CLI (TodoWrite 工具)
  → StandardOutput 流
    → 正则匹配 + JSON 解析
      → SessionState.Tasks 更新
        → UI 绑定刷新
          → PlanSidebar 渲染
```

#### 2.2.2 实时 Token 用量跟踪

**数据来源**：
- 流式输出中解析 `token_usage` 字段
- 或从 `--stats` 输出中提取

**标准化数据结构**：
```json
{
  "usedTokens": 12345,
  "maxTokens": 200000,
  "inputTokens": 8000,
  "outputTokens": 3000,
  "percentage": 6.2
}
```

#### 2.2.3 模型加载

**命令**：`claudecode --list-models --json`

**行为**：
- 程序启动时自动加载一次
- 列表上方添加刷新按钮，可手动刷新
- 结果缓存到 `SessionState.AvailableModels`

#### 2.2.4 模式传递

**Build/Plan 模式**：
- 通过命令行参数 `--interaction-mode=plan` 或 `--interaction-mode=default`
- 或运行时通过环境变量/标准输入切换

**权限模式**：
- 映射到 Claude Code 的权限设置
- `ApprovalRequired` → 默认行为
- `AutoAcceptEdits` → `--accept-edits`
- `FullAccess` → `--dangerously-skip-permissions`

### 2.3 UI 组件实现

#### 2.3.1 底部工具栏布局

```xml
<Grid Grid.Row="2">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>  <!-- 输入框 -->
        <RowDefinition Height="Auto"/>  <!-- 工具栏 -->
    </Grid.RowDefinitions>
    
    <!-- 输入框 -->
    <TextBox x:Name="InputBox" Grid.Row="0" ... />
    
    <!-- 工具栏 -->
    <Border Grid.Row="1" Background="{DynamicResource ThemeSurface3}" ...>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/> <!-- 模型选择 -->
                <ColumnDefinition Width="Auto"/> <!-- Build/Plan 切换 -->
                <ColumnDefinition Width="Auto"/> <!-- 权限下拉 -->
                <ColumnDefinition Width="*" />   <!-- 占位 -->
                <ColumnDefinition Width="Auto"/> <!-- 任务按钮 -->
                <ColumnDefinition Width="Auto"/> <!-- Tokens 圆图 -->
                <ColumnDefinition Width="Auto"/> <!-- 发送按钮 -->
            </Grid.ColumnDefinitions>
            
            <!-- 控件绑定到 SessionState -->
        </Grid>
    </Border>
</Grid>
```

#### 2.3.2 Tokens 圆图控件

自定义控件，使用 `Path` 绘制圆弧：
- 外圈：背景色（灰色）
- 内圈：进度色（蓝色渐变）
- 中心显示百分比数字

#### 2.3.3 任务侧边栏

- 复用现有 `SkillsPanel` 的 `GridSplitter` 机制
- 分隔符中间添加折叠按钮
- 可手动调整宽度
- 使用 `Visibility` 绑定 `SessionState.HasTasks`

---

## 三、实施路线图

### 阶段 1：基础架构（Session 状态与数据绑定）

**目标**：建立状态管理基础，支持未来多标签扩展

| 任务 | 验证标准 |
|------|----------|
| 定义 `SessionState` 类 | 类编译通过，包含所有必需属性 |
| 创建 `ObservableCollection<SessionState>` | 可在代码中创建并管理多个会话 |
| 实现 `INotifyPropertyChanged` | 属性变更时 UI 自动更新 |
| 迁移现有字段到 SessionState | 程序功能正常，无回归 |

### 阶段 2：UI 实现（底部工具栏）

**目标**：完成输入框布局重构和控件基础功能

| 任务 | 验证标准 |
|------|----------|
| 重构输入区为两行布局 | 界面显示正常，输入框和工具栏分离 |
| 实现模型选择下拉框 | 显示模型列表，可切换选中 |
| 实现 Build/Plan 切换按钮 | 点击切换，UI 状态正确 |
| 实现权限下拉菜单（向上弹出） | 点击展开，选择后更新状态 |
| 实现 Tokens 圆图控件 | 显示圆形进度，支持数据绑定 |
| 实现发送/停止按钮切换 | 发送后变红，完成后恢复 |
| 实现任务按钮（条件显示） | 有任务时显示，无任务时隐藏 |

### 阶段 3：核心功能（与 Claude Code 集成）

**目标**：实现与 Claude Code CLI 的实时交互

| 任务 | 验证标准 |
|------|----------|
| 实现 `--list-models --json` 调用 | 启动时加载模型列表，显示在 UI |
| 实现模型刷新按钮 | 点击刷新，列表更新 |
| 实现流式任务列表解析 | 执行时自动提取 TodoWrite，显示任务 |
| 实现实时 Token 追踪 | Tokens 圆图随输出实时更新 |
| 实现模式参数传递 | Build/Plan 模式正确传递给 CLI |
| 实现权限参数传递 | 权限设置正确映射到 CLI 参数 |

### 阶段 4：完善体验（任务侧边栏与细节）

**目标**：完成任务侧边栏和交互细节优化

| 任务 | 验证标准 |
|------|----------|
| 实现任务侧边栏布局 | 右侧显示，可调整宽度 |
| 实现分隔符折叠按钮 | 点击折叠/展开侧边栏 |
| 实现任务状态更新 | 任务完成时 UI 同步更新 |
| 添加输入框草稿保存 | 切换会话时草稿不丢失 |
| 优化 Tokens 显示精度 | 显示 KB/MB 单位，更友好 |
| 添加工具提示和快捷键 | 提升可用性 |

---

## 四、关键技术点

### 4.1 流式 JSON 解析

需要处理不完整 JSON 和转义字符：

```csharp
// 使用 JsonDocument.ParseValue 处理片段
// 或使用正则提取完整 JSON 对象后再解析
```

### 4.2 多线程 UI 更新

所有 UI 更新必须在 UI 线程执行：

```csharp
Dispatcher.Invoke(() => {
    // 更新 UI
});
```

### 4.3 命令行参数构建

根据模式动态构建参数：

```csharp
var args = new List<string>();
if (session.Mode == InteractionMode.Plan)
    args.Add("--interaction-mode=plan");
if (session.PermissionLevel == RuntimeMode.FullAccess)
    args.Add("--dangerously-skip-permissions");
// ...
```

---

## 五、参考实现

### 5.1 T3code 项目

T3code 已实现相同功能，关键参考：
- `ClaudeAdapter.ts` - 流事件拦截和解析
- `extractPlanStepsFromTodoInput()` - 任务提取
- `normalizeClaudeTokenUsage()` - Token 标准化
- `ProviderRuntimeEvent` 事件系统 - 状态传递

### 5.2 本项目现有代码

- `MainWindow.xaml.cs` - 进程管理和流处理
- `MainWindow.xaml` - UI 布局
- `ThemeManager.cs` - 主题系统

---

## 六、注意事项

1. **不要假设，先问清楚** - 不确定的需求及时确认
2. **能简则简** - 不为一次性逻辑搭抽象层
3. **精准修改** - 只动该动的地方，保持原有风格
4. **目标驱动** - 每个阶段定义明确的验证标准
5. **前瞻性设计** - 状态封装好，未来多标签只需改容器层

---

## 七、变更记录

| 日期 | 变更 | 作者 |
|------|------|------|
| 2026-05-07 | 初始版本 | Claude |
