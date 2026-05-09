# Claude Code GUI 多标签 + 侧边栏重构方案

> 创建日期：2026-05-08
> 目标：移除顶部工具栏、左侧侧边栏改为多标签、标题栏增加会话标签、支持项目与会话管理

---

## 一、架构总览

### 1.1 核心概念

| 概念 | 说明 |
|------|------|
| **项目 (Project)** | 对应一个工作目录，是会话的容器。从本软件中增删（非物理文件删除） |
| **会话 (Session)** | 一次与 Claude Code 的对话，属于某个项目。从本软件中增删 |
| **标签 (Tab)** | 已打开的会话在标题栏的视觉表示。每个标签对应一个独立会话 |

### 1.2 数据流

```
ProjectManager (projects.json 持久化)
  └─ ProjectItem
       └─ SessionItem[]
            └─ SessionState (输入、模式、Token、任务、聊天记录)
                 └─ UI 绑定 (标签栏 + 聊天区域 + 输入区)
```

### 1.3 新增/修改文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `SessionManager.cs` | **新增 ✅** | 管理所有会话生命周期、激活切换 |
| `ProjectManager.cs` | **新增 ✅** | 管理项目/会话树、持久化 |
| `ProjectSidebar.xaml` | **新增** | 项目标签页 UI（阶段 2） |
| `ProjectSidebar.xaml.cs` | **新增** | 项目树交互逻辑（阶段 2） |
| `SessionTabItem.cs` | **新增 ✅** | 标签页数据模型 |
| `ProjectItem.cs` | **新增 ✅** | 项目 + 会话模型类 |
| `MainWindow.xaml` | **修改** | 移除 Row1 工具栏、标题栏插入 TabBar、侧边栏改为双标签 |
| `MainWindow.xaml.cs` | **修改** | 从单会话 → 多会话架构、移除工具栏事件、集成 TabBar |
| `SessionState.cs` | **修改 ✅** | 添加 `SessionId`、`Name` 属性 |
| `App.xaml` | **不改** | 主题资源可复用 |

---

## 二、数据模型

### 2.1 SessionManager.cs

```csharp
/// <summary>多会话管理器</summary>
public class SessionManager
{
    public ObservableCollection<SessionTabItem> Tabs { get; }
    public SessionTabItem? ActiveTab { get; private set; }
    
    // 事件
    public event EventHandler<SessionTabItem>? TabActivated;
    public event EventHandler<SessionTabItem>? TabAdded;
    public event EventHandler<SessionTabItem>? TabClosed;
    
    public SessionTabItem CreateTab(string name, string workDir);
    public void ActivateTab(SessionTabItem tab);
    public void CloseTab(SessionTabItem tab);
}
```

### 2.2 SessionTabItem.cs

```csharp
/// <summary>标签页数据模型（一个标签 = 一个会话）</summary>
public class SessionTabItem : INotifyPropertyChanged
{
    public string Id { get; }           // GUID
    public string Name { get; set; }    // 显示名称
    public SessionState State { get; }  // 原有的会话状态
    
    // 聊天记录：存储为 UI 元素列表，切换标签时快速恢复
    public List<UIElement> ChatBubbles { get; } = new();
    
    // 用于标签栏显示的只读属性
    public string DisplayName => ...;
    public bool IsActive { get; set; }
}
```

### 2.3 ProjectManager.cs

保存到 `%APPDATA%/ClaudeCodeGUI/`，仅用于本软件内部管理，不触碰磁盘上的用户文件。

```csharp
public class ProjectItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public ObservableCollection<SessionItem> Sessions { get; set; } = new();
    public bool IsExpanded { get; set; } = true;
}

public class SessionItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    // 非持久化：关联的 Tab，当会话在标签中打开时设置
    public string? TabId { get; set; }
}

public class ProjectManager
{
    public ObservableCollection<ProjectItem> Projects { get; }
    
    public void Load();              // 从 JSON 加载
    public void Save();              // 持久化到 %APPDATA%/ClaudeCodeGUI/projects.json
    public ProjectItem AddProject(string name, string workDir);
    public void RemoveProject(ProjectItem project);  // 仅从列表移除，不删磁盘
    public SessionItem AddSession(ProjectItem project, string name);
    public void RemoveSession(ProjectItem project, SessionItem session);
    public void RenameSession(ProjectItem project, SessionItem session, string newName);
}
```

### 2.4 SessionState.cs 修改

添加两个属性：

```csharp
// 会话标识
public string SessionId { get; set; } = Guid.NewGuid().ToString();

// 会话名称（显示在标签上）
private string _name = "新会话";
public string Name
{
    get => _name;
    set { _name = value; OnPropertyChanged(); }
}
```

---

## 三、UI 布局方案

### 3.1 窗口布局总图

```
┌─────────────────────────────────────────────────────────────────┐
│ Row 0: TITLE BAR                                                │
│ [Logo+Title] [=== 会话标签栏 ===] [+添加标签] [状态] [主题] [控件] │
├─────────────────────────────────────────────────────────────────┤
│ Row 1: MAIN CONTENT                                  │ ← 移除了 │
│ ┌──────────┬──────────────────────────────┐          │   旧的   │
│ │ SIDEBAR  │  CHAT AREA                  │          │   Row 1  │
│ │ ┌──────┐ │  ┌────────────────────────┐ │          │  工具栏  │
│ │ │技能│项目│ │  │  聊天输出区域        │ │          │         │
│ │ ├──────┤ │  │                        │ │          │         │
│ │ │内容  │ │  │                        │ │          │         │
│ │ │区域  │ │  ├────────────────────────┤ │          │         │
│ │ │      │ │  │  进度条                │ │          │         │
│ │ │      │ │  ├────────────────────────┤ │          │         │
│ │ │      │ │  │  输入框 + 底部工具栏   │ │          │         │
│ │ └──────┘ │  └────────────────────────┘ │          │         │
│ └──────────┴──────────────────────────────┘          │         │
├───────────────────────────────────────────────────────┤         │
│ Row 2: STATUS BAR                                     │         │
└───────────────────────────────────────────────────────┘         │
```

### 3.2 标题栏（Tab Bar 区域）

**当前标题栏 Grid 列定义：**
```
Col 0 [Auto] — Logo + Title
Col 1 [*   ] — 空白（改为 Tab Bar）
Col 2 [Auto] — 状态指示
Col 3 [Auto] — 主题切换
Col 4 [Auto] — 窗口控制（最小化、最大化、关闭）
```

**Tab Bar 设计（Col 1）：**
```
┌──────────┬──────────┬──────────┬──────┐
│ 会话1  × │ 会话2  × │ 新建会  │ [+]
│ 话3  ×   │          │          │      │
└──────────┴──────────┴──────────┴──────┘
```

- 每个标签：圆角胶囊，显示会话名 + × 关闭按钮
- 激活标签：高亮背景色
- 末尾 `+` 按钮：在当前项目下新建会话并自动打开标签
- 标签过多时：按下鼠标可横向拖动/滚动（WrapPanel 或 ScrollViewer）

**XAML 示意：**
```xml
<TabControl x:Name="SessionTabBar" Grid.Column="1"
            Background="Transparent" BorderThickness="0"
            ItemsSource="{Binding Tabs}"
            SelectedItem="{Binding ActiveTab}">
    <TabControl.ItemTemplate>
        <DataTemplate>
            <Border CornerRadius="6" Padding="10,4" Margin="2,0">
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="{Binding Name}" .../>
                    <Button Content="×" ... Click="CloseTab_Click"/>
                </StackPanel>
            </Border>
        </DataTemplate>
    </TabControl.ItemTemplate>
</TabControl>
```

**TabControl 的样式**需要彻底重写以去掉默认的 TabControl 边框、选中高亮条等，使其看起来像 VSCode 的标签栏。

### 3.3 侧边栏改为双标签

**当前侧边栏 Grid 行定义：**
```
Row 0 [Auto] — Header（"技能" + 操作按钮）
Row 1 [Auto] — 搜索框
Row 2 [*   ] — 技能列表
Row 3 [Auto] — 底部预览
```

**改为双标签后：**
```
Row 0 [Auto] — 标签切换栏：[技能] [项目]  + 当前标签操作按钮
Row 1 [*   ] — 内容区域（根据选中标签切换）
```

**双标签切换栏：**
```xml
<Border Grid.Row="0" ...>
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        
        <!-- 切换按钮 -->
        <ToggleButton Grid.Col="0" Content="技能" .../>
        <ToggleButton Grid.Col="1" Content="项目" .../>
        
        <!-- 操作按钮（根据当前标签切换内容） -->
        <StackPanel Grid.Col="3" Orientation="Horizontal">
            <!-- 技能标签时：+ .md, + 文件夹, ✨, ⟳ -->
            <!-- 项目标签时：+ 项目 -->
        </StackPanel>
    </Grid>
</Border>
```

### 3.4 项目标签页内容

当选中"项目"标签时，Row 1 显示：

```
┌──────────────────────────┐
│ 📁 my-project       [+]
│ ├─ 💬 会话1            │
│ ├─ 💬 会话2            │
│ └─ 💬 会话3            │
│                          │
│ 📁 another-project      │
│ └─ 💬 会话1            │
└──────────────────────────┘
```

- 使用 `TreeView` 展示项目 → 会话层级
- 每个项目项有右键菜单：添加会话、重命名项目、删除项目
- 每个会话项有右键菜单：重命名、删除、在新标签中打开（选中）
- 顶部 `+` 按钮：弹出对话框选择目录创建新项目

**右键菜单设计：**

| 操作对象 | 菜单项 |
|----------|--------|
| 项目 | 添加会话 | 重命名项目 | 删除项目（从列表移除） |
| 会话 | 打开（新标签） | 重命名 | 删除 |

### 3.5 聊天区域的多会话支持

原 `ChatPanel`（单一 StackPanel）需要改造：

**方案：ContentControl + 数据模板**
```xml
<ContentControl Grid.Row="0" Content="{Binding ActiveChatPanel}">
    <!-- 每个标签切换时，ActiveChatPanel 指向不同的 StackPanel -->
</ContentControl>
```

每个 `SessionTabItem` 持有一个 `StackPanel _chatPanel`。当激活标签时：
1. 将当前显示面板从容器中移除
2. 将目标面板插入容器

**`SessionTabItem` 中存储的聊天 UI：**
```csharp
public class SessionTabItem
{
    // ⚡ 按需创建，懒加载
    public StackPanel ChatPanel { get; }
    public TextBox InputBox { get; }      // 每个会话独立输入框
    public TextBlock InputPlaceholder { get; }
    public TextBlock TokenPercentText { get; }
    public Path TokenProgressArc { get; }
    public ComboBox ModelComboBox { get; }
    public Button ModeToggleButton { get; }
    public Button PermissionBtn { get; }
}
```

激活标签时，交换 ChatGrid 中的子元素：
```csharp
private void ActivateTab(SessionTabItem tab)
{
    // 保存当前 tab 的 UI 状态
    SaveActiveTabUI();
    
    // 将新 tab 的 UI 元素放入 ChatContainer
    LoadTabUI(tab);
    
    // 更新 SessionState 绑定
    DataContext = tab.State;
    _session = tab.State;
}
```

---

## 四、迁移实施路线图

### 阶段 1：数据层（新文件）✅

| 任务 | 验证标准 | 状态 |
|------|----------|------|
| 创建 `ProjectItem`、`SessionItem` 模型类 | 编译通过，支持 INotifyPropertyChanged | ✅ |
| 创建 `ProjectManager` | 可 `Load()`/`Save()` JSON，支持 CRUD | ✅ |
| 创建 `SessionTabItem` | 包含 Name、Id、State、ChatBubbles、进程引用 | ✅ |
| 创建 `SessionManager` | 管理 Tabs 集合、激活/切换/关闭 | ✅ |
| 修改 `SessionState` | 添加 SessionId + Name 属性 | ✅ |

### 阶段 2：侧边栏改造 ✅

| 任务 | 验证标准 | 状态 |
|------|----------|------|
| 侧边栏 Header 改为 技能/项目 双标签按钮 | 点击切换，按钮样式不同 | ✅ |
| 用 Grid 切换技能/项目内容区域 | 切换时内容正确更换 | ✅ |
| 创建 `ProjectSidebar`（项目树） | 显示项目→会话 TreeView | ✅ |
| 项目树双击会话 → 激活 | 触发 SessionActivated 事件 | ✅ |
| 添加项目对话框 | 选择目录、命名项目、保存到 ProjectManager | ✅ |
| 标签切换时操作按钮联动 | 技能标签显示技能操作按钮，项目标签只显示 + 添加项目 | ✅ |

### 阶段 3：标签栏 + 多会话 ✅

| 任务 | 验证标准 | 状态 |
|------|----------|------|
| 标题栏插入 TabBar（ItemsControl + DataTemplate） | 显示会话标签，激活高亮 | ✅ |
| 点击项目树中的"会话" → 创建标签 | 新标签出现在标题栏，聊天区域清空 | ✅ |
| 标签切换 → 聊天内容完整切换 | 内容不丢失，输入框草稿恢复 | ✅ |
| 标签关闭 → 确认/直接关闭 | TabBar 移除，ChatPanel 清理 | ✅ |
| 移除 Row 1 工具栏（Visibility.Collapsed） | 程序无编译错误，功能不受影响 | ✅ |
| 创建 MainWindow.Tabs.cs（标签管理 partial class） | TabItem_Click, TabClose_Click, AddTab_Click, OnTabActivated, OnTabClosed | ✅ |
| 集成 SessionManager 到 MainWindow | 初始化首个标签并保存欢迎气泡 | ✅ |
| 停止按钮移至 ThinkingBar | ThinkingStopBtn 在运行时可见 | ✅ |

### 阶段 4：完善 + 回归测试 ✅

| 任务 | 验证标准 | 状态 |
|------|----------|------|
| 标签关闭后恢复上一个标签内容 | 不出现空白聊天区域 | ✅ |
| 所有现有功能无回归（技能、主题、图像） | 逐项手动测试 | ✅ |
| 项目/会话列表从 JSON 持久化 | 重启应用后数据恢复 | ✅ |
| 标签数量多时 TabBar 可滚动 | ScrollViewer + Auto，不溢出窗口 | ✅ |
| 标签右键菜单（停止、清空、关闭） | ContextMenu + PlacementTarget 传参 | ✅ |
| 标签双击重命名 | 弹出小窗口 Enter 确认 Esc 取消 | ✅ |
| 图片附件 per-tab 保存/恢复 | AttachedImagePath 在 SessionTabItem 中 | ✅ |
| SaveAsSkill + 清空按钮移至底部工具栏 | ✨ + 🗑 在 Token 和 Send 之间 | ✅ |
| 欢迎语更新 | 指向项目标签操作流程 | ✅ |
| 项目树右键菜单（添加会话、重命名、删除项目；打开、重命名、删除会话） | ProjectSidebar.xaml + .cs | ✅ |

---

## 五、关键技术决策

### 5.1 TabControl 样式重写

WPF 的 `TabControl` 默认有完整的边框、选中高亮条、内容区域边框。必须重写 `ControlTemplate` 使其成为**纯标签条**（无内容区域），内容由外部 `ContentControl` 管理。更简单的替代方案：不用 TabControl，直接用 `ListBox` + 自定义 ItemTemplate，或者用 `ItemsControl` + 普通按钮。

**推荐方案**：使用 `ItemsControl` + 水平 `StackPanel`，每个 item 是一个 Border(Button)，手动管理选中状态。比重写 TabControl 更可控、更轻量。

### 5.2 会话 UI 存储策略

**不推荐** 存储 WPF UI 元素到模型类（违反 MVVM）。但本应用是 code-behind 模式，实务方案：

每个 `SessionTabItem` 的 `ChatPanel`（StackPanel）在 C# 代码中动态创建，交换时仅操作 `Panel.Children` 的 Add/Remove。不涉及序列化。

### 5.3 进程管理变化

当前一个全局 `_job` 进程 → 改为每个 SessionTabItem 持有自己的进程引用：

```csharp
public class SessionTabItem
{
    public Process? Job { get; set; }
    public CancellationTokenSource? CTS { get; set; }
    public bool HasContinue { get; set; }
    public int Turns { get; set; }
}
```

激活/切换标签时**不中断**后台进程，仅暂停 UI 更新。收到输出时写入对应 Tab 的 ChatPanel。

### 5.4 项目持久化

保存路径：`%APPDATA%/ClaudeCodeGUI/`（非项目目录，避免侵入用户文件）

```json
[
  {
    "id": "uuid",
    "name": "My Project",
    "workingDirectory": "C:/projects/my-app",
    "sessions": [
      { "id": "uuid", "name": "初始化项目", "createdAt": "2026-05-08T..." },
      { "id": "uuid", "name": "添加登录功能", "createdAt": "2026-05-07T..." }
    ]
  }
]
```

### 5.5 移除工具栏后的目录选择入口

原"浏览"按钮在 Row 1。移除后，目录选择改为：
- 在"项目"标签中点击 `+` 添加项目时弹出的选择目录对话框
- 无活动会话时，主界面提示"请先在项目标签中添加项目"
- 每个项目的 WorkingDirectory 存放在 `ProjectItem` 中

### 5.6 原工具栏其他按钮的去向

| 原按钮 | 新位置 |
|--------|--------|
| 目录选择 | 创建/编辑项目时设置 |
| 浏览 | 添加项目对话框 |
| 停止 | Tab 标签右键菜单、"正在运行的标签"显示停止按钮在标签上 |
| 清空 | Tab 标签右键菜单、或聊天区域标题栏的小按钮 |
| 保存为技能 | 保留在聊天区域右上角（原位置） |
| 新任务 | 项目标签中的 + 会话（或 TabBar 的 + 按钮） |

---

## 六、文件修改详单

### 6.1 新建文件

| 文件 | 预估行数 | 实际行数 | 内容 |
|------|----------|----------|------|
| `ProjectItem.cs` | ~80 | ✅ | ProjectItem + SessionItem 模型类 |
| `ProjectManager.cs` | ~150 | ✅ | JSON 持久化 + CRUD |
| `SessionManager.cs` | ~120 | ✅ | 标签生命周期管理 |
| `SessionTabItem.cs` | ~120 | ✅ | 标签页模型 + 进程引用 |
| `ProjectSidebar.xaml` | ~100 | ✅ | 项目树 XAML + HierarchicalDataTemplate |
| `ProjectSidebar.xaml.cs` | ~120 | ✅ | 树交互逻辑 + SessionActivated 事件 |

### 6.2 修改文件

| 文件 | 修改范围 | 说明 |
|------|----------|------|
| `MainWindow.xaml` | 侧边栏改双标签 ✅ | 侧边栏 Header 改为 [技能] [项目] 切换 + 上下文操作按钮，内容区分为 SkillsPanel/ProjectsPanel |
| `MainWindow.xaml.cs` | ~200 行净增 ✅ | 添加 SidebarTabSkills/Projects_Click、UpdateSidebarTabUI、AddProject_Click、Sidebar_SessionActivated、_projectManager 字段 |
| `SessionState.cs` | +15 行 ✅ | 添加 Name、SessionId 属性 |

### 6.3 Golive 后暂不修改

- `App.xaml` — 主题资源完全复用
- `Converters.cs` — 仍用于任务状态
- `PlanSidebar.xaml/.cs` — 保持现有任务侧边栏功能

---

## 七、风险点与防范

| 风险 | 影响 | 防范措施 |
|------|------|----------|
| 多进程 Claude Code CLI 资源消耗大 | 同时打开多个会话时内存/CPU | 限制最大同时活跃进程数（如 3 个），非活跃 Tab 的进程保持但不做流式读取 |
| 初始化多个 Session UI 元素导致启动变慢 | 用户体验下降 | 懒加载：仅在首次激活 Tab 时创建 UI 元素 |
| 聊天内容切换丢失 | 对话历史丢失 | 切换 Tab 时深度保存 ChatPanel 的所有 Children，恢复时全部重新 Add |
| 标签关闭时进程未终止 | 残留进程 | `CloseTab()` 中强制 Kill 关联 Job |
| JSON 持久化并发写 | 数据损坏 | 每次 Save() 使用临时文件 + 原子替换 |

---

## 八、变更记录

| 日期 | 变更 | 作者 |
|------|------|------|
| 2026-05-08 | 初始版本 | Claude |
| 2026-05-08 | 阶段 1 完成：ProjectItem.cs、SessionTabItem.cs、ProjectManager.cs、SessionManager.cs，SessionState 增加 SessionId/Name | Claude |
| 2026-05-08 | 阶段 2 完成：侧边栏改为技能/项目双标签、ProjectSidebar 项目树、添加项目对话框 | Claude |
| 2026-05-08 | 阶段 3 完成：TabBar（ItemsControl）、MainWindow.Tabs.cs、工具栏折叠、聊天切换保存/恢复、停止按钮移至 ThinkingBar | Claude |
| 2026-05-08 | 阶段 4 完成：标签右键菜单、双击重命名、图片附件 per-tab、SaveAsSkill/Clear 移至底部工具栏、欢迎语更新、TabBar 可滚动 | Claude |
