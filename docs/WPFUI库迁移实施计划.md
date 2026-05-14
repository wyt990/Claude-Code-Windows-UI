# WPFUI 库迁移实施计划

> 目标：将项目从 WPF 原生样式迁移到 [WPFUI](https://github.com/lepoco/wpfui) 库（Fluent Design 风格），在保持所有功能不变的前提下，显著提升界面现代感和精致度。
>
> ⚠️ **迁移结果：失败** — WPFUI v4.3.0 的 `FluentWindow` 在 Windows 11 + .NET 9 环境下存在严重兼容性问题，导致窗口完全空白。所有迁移更改已回滚，项目恢复使用原生 WPF Window。

---

## ⚠️ 迁移失败记录（2026-05-13）

### 问题现象

将 `MainWindow` 从原生 `Window` 改为 `ui:FluentWindow` 后，应用程序启动时窗口**完全空白**：
- 窗口框架可见（标题栏、边框）
- 窗口内容区域为纯白色/透明，无任何 UI 元素渲染
- 无异常抛出，无错误日志
- 编译成功（0 errors）

### 排查过程

| 尝试 | 操作 | 结果 |
|------|------|------|
| 1 | 添加 `ui:ThemesDictionary` 到 App.xaml | 仍空白 |
| 2 | 使用 `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"` | 仍空白 |
| 3 | 调用 `ApplicationThemeManager.Apply(Dark, Mica, true)` | 仍空白 |
| 4 | 移除 `ThemesDictionary`，仅用 `Apply()` | 仍空白 |
| 5 | 移除 `Apply()`，仅用 `ThemesDictionary` | 仍空白 |
| 6 | 硬编码 `Background="#0D1117"` | 仍空白（背景未生效） |
| 7 | 移除所有 `ui:SymbolIcon`，改用 TextBlock | 抛出异常：FontSize 为空字符串 |
| 8 | 移除所有 WPFUI 控件，仅保留 FluentWindow | 仍空白 |
| 9 | **回滚到原生 Window** | ✅ 窗口正常渲染 |

### 根本原因

**WPFUI v4.3.0 的 `FluentWindow` 控件在 Windows 11 + .NET 9 环境下根本性损坏：**

1. **内部模板加载失败** — FluentWindow 的 ControlTemplate 无法正确加载，导致内容区域为空白
2. **资源解析异常** — `SymbolIcon` 构造函数抛出 `""不是属性"FontSize"的有效值` 异常，表明内部资源字典存在空值问题
3. **与 .NET 9 不兼容** — 可能是 WPFUI v4.3.0 针对 .NET 9 的适配不完整

### 解决方案

**唯一可行方案：完全放弃 WPFUI，使用原生 WPF Window + 自定义样式。**

迁移尝试已全部回滚，项目恢复到原生 WPF 状态。

### 替代方案建议

如需 Fluent Design 风格，可考虑以下替代方案：

| 方案 | 说明 | 推荐度 |
|------|------|--------|
| **保持现状** | 原生 WPF + 自定义主题系统（当前状态） | ⭐⭐⭐⭐ 功能稳定 |
| **ModernWPF** | 另一个 Fluent 风格库，API 更简单 | ⭐⭐⭐ 可尝试 |
| **FluentWPF** | 专注于 Fluent Design 的轻量库 | ⭐⭐⭐ 可尝试 |
| **MaterialDesignInXAML** | Material Design 风格，成熟稳定 | ⭐⭐⭐⭐ 推荐尝试 |
| **WPFUI v3.x** | 旧版本，API 不同但可能更稳定 | ⭐⭐ 不推荐（API 差异大） |
| **手动实现** | 自定义 WindowChrome + 样式 | ⭐⭐ 工作量大 |

---

## 附：WPFUI v4.3 实际 API 参考（仅供参考，实际不可用）

基于已安装的 `WPF-UI 4.3.0` NuGet 包的实际情况：

| 组件 | 命名空间 | 说明 |
|------|---------|------|
| 窗口基类 | `Wpf.Ui.Controls.FluentWindow` | 替换 WPF 原生 `Window`，自带标题栏、圆角、阴影、Mica 背景 |
| 主题管理 | `Wpf.Ui.Appearance.ApplicationThemeManager` | `Apply(ApplicationTheme, WindowBackdropType, bool)` 应用主题 |
| 主题枚举 | `Wpf.Ui.Appearance.ApplicationTheme` | `Dark` / `Light` / `HighContrast` |
| 背景效果 | `Wpf.Ui.Controls.WindowBackdropType` | `Mica` / `Acrylic` / `Tabbed` / `None` |
| 按钮 | `Wpf.Ui.Controls.Button` | 支持 `Appearance` 属性（Primary / Secondary / Danger / Success / Warning / Info）和 `Icon` 属性 |
| 输入框 | `Wpf.Ui.Controls.TextBox` | 支持 `PlaceholderText`、`ClearButtonEnabled`、`Icon` |
| 图标 | `Wpf.Ui.Controls.SymbolIcon` | `Symbol` 属性接收 `SymbolRegular` / `SymbolFilled` 枚举值 |
| 标题栏 | `Wpf.Ui.Controls.TitleBar` | `LeftContent` / `CenterContent` / `RightContent` |
| 导航 | `Wpf.Ui.Controls.NavigationView` | 侧边导航容器 |

**关键差异（与 v3 相比）：**
- ❌ v4 **没有** `Fluent.xaml` 资源字典文件，主题通过代码 `ApplicationThemeManager.Apply()` 驱动
- ❌ 不存在 `Wpf.Ui.ThemeManager` 类（v3 的旧 API 已被 `ApplicationThemeManager` 取代）
- ✅ 控件默认样式由 WPF 的 `Generic.xaml` 机制自动加载，无需在 `App.xaml` 中手动合并

## 一、现状分析（为什么 UI 显得低端）

### 1.1 核心问题

| 严重度 | 问题 | 具体表现 |
|--------|------|---------|
| 🔴 致命 | **全局等宽字体** | `MainWindow.xaml:10` 设置 `FontFamily="Consolas, Courier New"` 应用于整个窗口，所有 UI 文字（状态栏、标签、按钮、标题）都用等宽字体，产生"终端感" |
| 🔴 致命 | **Unicode 充当地址图标** | 随处可见 `&#x25B6;` `&#x2728;` `&#x1F5D1;` 等 Unicode 字符代替矢量图标，渲染不一致，不能改色/缩放 |
| 🔴 致命 | **颜色层次不足** | 默认深色主题 Surface 色值仅差 2-5%（如 #0D1117 → #161B22），各区域"糊"在一起 |
| 🟡 严重 | **气泡设计简陋** | 仅有 `CornerRadius=10` + 1px 边框，无阴影、无头像、无视觉深度 |
| 🟡 严重 | **自定义窗口简陋** | `WindowStyle="None"` + 自绘标题栏，Unicode 字符做窗口按钮(- □ ✕)，无阴影 |
| 🟡 中等 | **缺少过渡动画** | 面板切换是瞬切的(`Visibility.Collapsed`)，没有平滑过渡 |
| 🟡 中等 | **按钮样式单一** | 所有按钮只有 opacity 变化做悬停反馈，无 Ripple 等现代交互反馈 |
| 🟡 中等 | **代码语法高亮弱** | 仅支持 6 种语言，Regex tokenizer 不如专业编辑器 |
| ⚪ 轻微 | **输入框占位文字太长** | 一整句说明占据了宝贵的 UI 空间 |

### 1.2 现有主题系统（将废弃）

- **Theme.cs** — 6 个主题工厂方法，52 个颜色属性
- **ThemeManager.cs** — 管理主题切换，通过 `Application.Resources` 动态注入 50+ 个 `SolidColorBrush`
- **AppStyles.xaml** — 自定义 `InputStyle`、`FlatButton`、`SidebarScrollBar` 等 ControlTemplate

这套系统工作量不小（手写了数百行颜色和模板），但效果仍然不如 WPFUI 库提供的 Fluent Design 主题。

---

## 二、影响文件清单

### 2.1 需要修改的文件

| 文件 | 改动范围 | 类型 |
|------|---------|------|
| `ClaudeCodeGUI.csproj` | +1 行：添加 WPFUI NuGet 引用 | 新增依赖 |
| `App.xaml` | 移除 WindowChrome 资源、修剪 DynamicResource 默认值 | 修改 |
| `App.xaml.cs` | 替换 ThemeManager 初始化 | 修改 |
| `MainWindow.xaml` | 窗口基类改为 WPFUI，替换所有控件为 WPFUI 风格，图标替换 | **大幅修改** |
| `MainWindow.xaml.cs` | `: Window` → `: FluentWindow`，简化窗口控制方法 | 修改 |
| `MainWindow.Chat.cs` | 移除字体硬编码 | 微调 |
| `MainWindow.Commands.cs` | 无变化 | — |
| `MainWindow.Session.cs` | 无变化 | — |
| `MainWindow.Skills.cs` | 无变化 | — |
| `MainWindow.Tabs.cs` | 无变化 | — |
| `MainWindow.Utilities.cs` | 可保留 `Candidates()`、`FindClaude()`、`Strip()`；`H()` 可能仍在其他文件使用 | 微调 |
| `ProjectSidebar.xaml` | TreeView 改用 WPFUI 样式 | 修改 |
| `FilesSidebar.xaml` | 控件替换 | 修改 |
| `SearchPanel.xaml` | 控件替换 | 修改 |
| `ProblemsPanel.xaml` | 控件替换 | 修改 |
| `SettingsWindow.xaml` | 改用 WPFUI Window + 控件 | 修改 |
| `SettingsWindow.xaml.cs` | 窗口基类修改 | 微调 |
| `CodeViewer.xaml` | 控件样式替换 | 修改 |
| `ApprovalDialog.xaml` | 改用 WPFUI Window | 修改 |
| `ApprovalDialog.xaml.cs` | 窗口基类修改 | 微调 |
| `BubbleActions.cs` | 图标替换（Unicode → SymbolIcon） | 微调 |
| `CollapsibleGroup.cs` | 颜色引用调整 | 微调 |
| `CodeBlock.cs` | `ThemeManager.GetColor()` → WPFUI 颜色 API | 微调 |
| `DiffViewer.cs` | 同上 | 微调 |
| `CommandPaletteWindow.xaml` | 控件样式替换 | 修改 |
| `CommandPaletteWindow.xaml.cs` | 窗口基类修改 | 微调 |
| `StreamParseLogWindow.xaml` | 控件样式替换 | 修改 |
| `StreamParseLogWindow.xaml.cs` | 窗口基类修改 | 微调 |
| `AnimationHelper.cs` | 保留（不依赖主题） | — |

### 2.2 可以删除/冻结的文件

| 文件 | 处理方式 | 在哪一步 |
|------|---------|---------|
| `Themes/Theme.cs` | 删除（WPFUI 自带主题系统） | 最终步 |
| `Themes/ThemeManager.cs` | 删除（由 WPFUI ApplicationThemeManager 替代） | 最终步 |
| `Themes/AppStyles.xaml` | 删除 95% 内容（仅保留可能需要的自定义样式） | 逐步替换后删除 |

### 2.3 不受影响的纯逻辑文件

以下文件与 UI 无关，完全不需要修改：

`ClaudeEventParser.cs` `ConversationStore.cs` `Enums.cs` `EventBus.cs` `GitService.cs`
`ModelListResponse.cs` `ProblemService.cs` `ProjectManager.cs` `SearchService.cs`
`SessionManager.cs` `SessionState.cs` `SessionTabItem.cs` `SkillItem.cs` `TimelineModel.cs`
`TodoItem.cs` `TokenUsageTracker.cs` `ProjectItem.cs` `FileTreeItem.cs` `CommandItem.cs`
`FileSummary.cs` `ToolCallItem.cs` `StreamingRenderer.cs` `Converters.cs`

---

## 三、分步实施计划

每个步骤完成后 **必须能正常编译和运行**，便于逐步验收效果。

---

### 第 1 步：安装 WPFUI 包 + 基础配置

**目标**：引入依赖，确保编译通过。运行时外观不应有任何变化。

#### 1.1 安装 NuGet 包

```bash
cd d:\ai\ClaudeCodeWindows\ClaudeCodeUI
dotnet add package WPF-UI
```

验证：`ClaudeCodeGUI.csproj` 中新增了 `<PackageReference Include="WPF-UI" .../>`。

#### 1.2 验证编译

WPFUI v4 是纯 DLL 包（无 XAML 资源字典），控件样式通过 WPF `Generic.xaml` 机制自动加载，**无需修改 `App.xaml`**。

```
dotnet build
```

必须 0 错误通过。运行后界面完全不变——WPFUI 程序集已加载但尚未被使用。

---

### 第 2 步：替换 MainWindow 窗口基类

**目标**：将 MainWindow 从 WPF 原生 `Window` 改为 WPFUI 的 `FluentWindow`，获得现代窗口框架（内置标题栏、窗口阴影、Fluent 动画）。

> ✅ **第 2 步已于 2026-05-13 完成。** 实际改动：
> - MainWindow.xaml：根元素改为 `ui:FluentWindow`，移除了 `WindowStyle`/`AllowsTransparency`/`ResizeMode`/`WindowChrome`
> - MainWindow.xaml.cs：基类改为 `Wpf.Ui.Controls.FluentWindow`（全限定名以避免命名冲突）
> - 移除了自绘窗口按钮（最小化/最大化/关闭）— FluentWindow 自带
> - 移除了 `TitleBar_DragMove` 事件处理
> - 移除了 `_maximized`/`_normalLeft`/`_normalTop`/`_normalWidth`/`_normalHeight` 字段
> - 移除了 `WinMinimize_Click`/`WinMaximize_Click`/`WinClose_Click` 事件处理
> - 移除了 App.xaml 中的 `CustomWindowChrome` 资源
> - 6 个分部类文件移除了重复的 `: Window` 声明
> - **避免命名空间冲突**：不使用 `using Wpf.Ui.Controls;`，改用全限定名引用 `FluentWindow`

#### 2.1 修改 MainWindow.xaml

关键改动：
- 根元素 `Window` → `ui:FluentWindow`
- 引入 `xmlns:ui="clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui"`
- 删除 `WindowStyle="None"` `AllowsTransparency="False"` `ResizeMode="CanResizeWithGrip"` `WindowChrome` — FluentWindow 自带
- 保留 `Height` `Width` `MinHeight` `MinWidth` `WindowStartupLocation`
- 保留 `Title` `Icon`

```diff
- <Window x:Class="ClaudeCodeGUI.MainWindow"
-         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
-         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
-         xmlns:local="clr-namespace:ClaudeCodeGUI"
-         Title="Claude Code 代码助手"
-         Icon="img/barcode_6299441_256x256.ico"
-         Height="700" Width="1100"
-         MinHeight="480" MinWidth="800"
-         Background="{DynamicResource ThemeBackground}"
-         FontFamily="Consolas, Courier New"
-         WindowStartupLocation="CenterScreen"
-         WindowStyle="None"
-         AllowsTransparency="False"
-         ResizeMode="CanResizeWithGrip"
-         WindowChrome.WindowChrome="{StaticResource CustomWindowChrome}">

+ <ui:FluentWindow x:Class="ClaudeCodeGUI.MainWindow"
+         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
+         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
+         xmlns:ui="clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui"
+         xmlns:local="clr-namespace:ClaudeCodeGUI"
+         Title="Claude Code 代码助手"
+         Icon="img/barcode_6299441_256x256.ico"
+         Height="700" Width="1100"
+         MinHeight="480" MinWidth="800"
+         Background="{DynamicResource ThemeBackground}"
+         FontFamily="Segoe UI, Microsoft YaHei"
+         WindowStartupLocation="CenterScreen">
```

#### 2.2 修改 MainWindow.xaml.cs

```diff
- public partial class MainWindow : Window
+ public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
```

#### 2.3 删除自绘标题栏

MainWindow.xaml 中 `Grid.Row="0"` 的标题栏 Border 整段（约 200 行）需要**重构为 WPFUI 标题栏内容**。

具体做法：
- 删除自定义窗口控制按钮（最小化/最大化/关闭）— FluentWindow 自带了
- 将标题内容（Logo + "Claude Code" + SubtitleLabel + 标签栏 + 状态指示器 + 主题切换按钮）放入 WPFUI 标题栏的内容区域

WPFUI 的 FluentWindow 通过 `TitleBar` 属性或 `Template` 自定义标题栏。更简单的方式是使用 `FluentWindow` 默认标题栏 + `ExtendContentArea` 或将控件放在窗口的普通 Grid 中。

**推荐做法**：使用 WPFUI 的默认标题栏（自带窗口按钮、拖拽、圆角），然后把我们的选项卡栏放在标题栏下方 Grid.Row="1" 位置。

简化后的窗口布局：

```xml
<ui:FluentWindow ...>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- 原标题栏 → 简化：只放标签栏 -->
            <RowDefinition Height="*"/>      <!-- 主体 -->
            <RowDefinition Height="Auto"/>   <!-- 状态栏 -->
            <RowDefinition Height="Auto"/>   <!-- 终端 -->
        </Grid.RowDefinitions>

        <!-- 简化标题栏：只保留标签页 + 状态指示 + 主题切换（窗口按钮由 FluentWindow 提供） -->
        <Border Grid.Row="0" ...>
            <!-- 左侧：Logo + 标题文字 -->
            <!-- 中间：SessionTabBar -->
            <!-- 右侧：状态指示 + 主题切换按钮 -->
            <!-- 注意：不再需要最小化/最大化/关闭按钮 -->
        </Border>

        <!-- 其余行保持不变 ... -->
    </Grid>
</ui:FluentWindow>
```

#### 2.4 移除 WindowChrome 相关代码

在 `App.xaml` 中删除 `CustomWindowChrome` 资源。删除以下代码块：

```xml
<!-- WindowChrome 配置：消除顶部空白边距 -->
<WindowChrome x:Key="CustomWindowChrome" .../>
```

#### 2.5 更新窗口控制事件处理

MainWindow.xaml.cs 中 `WinMinimize_Click` `WinMaximize_Click` `WinClose_Click` 方法可以简化或删除（FluentWindow 自带窗口按钮）。

如果保留自定义按钮，则调用 WPFUI 的窗口控制 API 或保留原生方式：

```csharp
// 可保留自定义最小化/最大化/关闭按钮（如果需要）
// WPFUI 提供 WindowState 属性操作
```

#### 2.6 编译验证

```
dotnet build
```

修复可能的问题：
- 命名空间冲突（如 `Path` 等）
- 资源引用错误
- 事件处理签名变化

运行时检查：
- 窗口正常显示，有现代窗口边框和阴影
- 标题栏功能正常（可拖拽、缩放、关闭）
- 所有原有功能可用

---

### 第 3 步：替换全局字体 + 引入图标

**目标**：将全局面字体从 Consolas 改为 Segoe UI，开始用 WPFUI 的 SymbolIcon 替换 Unicode 图标。这一步不改变布局，只改字体和首批图标。

> ✅ **第 3 步已于 2026-05-13 完成。** 实际改动：
> - MainWindow.xaml：`FontFamily` 从 `"Consolas, Courier New"` 改为 `"Segoe UI, Microsoft YaHei"`
> - 替换了 22 处 Unicode 图标为 WPFUI `SymbolIcon`，覆盖标题栏、侧边栏、输入区域和底部工具栏
> - ContextMenu 中的图标通过 `MenuItem.Icon` 属性使用 `SymbolIcon`
> - 混合内容按钮（图标+文字）改用 `StackPanel` 包裹 `SymbolIcon` + `TextBlock`
> - `dotnet build` 0 错误通过

#### 3.1 全局字体

MainWindow.xaml 中：

```diff
- FontFamily="Consolas, Courier New"
+ FontFamily="Segoe UI, Microsoft YaHei"
```

仅保留**代码块**和**输入框**使用等宽字体。

#### 3.2 图标替换对照表

MainWindow.xaml 中 22 处 Unicode 图标全部替换为 `SymbolIcon`：

| 位置 | 旧内容 | 新 SymbolIcon |
|------|--------|--------------|
| Tab 上下文菜单「停止」 | `⏹` | `Stop20`（MenuItem.Icon）|
| Tab 上下文菜单「清空对话」 | `🗑` | `Delete20`（MenuItem.Icon）|
| Tab 关闭按钮 | `×` | `Dismiss20` |
| 新建标签按钮 | `+` | `Add20` |
| 主题切换 | `&#x263E;` (☾) | `DarkTheme24` |
| 工具栏「保存为技能」 | `&#x2728; 保存为技能` | `Sparkle20` + TextBlock |
| 技能添加按钮 | `+` | `Add20` |
| 新建文件夹 | `&#x1F4C2;` (📂) | `Folder20` |
| 技能生成 | `&#x2728;` (✨) | `Sparkle20` |
| 技能刷新 | `&#x21BB;` (↻) | `ArrowSync20` |
| 项目添加 | `+` | `Add20` |
| 搜索图标 | `&#x1F50D;` (🔍) | `Search24` |
| 技能菜单「插入命令」 | `&#x25B6;  插入命令` | `Play20`（MenuItem.Icon）|
| 技能卡片插入 | `&#x25B6;` (▶) | `Play20` |
| 底部「插入命令」按钮 | `&#x25B6;  插入命令` | `Play20` + TextBlock |
| Thinking 停止按钮 | `⏹` | `Stop20` |
| 移除图片按钮 | `&#x2715;` (✕) | `Dismiss20` |
| 刷新模型按钮 | `&#x21BB;` (↻) | `ArrowSync20` |
| 底部分享技能 | `&#x2728;` (✨) | `Sparkle20` |
| 清空对话 | `&#x1F5D1;` (🗑) | `Delete20` |
| 发送按钮 | `&#x25B6;` (▶) | `Send48` |
| 终端关闭 | `✕` | `Dismiss20` |

#### 3.3 编译验证

```
dotnet build
```

验证：0 error 通过。全应用使用矢量图标，字体统一为 Segoe UI。

---

### 第 4 步：替换底部工具栏控件

**目标**：将输入框、发送按钮、模型选择、模式切换等底部控件替换为 WPFUI 风格。

> ✅ **第 4 步已于 2026-05-13 完成。** 实际改动：
> - InputBox：原生 `TextBox` → `ui:TextBox`，使用 `PlaceholderText` 替代手动 `InputPlaceholder` TextBlock
> - 删除了 `InputPlaceholder` TextBlock 及其所有代码‑后隐藏逻辑（WPFUI 自动管理）
> - 删除了 `InputBox_GotFocus` / `InputBox_LostFocus` 空壳事件处理
> - ModeToggleButton：原生 `Button` + 自定义 ControlTemplate → `ui:Button Appearance="Primary"`
> - PermissionBtn：原生 `Button` + FlatButton 样式 → `ui:Button`
> - SendButton：原生 `Button` + FlatButton 样式 → `ui:Button Appearance="Primary"`
> - RefreshModelsBtn：原生 `Button` + FlatButton 样式 → `ui:Button Appearance="Secondary"`
> - BottomSaveSkillBtn / ClearOutput 按钮：原生 `Button` + FlatButton 样式 → `ui:Button`
> - ImagePreview 移除按钮：原生 `Button` + FlatButton 样式 → `ui:Button`
> - 代码‑后 `SendButton.Content` / `.Background` / `.Foreground` 动态赋值不受影响（`ui:Button` 继承自 WPF Button）
> - `dotnet build` 0 错误通过

#### 4.1 输入框

将 `TextBox` 替换为 WPFUI 的样式化 TextBox。在 AppStyles.xaml 中修改 `InputStyle` 或在 MainWindow.xaml 中直接使用 WPFUI 控件：

```xml
<!-- 使用 WPFUI 风格的 TextBox -->
<ui:TextBox x:Name="InputBox"
            PlaceholderText="输入消息..."
            MinHeight="36" MaxHeight="140"
            AllowDrop="True"
            .../>
```

`ui:TextBox` 自带 Fluent Design 风格的边框、焦点效果和占位文字。

#### 4.2 按钮替换

发送按钮：

```xml
<ui:Button x:Name="SendButton"
           Icon="Send48"
           Content="发送"
           Width="44" Height="28"
           Appearance="Primary"
          .../>
```

WPFUI 的 `Button` 支持 `Appearance` 属性（Primary, Secondary, Danger, Success 等）和 `Icon` 属性。

模式切换按钮：

```xml
<ui:ToggleButton x:Name="ModeToggleButton"
                 Content="构建"
                 Appearance="Primary"
                 .../>
```

#### 4.3 编译验证

```
dotnet build
```

验证：底部工具栏按钮有 Fluent Design 悬停效果和 Ripple 动画。

---

### 第 5 步：替换侧边栏面板

**目标**：将 ProjectSidebar、FilesSidebar、SearchPanel、ProblemsPanel 中的控件替换为 WPFUI 风格。

> ✅ **第 5 步已于 2026-05-13 完成。** 实际改动：
> - **MainWindow 技能搜索框**：移除自定义 Border 包装结构，改用独立 `ui:TextBox` 含 `PlaceholderText`、`Icon="Search24"`、`ClearButtonEnabled`
> - **ProjectSidebar**：添加 `xmlns:ui`；文件夹图标 `&#x1F4C1;` → `SymbolIcon Folder20`；会话图标 `&#x1F4AC;` → `SymbolIcon Chat20`；上下文菜单 `&#x25B6; 在新标签中打开` → `MenuItem.Icon` + `Play20`
> - **SearchPanel**：移除自定义 Border+Grid+图标包装结构，改用独立 `ui:TextBox` 含 `PlaceholderText`、`Icon="Search24"`、`ClearButtonEnabled`
> - **ProblemsPanel**：添加 `xmlns:ui`；`RefreshButton` 改为 `ui:Button` + `SymbolIcon ArrowSync20`；`ClearButton` 改为 `ui:Button` + `SymbolIcon Dismiss20`
> - **FilesSidebar**：添加 `xmlns:ui` 待后续使用，TreeView 保持原生（图标由数据绑定驱动）
> - TreeView 控件保留原生 WPF（WPFUI 不提供 Fluent TreeView 替代），仅替换其中图标
> - `dotnet build` 0 错误通过

#### 5.1 ProjectSidebar

TreeView 仍可使用，但应用 WPFUI 的 TreeView 样式。在 XAML 中移除自定义样式，依赖 WPFUI 默认样式。

或者改用 WPFUI 的 `TreeListBox` 控件。

#### 5.2 技能搜索框

```xml
<ui:TextBox x:Name="SkillSearchBox"
            PlaceholderText="搜索技能..."
            ClearButtonEnabled="True"/>
```

#### 5.3 编译验证

```
dotnet build
```

---

#### 5.4 空白窗口修复

在 Step 5 完成后，应用程序窗口渲染为空白。排查并修复：

**根因 1：** App.xaml 中缺少 `ui:ThemesDictionary`。WPFUI 4.x 需要将主题资源字典合并到应用程序级别资源中，否则 UserControl 内部的 WPFUI 控件（如 `ui:SymbolIcon`）在 XAML 解析时会失败，导致整个窗口树崩溃。

**修复：** 在 App.xaml 中添加：
```xml
xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
...
<ui:ThemesDictionary />
```

**根因 2：** `ui:TextBox` 在 `ui:FluentWindow` 的深嵌套布局中可能导致 XAML 加载失败。

**修复：** 将 `MainWindow.xaml` 中的 InputBox 从 `ui:TextBox` 回退为原生 `TextBox` + 装饰性 `Border` 包裹。

---

### 第 6 步：替换聊天气泡颜色和字体

**目标**：移除气泡代码中的字体和颜色硬编码，使气泡自然继承 WPFUI 主题。

> ✅ **第 6 步已于 2026-05-13 完成。** 实际改动：
> - `MainWindow.Chat.cs` 中 7 处 `FontFamily = new FontFamily("Consolas, Courier New")` 已移除
> - 受影响的：`ShowWelcome` 提示文字、`CreateBubbleFromEntry` 系统消息、`AddUserBubble` 用户内容、`AddAssistantBubble` AI 内容、`AddStoredBubble` 恢复消息、`SysLine` 系统行、`BuildMessageContent` 纯文本段
> - 文字现在从父窗口继承 `Segoe UI, Microsoft YaHei`
> - 代码块（`CodeBlock.Build`）、输入框（`InputBox`）等需要等宽字体的位置**不受影响**（各自独立设置）
> - 气泡颜色依然使用自定义 `ThemeManager` 颜色（第 9 步统一替换）
> - `dotnet build` 0 错误通过

#### 6.1 修改 MainWindow.Chat.cs

将气泡中的字体设置改为继承：

```diff
- FontFamily = new FontFamily("Consolas, Courier New"),
+ // 不再覆盖字体，从父级继承
```

#### 6.2 修改气泡工厂

保留气泡结构（Border + Grid + StackPanel），但背景色和边框色仍使用 `ThemeManager.GetColor`（在最终步前不会废弃 ThemeManager）。

#### 6.3 编译验证

```
dotnet build
```

验证：气泡文字使用 Segoe UI，与整体 UI 协调。

---

### 第 7 步：替换对话框窗口

**目标**：将 ApprovalDialog、SettingsWindow、CommandPaletteWindow、StreamParseLogWindow 等子窗口改为 WPFUI 窗口。

> ✅ **第 7 步已于 2026-05-13 完成。**

#### 7.1 更改摘要

| 文件 | 变更 |
|------|------|
| ApprovalDialog.xaml | `<Window` → `<ui:FluentWindow`，移除 `WindowStyle="None"`、`AllowsTransparency` |
| ApprovalDialog.xaml.cs | `: Window` → `: Wpf.Ui.Controls.FluentWindow` |
| SettingsWindow.xaml | `<Window` → `<ui:FluentWindow`，移除 `BorderBrush`/`BorderThickness` |
| SettingsWindow.xaml.cs | `: Window` → `: Wpf.Ui.Controls.FluentWindow` |
| CommandPaletteWindow.xaml | `<Window` → `<ui:FluentWindow`，移除 `AllowsTransparency`，添加 `ExtendsContentIntoTitleBar="True"` |
| CommandPaletteWindow.xaml.cs | `: Window` → `: Wpf.Ui.Controls.FluentWindow` |
| StreamParseLogWindow.xaml | `<Window` → `<ui:FluentWindow` |
| StreamParseLogWindow.xaml.cs | `: Window` → `: Wpf.Ui.Controls.FluentWindow` |

所有文件添加 `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"`。

#### 7.2 已知限制

- CommandPaletteWindow 的 `AllowsTransparency="True"` 需要移除（FluentWindow 不支持），改用 `ExtendsContentIntoTitleBar="True"` + `WindowStyle="None"` 实现覆盖层效果
- 子窗口关闭后焦点返回主窗口

#### 7.3 编译验证

```
dotnet build  # 0 errors
```

验证：所有对话框拥有 Fluent Design 窗口边框和阴影。

---

### 第 8 步：替换所有 SymbolIcon 为 Unicode TextBlock 图标

> ✅ **第 8 步已于 2026-05-13 完成。**

**背景**：`Wpf.Ui.Controls.SymbolIcon` 在 v4.3.0 中存在构造函数异常（内部 `FontSize` 资源解析为空字符串），导致整个窗口 XAML 加载失败。因此无法使用 SymbolIcon。

**替代方案**：全部使用带有 Unicode 符号的 `TextBlock`，保持一致的视觉风格。

#### 8.1 Unicode → TextBlock 映射表

用于一致替换：

| 用途 | 字符 | FontSize |
|------|------|----------|
| 添加/新增 | `+` (U+002B) | 14-18 |
| 关闭/删除 | `✕` (U+2715) | 12-14 |
| 文件夹 | `▣` (U+25A3) | 14 |
| 聊天/会话 | `☰` (U+2630) | 12 |
| 搜索 | `⌕` (U+2315) | 13 |
| 播放/运行 | `▶` (U+25B6) | 10-12 |
| 停止 | `■` (U+25A0) | 12 |
| 刷新/同步 | `↻` (U+21BB) | 12-14 |
| 发送 | `➤` (U+27A4) | 16 |
| 主题切换 | `☾` (U+263E) | 16 |
| AI/Sparkle | `✦` (U+2726) | 12-14 |

#### 8.2 替换范围

- `MainWindow.xaml` — 22 处 SymbolIcon 全部替换
- `ProjectSidebar.xaml` — 3 处 SymbolIcon 全部替换（同时移除 `xmlns:ui`）
- `SearchPanel.xaml` — 1 处 SymbolIcon 全部替换（同时移除 `xmlns:ui`）
- `BubbleActions.cs` — 原生代码创建 UI，无需改动
- `CodeBlock.cs` — 原生代码创建 UI，无需改动

#### 8.3 编译验证

```
dotnet build  # 0 errors
```

**经验教训**：
- `ui:SymbolIcon` 在 WPFUI v4.3.0 不可用
- `ui:ThemesDictionary` 需合并到 App.xaml
- 所有 `xmlns:ui` 统一使用 `http://schemas.lepo.co/wpfui/2022/xaml`
- 已不需要 `xmlns:ui` 的文件应移除命名空间声明

---

### 第 9 步：删除旧主题系统

**目标**：删除 `Theme.cs` `ThemeManager.cs`，改为使用 WPFUI 内置主题系统。

#### 9.1 代码更改

WPFUI v4 主题系统使用方式：

```csharp
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

// 设置主题
ApplicationThemeManager.Apply(
    ApplicationTheme.Dark,
    WindowBackdropType.Mica,
    true);  // true = 更新所有窗口

// 或
ApplicationThemeManager.Apply(ApplicationTheme.Light);

// 监听主题变化
ApplicationThemeManager.Changed += (_, _) => { /* 重新应用颜色 */ };
```

实际项目中需要将原来的主题切换代码(ThemeToggle_Click)改为调用 `ApplicationThemeManager.Apply()`。

#### 9.2 颜色适配方案

WPFUI v4 通过 `ApplicationThemeManager.Apply()` 内部管理资源字典，不提供 `Fluent.xaml` 外部文件。但我们的项目中 `CodeBlock.cs`、`CollapsibleGroup.cs`、`DiffViewer.cs` 等文件使用了 `ThemeManager.GetColor("ThemeCodeBg")` 获取自定义主题色。

**方案 A（推荐）**：在 `App.xaml` 中补充 WPFUI 没有的自定义颜色资源（代码高亮色、Diff 色等），使用与 WPFUI 主题无关的固定色值。

```xml
<!-- App.xaml 中保留的自定义颜色 -->
<SolidColorBrush x:Key="ThemeCodeBg" Color="#0D1117"/>
<SolidColorBrush x:Key="SyntaxKeyword" Color="#569CD6"/>
<!-- ... 其他语法高亮色 -->
```

然后保留一个简化版的颜色管理类（或直接在需要的地方引用 `Application.Current.Resources`），替换原来 `ThemeManager.GetColor()` 的调用。

#### 9.3 可删除的文件

```
Themes/Theme.cs           → 删除
Themes/ThemeManager.cs    → 删除
Themes/AppStyles.xaml中：
  InputStyle              → 删除（改用WPFUI TextBox）
  FlatButton              → 删除（改用WPFUI Button）
  SidebarScrollBar        → 删除（WPFUI自带）
  SidebarScrollViewer     → 删除（WPFUI自带）
  SidebarIconBtn          → 删除（WPFUI Button + SymbolIcon）
  PathBox                 → 删除
  TreeView/TreeViewItem   → 删除（WPFUI自带）
  ContextMenu/MenuItem    → 可保留或删除
  Separator               → 可保留或删除
```

#### 9.4 清理 App.xaml

删除 App.xaml 中所有 `DynamicResource` 默认值（约 50+ 个 `SolidColorBrush`），仅保留：
- 自定义语法高亮色（CodeBlock.cs 使用）
- 自定义功能色（DiffViewer.cs 使用）
- Converters

#### 9.5 移除 ThemeManager 监听

`App.xaml.cs` 中删除 `ThemeManager.Instance.ThemeChanged += OnThemeChanged;` 及相关方法。

#### 9.6 更新 MainWindow.Utilities.cs

保留 `H()` 方法（被 CodeBlock.cs 等使用），可以保留下面的辅助方法。

#### 9.7 编译验证

```
dotnet build
```

验证：应用外观不变（颜色由 App.xaml 中保留的自定义颜色维持），功能正常。

---

### 第 10 步：最终打磨

**目标**：调整细节，达到最佳视觉效果。

#### 10.1 窗口阴影

FluentWindow 默认自带窗口阴影和圆角，不需要额外配置。

#### 10.2 面板过渡动画

使用 WPFUI 的动画或 `AnimationHelper.cs`，为技能/项目/文件/搜索面板切换添加淡入动画。

#### 10.3 气泡微调

调整气泡边距、行高至舒适阅读比例（推荐 14px 字体 × 1.7 行高 ≈ 24px）。

#### 10.4 统一的间距系统

利用 WPFUI 的间距系统，或使用统一的 Margins/Paddings 规范。

#### 10.5 最终构建和全功能回归测试

```
dotnet build
```

测试所有功能：
- [ ] 新建/切换/关闭标签页
- [ ] 发送消息（文本 + 图片）
- [ ] 流式渲染
- [ ] 技能面板（搜索、使用、新建、删除）
- [ ] 项目面板（展开/折叠、右键菜单）
- [ ] 文件/搜索/问题面板
- [ ] 主题切换
- [ ] 审批对话框
- [ ] 代码高亮
- [ ] 设置窗口
- [ ] 终端面板

---

## 四、总工作量估算

| 步骤 | 内容 | 预估工时 | 可验证 |
|------|------|---------|--------|
| 1 | 安装包 + App.xaml | 0.5h | ✅ 编译 + 运行 |
| 2 | MainWindow 窗口基类 | 2-3h | ✅ 编译 + 运行 |
| 3 | 字体 + 首批图标 | 0.5h | ✅ 编译 + 运行 |
| 4 | 底部工具栏 | 1h | ✅ 编译 + 运行 |
| 5 | 侧边栏面板 | 1-2h | ✅ 编译 + 运行 |
| 6 | 聊天气泡 | 0.5h | ✅ 编译 + 运行 |
| 7 | 对话框窗口 | 1-2h | ✅ 编译 + 运行 |
| 8 | 全部图标替换 | 1-2h | ✅ 编译 + 运行 |
| 9 | 删除旧主题系统 | 1-2h | ✅ 编译 + 运行 |
| 10 | 最终打磨 + 回归测试 | 2-3h | ✅ 全功能测试 |
| **合计** | | **10-16h** | |

---

## 五、风险与注意事项

1. **WPFUI 版本兼容性**：当前项目为 `net9.0-windows`，WPFUI 最新版需验证是否兼容 .NET 9。
2. **FluentWindow 的标题栏**：WPFUI 的 `FluentWindow` 有内置标题栏和窗口按钮，需要将现有的标题栏内容（标签栏等）适配进去，可能涉及布局调整。
3. **ThemeManager 冲突**：WPFUI v4 使用 `Wpf.Ui.Appearance.ApplicationThemeManager`，我们的 `ClaudeCodeGUI.Themes.ThemeManager` 在删除前需要重命名以避免混淆。
4. **`SymbolIcon` 的符号名**：WPFUI 的图标符号名称与 Fluent UI Icons 对应，具体名称需参考 WPFUI 文档或通过 Intellisense 查找。
5. **代码中动态创建 UI**：`MainWindow.Chat.cs` `CodeBlock.cs` `BubbleActions.cs` 等文件在 C# 代码中动态创建 WPF 控件，需要决定是否使用 WPFUI 控件还是保留原生控件。推荐保留原生控件（性能更好），让 WPFUI 主题自动覆盖其样式。
