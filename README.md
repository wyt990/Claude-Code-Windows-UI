# Claude Code GUI

适用于 [Claude Code CLI](https://code.claude.com) 的 Windows 桌面图形界面 — 通过原生聊天界面与 Claude Code 交互，无需使用终端。

## 功能特性

- **多标签会话** — 同时运行多个独立对话，每个标签拥有独立的进程、聊天历史和输入状态
- **双模式聊天** — 在**构建**和**计划**两种交互模式间切换，提供三级权限（监督模式 / 自动接受编辑 / 完全访问）
- **项目管理** — 将工作组织为项目（工作目录），每个项目包含持久化的会话树，会话自动关联到标签
- **模型选择** — 从 `claudecode --list-models --json` 加载可用模型，每个会话可独立切换
- **任务侧边栏** — 在可折叠的侧面板中查看和跟踪待办事项（由计划模式创建）
- **主题系统** — 内置 6 套主题：默认、GitHub、苹果、Monokai、Dracula、暖纸，可从标题栏一键切换
- **Token 用量指示器** — 环形进度条实时显示当前 Token 使用量占模型上限的比例
- **技能侧边栏** — 浏览、搜索、创建、编辑和删除可复用的 `.md` 技能文件，支持文件夹分组和项目级技能（`.claude/skills/`）
- **保存为技能** — 让 Claude 将任意对话一键总结为可复用的技能文件
- **技能管理** — 内联重命名、编辑描述、整理到文件夹、从侧边栏删除
- **图片附件** — 支持 PNG/JPG/WEBP 格式图片，附带预览功能
- **实时进度指示** — 动画旋转图标、滚动实时输出预览、耗时计数器和对数进度条
- **超时警告** — 30 秒无输出显示黄色警告，2 分钟无输出显示红色警告
- **活动日志** — 在进度条区域显示最近 3 行实时输出
- **标签管理** — 创建、关闭、双击重命名标签，右键菜单支持停止/清空/关闭
- **自定义窗口边框** — 可拖拽标题栏、最小化/最大化/关闭按钮
- **多窗口** — 启动第二个实例并行工作
- **自动权限** — 自动更新 `~/.claude/settings.json`，授予工作目录和技能目录的文件访问权限
- **聊天复制** — 在聊天面板中按 `Ctrl+C` 即可复制全部对话文本到剪贴板

## 系统要求

- Windows 10 或更高版本（x64）
- 已安装 [Claude Code CLI](https://code.claude.com)。本 GUI 使用 `claudecode` 命令（自定义分支，维护于 [github.com/wyt990/claude-code-haha](https://github.com/wyt990/claude-code-haha)）。
  ```
  npm install -g @anthropic-ai/claude-code
  ```
  或使用 [claude.com/download](https://claude.com/download) 上的本地安装程序
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（从源码构建时需要）

## 快速开始

### 方式一 — 下载安装包（推荐）

从 [Releases](../../releases) 页面下载 `ClaudeCodeUI_Setup.exe` 并运行。

> 首次启动：Windows SmartScreen 可能显示警告，请点击**更多信息 → 仍要运行**。

### 方式二 — 从源码构建

```bash
git clone https://github.com/YOUR_USERNAME/ClaudeCodeUI.git
cd ClaudeCodeUI

dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish

.\publish\ClaudeCodeUI.exe
```

## 使用说明

1. **添加项目** — 在侧边栏切换到**项目**标签，点击 `+`，输入项目名称和工作目录
2. **启动会话** — 在项目树中双击会话来打开标签。输入消息后按 **Enter** 发送
   - `Ctrl+Enter` / `Shift+Enter` 换行
3. **技能侧边栏** — 点击**技能**标签浏览可用技能。点击技能卡片上的 `▶` 将 `/command` 插入输入框，或点击技能查看预览
4. **创建技能** — 点击 `+` 新建技能文件，或点击 `✨` 让 Claude 将当前对话总结为技能
5. **切换模式** — 使用底部工具栏在**构建**和**计划**模式间切换，或循环切换权限级别
6. **管理标签** — 点击标签栏的 `+` 新建会话，双击标签重命名，右键查看更多选项
7. **切换主题** — 点击标题栏的 `☾` 按钮循环切换主题

## 技能

技能是存储在 `~/.claude/skills/`（或项目内的 `.claude/skills/`）中的 `.md` 文件，遵循 [Agent Skills Open Standard](https://agentskills.io) 标准。

每个技能文件包含 YAML 头部信息：

```markdown
---
name: convert-ogg-wav
description: >
  将当前文件夹中的所有 .ogg 文件使用 ffmpeg 转换为 .wav 格式。
  当用户要求批量转换音频文件时使用。
---

## 步骤
1. 使用 `Get-ChildItem *.ogg` 列出所有 .ogg 文件
2. 对每个文件执行 `ffmpeg -i input.ogg output.wav`
3. 报告转换了多少个文件
```

在输入框中用 `/convert-ogg-wav` 调用。

## 项目结构

```
ClaudeCodeUI/
├── App.xaml                   # WPF 应用程序入口
├── App.xaml.cs
├── MainWindow.xaml            # UI 布局（标题栏、侧边栏、聊天区、输入框）
├── MainWindow.xaml.cs         # 主窗口（模式切换、侧边栏标签）
├── MainWindow.Chat.cs         # 聊天气泡构建器
├── MainWindow.Commands.cs     # 命令操作：模型加载、权限管理、保存为技能
├── MainWindow.Session.cs      # 会话管理：发送、流式读取、进程控制
├── MainWindow.Skills.cs       # 技能面板：加载、搜索、增删改
├── MainWindow.Tabs.cs         # 多标签管理：激活、关闭、重命名
├── MainWindow.Utilities.cs    # 辅助方法：Claude 检测、ANSI 清理
├── Enums.cs                   # InteractionMode、RuntimeMode、TodoStatus
├── Converters.cs              # WPF 值转换器
├── ModelListResponse.cs       # --list-models 响应的 JSON 模型
├── ProjectItem.cs             # 项目 + 会话数据模型
├── ProjectManager.cs          # 项目增删改查（JSON 持久化）
├── ProjectSidebar.xaml/.cs    # 项目树侧边栏控件
├── SessionManager.cs          # 多标签会话生命周期管理器
├── SessionState.cs            # 每个标签的状态（模式、权限、Token、任务）
├── SessionTabItem.cs          # 标签数据模型（进程、聊天气泡、历史）
├── SkillItem.cs               # 技能卡片数据模型
├── TodoItem.cs                # 任务项数据模型
├── PlanSidebar.xaml/.cs       # 任务侧边栏控件
├── Themes/
│   └── AppStyles.xaml         # 完整主题系统（6 套主题）
├── img/                       # 应用图标
└── ClaudeCodeUI.csproj        # .NET 10 项目文件
```

## 工作原理

每条消息都会以适当的参数启动一个 `claudecode` 进程：

```
claudecode [--continue] [-p "消息内容"] [--interaction-mode=build|plan]
           [--accept-edits|--dangerously-skip-permissions] [--model=MODEL]
```

- `--continue` — 延续之前的会话上下文进行多轮对话
- `--interaction-mode` — 切换构建/计划策略
- 权限标志 — 控制自动化程度
- `--model` — 选择 Claude 模型变体
- 输出实时流式传输到聊天气泡，并经过 ANSI 清理

每个标签持有独立的进程，因此您可以同时运行多个互不干扰的对话。

## 与官方 Claude Code CLI 的区别

本 GUI 使用 `claudecode`（官方 CLI 的自定义分支/增强版，维护于 [github.com/wyt990/claude-code-haha](https://github.com/wyt990/claude-code-haha)）而非官方的 `claude` 命令。所有路径和自动检测逻辑优先适配 `claudecode`，官方 `claude` 作为回退方案。

## 许可证

MIT — 详见 [LICENSE](LICENSE)。

## 致谢

基于 [Anthropic Claude Code CLI](https://code.claude.com) 构建。
UI 设计灵感来自 macOS 设计语言。
