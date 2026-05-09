using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>多标签管理：标签切换、关闭、新建、聊天气泡保存/恢复</summary>
public partial class MainWindow : Window
{
    // ── 会话管理器 ────────────────────────────────────────
    private SessionManager _sessionManager = new();
    private SessionTabItem? _lastActiveTab;

    // ════════════════════════════════════════════════════════
    //  初始化
    // ════════════════════════════════════════════════════════

    /// <summary>初始化多会话管理器并创建第一个标签</summary>
    private void InitializeSessionManager()
    {
        _sessionManager.TabActivated += OnTabActivated;
        _sessionManager.TabClosed += OnTabClosed;

        // 绑定 ItemsControl 到标签集合 — 这是标签显示的关键绑定
        SessionTabBar.ItemsSource = _sessionManager.Tabs;

        // 创建第一个标签，复用已有的 _session
        var firstTab = _sessionManager.CreateTab("会话 1", _session);
        _session.ClaudeSessionId = Guid.NewGuid().ToString("D");

        // 同步 TimelineModel 引用
        _sessionTabTimeline = firstTab.Timeline;

        // 保存当前 ChatPanel 中的欢迎气泡到第一个标签
        firstTab.SaveChatBubbles(ChatPanel.Children);

        // 激活第一个标签
        _sessionManager.ActivateTab(firstTab);
        _lastActiveTab = firstTab;
    }

    // ════════════════════════════════════════════════════════
    //  标签事件处理
    // ════════════════════════════════════════════════════════

    /// <summary>点击标签 → 切换会话；双击 → 重命名</summary>
    private void TabItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is SessionTabItem tab)
        {
            if (e.ClickCount >= 2)
            {
                e.Handled = true;
                ShowRenameDialog(tab);
            }
            else
            {
                _sessionManager.ActivateTab(tab);
            }
        }
    }

    /// <summary>弹出重命名会话对话框（主题色 + 鼠标位置定位）</summary>
    private void ShowRenameDialog(SessionTabItem tab)
    {
        var txtPrimary = ThemeManager.GetColor("ThemeTextPrimary");
        var txtSecondary = ThemeManager.GetColor("ThemeTextSecondary");
        var surface1 = ThemeManager.GetColor("ThemeSurface1");
        var surface2 = ThemeManager.GetColor("ThemeSurface2");
        var accent = ThemeManager.GetColor("ThemeAccent");
        var border1 = ThemeManager.GetColor("ThemeBorder1");

        var inputBox = new TextBox
        {
            Text = tab.Name,
            Background = surface2,
            Foreground = txtPrimary,
            BorderBrush = accent,
            BorderThickness = new Thickness(1),
            FontSize = 12, FontFamily = new FontFamily("Segoe UI"),
            MinWidth = 80, MaxWidth = 200,
        };

        var popup = new Window
        {
            Width = 220, Height = 80,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = surface1,
        };

        // 鼠标位置定位：右下偏移避免遮挡标签
        var cursorPos = GetCursorPosition();
        popup.Left = cursorPos.X - 100;
        popup.Top = cursorPos.Y + 20;

        popup.Content = new Border
        {
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "重命名会话",
                        Foreground = txtSecondary,
                        FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                        Margin = new Thickness(0, 0, 0, 6)
                    },
                    inputBox
                }
            }
        };

        inputBox.KeyDown += (s, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                string newName = inputBox.Text.Trim();
                if (!string.IsNullOrEmpty(newName))
                    tab.Name = newName;
                popup.Close();
            }
            else if (ke.Key == Key.Escape)
            {
                popup.Close();
            }
        };
        inputBox.Loaded += (s, _) => { inputBox.SelectAll(); inputBox.Focus(); };
        popup.ShowDialog();
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private static Point GetCursorPosition()
    {
        GetCursorPos(out POINT p);
        return new Point(p.X, p.Y);
    }

    /// <summary>点击 × → 关闭标签</summary>
    private void TabClose_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TextBlock tb && tb.DataContext is SessionTabItem tab)
            _sessionManager.CloseTab(tab);
    }

    /// <summary>点击 + → 新建标签（归属项目，自动或选项目）</summary>
    private void AddTab_Click(object sender, MouseButtonEventArgs e)
    {
        var projects = _projectManager.Projects;
        if (projects.Count == 0)
        {
            SysLine("请先在项目面板中添加项目。", BrYellow);
            StatusBar.Text = "无项目 — 请先添加项目";
            return;
        }

        ProjectItem project;
        if (projects.Count == 1)
        {
            project = projects[0];
        }
        else
        {
            // 多项目：弹出选择对话框
            var picked = ShowProjectPicker();
            if (picked == null) return; // 用户取消
            project = picked;
        }

        // 创建标签
        var state = new SessionState();
        string claudeSessionId = Guid.NewGuid().ToString("D");
        state.ClaudeSessionId = claudeSessionId;

        // 在项目中创建会话记录（会持久化到 JSON）
        var sessionItem = _projectManager.AddSession(project, $"会话 {project.Sessions.Count + 1}", claudeSessionId);
        state.SessionItemId = sessionItem.Id;
        _conversationStore.CreateConversation(sessionItem.Id, claudeSessionId);
        ProjectSidebarCtrl.RefreshTree();
        _projectManager.Save();

        state.Name = sessionItem.Name;
        state.WorkingDirectory = project.WorkingDirectory;
        var tab = _sessionManager.CreateTab(sessionItem.Name, state);
        sessionItem.TabId = tab.Id;
        _projectManager.Save();
        _sessionManager.ActivateTab(tab);
        StatusBar.Text = $"新建会话：{project.Name}/{sessionItem.Name}";
    }

    /// <summary>多项目时弹出项目选择对话框</summary>
    private ProjectItem? ShowProjectPicker()
    {
        var accent = ThemeManager.GetColor("ThemeAccent");
        var bgSurface1 = ThemeManager.GetColor("ThemeSurface1");
        var bgSurface2 = ThemeManager.GetColor("ThemeSurface2");
        var bgSurface3 = ThemeManager.GetColor("ThemeSurface3");
        var txtPrimary = ThemeManager.GetColor("ThemeTextPrimary");
        var txtSecondary = ThemeManager.GetColor("ThemeTextSecondary");
        var border1 = ThemeManager.GetColor("ThemeBorder1");

        var win = new Window
        {
            Title = "选择项目",
            Width = 360, Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = bgSurface1,
            FontFamily = new FontFamily("Segoe UI"),
        };

        var listBox = new ListBox
        {
            Background = bgSurface2,
            Foreground = txtPrimary,
            BorderBrush = border1,
            BorderThickness = new Thickness(1),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 10),
            DisplayMemberPath = "Name",
        };
        foreach (var p in _projectManager.Projects) listBox.Items.Add(p);
        listBox.SelectedIndex = 0;

        var okBtn = new Button
        {
            Content = "确定",
            Background = accent,
            Foreground = H("#FFFFFF"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 7, 16, 7),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
            IsEnabled = listBox.SelectedItem != null,
            MinWidth = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var cancelBtn = new Button
        {
            Content = "取消",
            Background = bgSurface3,
            Foreground = txtSecondary,
            BorderBrush = border1,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 7, 16, 7),
            FontSize = 12,
            Cursor = Cursors.Hand,
            MinWidth = 80,
        };

        ProjectItem? result = null;
        okBtn.Click += (_, _) => { if (listBox.SelectedItem is ProjectItem pi) result = pi; win.DialogResult = true; };
        cancelBtn.Click += (_, _) => win.DialogResult = false;
        listBox.MouseDoubleClick += (_, _) => { if (listBox.SelectedItem is ProjectItem pi2) { result = pi2; win.DialogResult = true; } };
        listBox.SelectionChanged += (_, _) => okBtn.IsEnabled = listBox.SelectedItem != null;

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = "选择一个项目以新建会话：",
            Foreground = txtSecondary, FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
        });
        root.Children.Add(listBox);
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(new Border { Width = 8 });
        btnRow.Children.Add(okBtn);
        root.Children.Add(btnRow);
        win.Content = root;
        win.Loaded += (_, _) => listBox.Focus();

        return win.ShowDialog() == true ? result : null;
    }

    // ════════════════════════════════════════════════════════
    //  标签右键菜单
    // ════════════════════════════════════════════════════════

    /// <summary>右键菜单打开时，设置 DataContext 并启用/禁用菜单项</summary>
    private void TabContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && menu.PlacementTarget is Border b
                                       && b.DataContext is SessionTabItem tab)
        {
            // 将 DataContext 传递给 ContextMenu，让 MenuItem 继承
            menu.DataContext = tab;

            // 启用/禁用"停止"菜单项
            foreach (var item in menu.Items)
            {
                if (item is MenuItem mi)
                {
                    if (mi.Header?.ToString()?.Contains("停止") == true)
                        mi.IsEnabled = tab.Job != null;
                    else if (mi.Header?.ToString()?.Contains("等待中") == true)
                        mi.Visibility = tab.Job != null ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>右键菜单：停止会话</summary>
    private void TabStopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromMenuItem(sender) is not SessionTabItem tab) return;

        tab.CTS?.Cancel();
        try { tab.Job?.Kill(entireProcessTree: true); } catch { }
        tab.Job = null;
        tab.CTS = null;
        tab.HasContinue = false;

        if (tab == _lastActiveTab)
        {
            _cts = null; _job = null;
            SetBusy(false);
            SysLine("已停止。", BrYellow);
        }
    }

    /// <summary>右键菜单：清空对话</summary>
    private void TabClearMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromMenuItem(sender) is not SessionTabItem tab) return;

        // 保存当前气泡（如果有）
        if (tab == _lastActiveTab)
            tab.SaveChatBubbles(ChatPanel.Children);

        tab.ChatBubbles.Clear();

        if (tab == _lastActiveTab)
        {
            ChatPanel.Children.Clear();
            ShowWelcome();
        }
    }

    /// <summary>右键菜单：关闭标签</summary>
    private void TabCloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromMenuItem(sender) is SessionTabItem tab)
            _sessionManager.CloseTab(tab);
    }

    /// <summary>从 MenuItem 回溯到 ContextMenu 获取 SessionTabItem</summary>
    private static SessionTabItem? GetTabFromMenuItem(object sender)
    {
        if (sender is MenuItem mi)
        {
            // 尝试从 DataContext 获取
            if (mi.DataContext is SessionTabItem dt) return dt;

            // 回退：从父级 ContextMenu 获取
            var ctxMenu = mi.Parent as ContextMenu;
            if (ctxMenu?.DataContext is SessionTabItem dt2) return dt2;
            if (ctxMenu?.Tag is SessionTabItem dt3) return dt3;
        }
        return null;
    }

    // ════════════════════════════════════════════════════════
    //  会话管理器事件
    // ════════════════════════════════════════════════════════

    /// <summary>标签被激活时：保存旧标签 UI、恢复新标签 UI</summary>
    private void OnTabActivated(object? sender, SessionTabItem tab)
    {
        // ── 保存上一个标签的状态 ──
        if (_lastActiveTab != null && _lastActiveTab != tab)
        {
            var prev = _lastActiveTab;
            prev.State.DraftInput = InputBox.Text;
            prev.SaveChatBubbles(ChatPanel.Children);
            // 保存进程/运行状态
            prev.Job = _job;
            prev.CTS = _cts;
            prev.HasContinue = _hasContinue;
            prev.Turns = _turns;
            prev.LastOutputTime = _lastOutputTime;
            prev.State.WorkingDirectory = _workDir;
            // 保存图片附件
            prev.AttachedImagePath = _attachedImagePath;

            // 解除旧 SessionState 的 PropertyChanged 事件
            prev.State.PropertyChanged -= OnSessionPropertyChanged;
        }

        // ── 切换到新标签 ──
        if (_lastActiveTab != tab)
        {
            // 绑定新 SessionState
            _session = tab.State;
            _session.PropertyChanged += OnSessionPropertyChanged;
            this.DataContext = _session;

            // 同步 TimelineModel 引用
            _sessionTabTimeline = tab.Timeline;

            // 恢复聊天气泡：优先从 TimelineModel 渲染
            if (tab.Timeline.Entries.Count > 0)
            {
                RenderTimelineToPanel(tab.Timeline);
            }
            else
            {
                bool hadBubbles = tab.RestoreChatBubbles(ChatPanel.Children);
                if (!hadBubbles)
                {
                    ShowWelcome();
                    tab.SaveChatBubbles(ChatPanel.Children);
                }
            }

            // 恢复输入框草稿
            InputBox.Text = tab.State.DraftInput;

            // 恢复进程/运行状态
            _job = tab.Job;
            _cts = tab.CTS;
            _hasContinue = tab.HasContinue;
            _turns = tab.Turns;
            _lastOutputTime = tab.LastOutputTime;
            _workDir = tab.State.WorkingDirectory;

            // 恢复图片附件
            _attachedImagePath = tab.AttachedImagePath;
            if (!string.IsNullOrEmpty(_attachedImagePath) && System.IO.File.Exists(_attachedImagePath))
                ShowImagePreview(_attachedImagePath);
            else
            {
                _attachedImagePath = null;
                ImagePreviewBar.Visibility = Visibility.Collapsed;
                ImagePreviewThumb.Source = null;
            }

            // 更新子标题和工作目录显示
            SubtitleLabel.Text = _claudeExe != null ? System.IO.Path.GetFileName(_claudeExe) : "未启动";

            // ═══ 同步底部工具栏 UI 到当前标签 ═══
            // 模型选择：空选中时补默认值，确保每个标签独立
            if (string.IsNullOrEmpty(_session.SelectedModel) && _allModels.Count > 0)
                _session.SelectedModel = _allModels[0];
            ModelComboBox.ItemsSource = _session.AvailableModels.Count > 0
                ? _session.AvailableModels
                : _allModels.Count > 0 ? _allModels : ModelComboBox.ItemsSource;
            ModelComboBox.SelectedItem = _session.SelectedModel;
            // 模式按钮
            ModeToggleButton.Content = _session.ModeLabel;
            // 权限按钮
            PermissionBtn.Content = _session.PermissionLabel;
            ActivityLogHost.Visibility = _session.HasTasks ? Visibility.Visible : Visibility.Collapsed;
            // Token 显示
            TokenPercentText.Text = _session.UsedTokens > 0
                ? $"{_session.TokensPercentage:F0}%"
                : "0%";

            // 重置忙状态为新标签的状态（每个标签独立运行进程）
            if (tab.Job != null && !tab.Job.HasExited)
            {
                // 标签有正在运行的进程 — 恢复忙状态
                _busy = true;
                ThinkingBar.Visibility = Visibility.Visible;
            }
            else
            {
                // 标签无进程 — 确保界面空闲
                _busy = false;
                _timer.Stop(); _sw.Stop();
                ThinkingBar.Visibility = Visibility.Collapsed;
                SendButton.Content = _session.SendButtonText;
                SendButton.Background = H("#0A84FF");
                SendButton.Foreground = H("#FFFFFF");
                SendButton.IsEnabled = _ready;
            }
        }

        _lastActiveTab = tab;
    }

    /// <summary>标签被关闭时处理</summary>
    private void OnTabClosed(object? sender, SessionTabItem tab)
    {
        if (_sessionManager.Tabs.Count == 0)
        {
            // 没有标签了 → 显示欢迎页面
            ChatPanel.Children.Clear();
            ShowWelcome();
            _lastActiveTab = null;
            _session = new SessionState();
            this.DataContext = _session;
        }
    }
}
