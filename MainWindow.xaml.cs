using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI
{
    public partial class MainWindow : Window
    {
        // ── Current Session State ─────────────────────────────────
        private SessionState _session = new();

        // ── Current tab's TimelineModel (convenience ref) ────────
        private TimelineModel? _sessionTabTimeline;

        // ── Process state (legacy, for compatibility) ────────────
        private Process?                 _job;
        private CancellationTokenSource? _cts;
        private bool                     _busy;
        private bool                     _ready;
        private bool                     _hasContinue;
        private string                   _workDir = "";
        private int                      _turns;
        private string?                  _claudeExe;
        private string?                  _attachedImagePath;
        private DateTime                 _lastOutputTime = DateTime.Now;
        private bool                     _maximized;
        private double                   _normalLeft, _normalTop, _normalWidth, _normalHeight;

        // ── Project manager ─────────────────────────────────────
        private readonly ProjectManager _projectManager = new();

        // ── Conversation persistence (SQLite) ────────────────────
        private readonly ConversationStore _conversationStore = new();

        // ── Shared model list (from Claude CLI, same for all tabs) ─
        private List<string> _allModels = new();

        // ── Sidebar tab state ───────────────────────────────────
        private int _sidebarTabIndex = 0; // 0=技能, 1=项目, 2=文件, 3=搜索, 4=问题

        // ── Skills root ────────────────────────────────────────
        private string _skillsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "skills");
        private string?    _selectedSkillPath;
        private SkillItem? _selectedSkillItem;

        // ── Animation ──────────────────────────────────────────
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(90) };
        private readonly Stopwatch       _sw    = new();
        private int                      _fi;
        private static readonly string[] Frames = { "⠋","⠙","⠹","⠸","⠼","⠴","⠦","⠧","⠇","⠏" };

        // ── ANSI ───────────────────────────────────────────────
        private static readonly Regex Ansi = new(
            @"\x1B(?:[@-Z\\-_]|\[[0-9;]*[A-Za-z]|\][^\x07]*\x07)", RegexOptions.Compiled);

        // ── Brushes (从主题系统获取) ────────────────────────────────
        private SolidColorBrush BrDefault => ThemeManager.GetColor("ThemeTextPrimary");
        private SolidColorBrush BrDim => ThemeManager.GetColor("ThemeTextSecondary");
        private SolidColorBrush BrMuted => ThemeManager.GetColor("ThemeTextMuted");
        private SolidColorBrush BrBlue => ThemeManager.GetColor("ThemeAccent");
        private SolidColorBrush BrGreen => ThemeManager.GetColor("ThemeSuccess");
        private SolidColorBrush BrYellow => ThemeManager.GetColor("ThemeWarning");
        private SolidColorBrush BrRed => ThemeManager.GetColor("ThemeError");
        private SolidColorBrush BrOrange => ThemeManager.GetColor("ThemeBrand");
        private SolidColorBrush BrBdrH => ThemeManager.GetColor("ThemeBorder1");
        private SolidColorBrush BrBdrA => ThemeManager.GetColor("ThemeBorder2");
        private SolidColorBrush BrBgUser => ThemeManager.GetColor("ThemeUserBg");
        private SolidColorBrush BrBgAst => ThemeManager.GetColor("ThemeAssistantBg");
        private SolidColorBrush BrUser => ThemeManager.GetColor("ThemeAccent");
        private SolidColorBrush BrAst => ThemeManager.GetColor("ThemeBrand");
        private SolidColorBrush BrBrowseGray => ThemeManager.GetColor("ThemeSurface3");
        private SolidColorBrush BrBrowseGreen => ThemeManager.GetColor("ThemeSuccessBg");

        // ══════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();

            // ── Command bindings ──
            CommandBindings.Add(new CommandBinding(NewSessionCmd, NewSession_Execute, NewSession_CanExecute));
            CommandBindings.Add(new CommandBinding(CloseTabCmd, CloseTab_Execute, CloseTab_CanExecute));
            CommandBindings.Add(new CommandBinding(SwitchTabCmd, SwitchTab_Execute, SwitchTab_CanExecute));
            CommandBindings.Add(new CommandBinding(QuickOpenCmd, QuickOpen_Execute));
            CommandBindings.Add(new CommandBinding(FindInChatCmd, FindInChat_Execute));
            CommandBindings.Add(new CommandBinding(CommandPaletteCmd, CommandPalette_Execute));
            CommandBindings.Add(new CommandBinding(ExportSessionCmd, ExportSession_Execute, ExportSession_CanExecute));
            CommandBindings.Add(new CommandBinding(OpenSettingsCmd, OpenSettings_Execute, OpenSettings_CanExecute));
            CommandBindings.Add(new CommandBinding(ToggleTerminalCmd, ToggleTerminal_Execute));

            _timer.Tick += OnTick;
            Loaded += (_, _) =>
            {
                // Initialize session state
                _session.WorkingDirectory = WorkingDirBox.Text;
                _session.DraftInput = "";
                _session.AvailableModels = new List<string>();
                _session.SelectedModel = "";

                // Set DataContext for XAML binding
                this.DataContext = _session;
                // PropertyChanged will be hooked by InitializeSessionManager() via OnTabActivated

                // Initialize model selection ComboBox
                ModelComboBox.ItemsSource = _session.AvailableModels;
                ModelComboBox.SelectionChanged += (s, e) =>
                {
                    if (ModelComboBox.SelectedItem is string model)
                        _session.SelectedModel = model;
                };

                // Initialize permission button
                PermissionBtn.Click += OpenPermissionPopup_Click;

                // Initialize send button
                SendButton.Click += SendMessage_Click;

                // Initialize input box
                InputBox.GotFocus += InputBox_GotFocus;
                InputBox.LostFocus += InputBox_LostFocus;
                InputBox.TextChanged += (s, e) => { _session.DraftInput = InputBox.Text; };

                InputBox.Focus();
                LoadSkillsTree();

                // Initialize multi-tab session manager
                InitializeSessionManager();

                // Initialize project manager and sidebar
                _projectManager.Load();
                ProjectSidebarCtrl.SetProjects(_projectManager);
                ProjectSidebarCtrl.SessionActivated += Sidebar_SessionActivated;
                ProjectSidebarCtrl.SessionDeleted += Sidebar_SessionDeleted;
                ProjectSidebarCtrl.SessionRenamed += Sidebar_SessionRenamed;

                // ═══ IDE panel event subscriptions ═══
                if (FindName("SearchPanelCtrl") is SearchPanel sp)
                    sp.OpenFileRequested += (_, args) => OpenCodeViewer(args.filePath, args.lineNumber);
                if (FindName("FilesSidebarCtrl") is FilesSidebar fs)
                    fs.FileActivated += (_, filePath) => OpenCodeViewer(filePath);
                if (FindName("ProblemsCtrl") is ProblemsPanel pp)
                    pp.ProblemDoubleClicked += (_, args) => OpenCodeViewer(args.filePath, args.lineNumber);

                // ═══ Auto-detect Claude CLI at startup ═══
                // (the old Browse→DoStartSession flow is gone with the collapsed toolbar)
                _claudeExe = FindClaude();
                if (_claudeExe != null)
                {
                    _workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _session.WorkingDirectory = _workDir;
                    SetReady(true);
                    StatusBar.Text = "就绪 — 在项目标签中添加项目后双击会话来开始对话";
                    // 刷新模型列表（旧 DoStartSession 中也会调用）
                    RefreshModels_Click(null!, null!);
                }
                else
                {
                    SysLine("未找到 claudecode 程序。", BrRed);
                    foreach (var p in Candidates()) SysLine($"  {p}", BrMuted);
                    SysLine("请安装 claudecode 后重新启动应用。", BrYellow);
                    StatusBar.Text = "未找到 claudecode — 请安装后重启";
                }

                // 订阅 Token 用量更新事件
                _session.TokenUsageUpdated += OnTokenUsageUpdated;
                UpdateTokenIndicator();

                // 默认侧边栏切换到项目标签
                SidebarTabProjects_Click(null!, null!);
            };
            Closed += (_, _) => KillJob();
            ShowWelcome();
        }

        // ── Animation tick ─────────────────────────────────────
        private void OnTick(object? s, EventArgs e)
        {
            _fi = (_fi + 1) % Frames.Length;
            SpinnerText.Text = Frames[_fi];
            if (_busy) SendButton.Content = Frames[_fi] + "  Running";

            var ts = _sw.Elapsed;
            ElapsedLabel.Text = ts.TotalSeconds < 60
                ? $"{(int)ts.TotalSeconds}s"
                : $"{(int)ts.TotalMinutes}m{ts.Seconds:D2}s";
            ElapsedLabel.Foreground =
                ts.TotalSeconds < 15 ? BrBlue :
                ts.TotalSeconds < 45 ? BrYellow : BrRed;
            // ThinkingBar 首行：不镜像助手流式正文；有任务时显示当前步骤摘要，否则显示等待/静默提示
            if (_session.Tasks.Count > 0)
            {
                ThinkingLabel.Text = "运行中";
                LiveOutputLabel.Foreground = ThemeManager.GetColor("ThemeTextSecondary");
                var focus = _session.Tasks.FirstOrDefault(t => t.Status == TodoStatus.InProgress)
                    ?? _session.Tasks.FirstOrDefault(t => t.Status == TodoStatus.Pending);
                if (focus != null)
                {
                    const int maxLen = 120;
                    string t = focus.Content;
                    LiveOutputLabel.Text = t.Length > maxLen ? t[..maxLen] + "…" : t;
                }
                else
                    LiveOutputLabel.Text = "正在收尾…";
            }
            else
            {
                double silentSec = (DateTime.Now - _lastOutputTime).TotalSeconds;
                if (silentSec > 30)
                {
                    ThinkingLabel.Text = "运行中";
                    int silentMin = (int)(silentSec / 60);
                    string silentStr = silentMin > 0 ? $"{silentMin}m{(int)(silentSec % 60)}s" : $"{(int)silentSec}s";
                    LiveOutputLabel.Text = $"⚠  已 {silentStr} 无输出 — 可能正在处理大型任务或已卡住";
                    LiveOutputLabel.Foreground = silentSec > 120
                        ? ThemeManager.GetColor("ThemeError")
                        : ThemeManager.GetColor("ThemeWarning");
                }
                else
                {
                    ThinkingLabel.Text = "运行中";
                    string[] dots = { "正在处理…", "正在处理… .", "正在处理… ..", "正在处理… ..." };
                    LiveOutputLabel.Text = dots[_fi % dots.Length];
                    LiveOutputLabel.Foreground = ThemeManager.GetColor("ThemeTextMuted");
                }
            }

            // Progress bar: fill based on elapsed time (0→full in ~90s, then pulses)
            double elapsed = _sw.Elapsed.TotalSeconds;
            double parentWidth = ProgressBarFill.ActualWidth > 0
                ? ProgressBarFill.ActualWidth
                : (ProgressBarFill.Parent as Border)?.ActualWidth ?? 300;
            double trackWidth = ((ProgressBarFill.Parent as Border)?.ActualWidth ?? 400);
            // Logarithmic fill: fast at start, slows down, never quite reaches 100%
            double pct = 1.0 - Math.Exp(-elapsed / 45.0);
            ProgressBarFill.Width = Math.Max(4, trackWidth * pct);
        }

        /// <summary>Token 用量更新时刷新指示器</summary>
        private void OnTokenUsageUpdated(object? sender, EventArgs e)
        {
            _ = Dispatcher.InvokeAsync(UpdateTokenIndicator);
        }

        /// <summary>更新 Token 圆形进度指示器</summary>
        private void UpdateTokenIndicator()
        {
            double total = _session.InputTokens + _session.OutputTokens;
            double max = _session.MaxTokens > 0 ? _session.MaxTokens : 200_000;
            double pct = Math.Clamp(total / max, 0, 1);

            // 计算弧线终点
            if (pct <= 0)
            {
                TokenProgressArc.Data = Geometry.Parse("M 14,4 A 0,0 0 0 1 14,4");
            }
            else if (pct >= 1)
            {
                // 满圆需要两段弧
                TokenProgressArc.Data = Geometry.Parse("M 14,4 A 10,10 0 1 1 14,24 M 14,24 A 10,10 0 1 1 14,4");
            }
            else
            {
                double angle = pct * 360.0;
                double rad = angle * Math.PI / 180.0;
                double x = 14 + 10 * Math.Sin(rad);
                double y = 14 - 10 * Math.Cos(rad);
                bool largeArc = angle > 180;
                string pathData = $"M 14,4 A 10,10 0 {(largeArc ? "1" : "0")} 1 {x:F3},{y:F3}";
                TokenProgressArc.Data = Geometry.Parse(pathData);
            }

            // 颜色分级：绿 → 黄 → 橙 → 红
            var color = pct switch
            {
                < 0.60 => Color.FromRgb(0x22, 0xC5, 0x5E),   // 绿
                < 0.80 => Color.FromRgb(0xF5, 0x9E, 0x0B),   // 黄
                < 0.95 => Color.FromRgb(0xF9, 0x73, 0x16),   // 橙
                _      => Color.FromRgb(0xEF, 0x44, 0x44),   // 红
            };
            TokenProgressArc.Stroke = new SolidColorBrush(color);
            TokenPercentText.Text = $"{(int)(pct * 100)}%";

            // ToolTip 明细
            TokenIndicatorBorder.ToolTip =
                $"输入: {_session.InputTokens:N0} tokens\n" +
                $"输出: {_session.OutputTokens:N0} tokens\n" +
                $"总计: {total:N0} / {(long)max:N0} tokens\n" +
                $"上下文: {(long)max:N0} tokens";
        }

        // ════════════════════════════════════════════════════════
        //  SESSION STATE & UI HANDLERS
        // ════════════════════════════════════════════════════════

        /// <summary>Build/Plan 模式切换</summary>
        private void ToggleMode_Click(object sender, RoutedEventArgs e)
        {
            _session.Mode = _session.Mode == InteractionMode.Build ? InteractionMode.Plan : InteractionMode.Build;
        }

        /// <summary>权限模式下拉菜单</summary>
        private void OpenPermissionPopup_Click(object sender, RoutedEventArgs e)
        {
            switch (_session.PermissionLevel)
            {
                case RuntimeMode.ApprovalRequired:
                    _session.PermissionLevel = RuntimeMode.AutoAcceptEdits;
                    break;
                case RuntimeMode.AutoAcceptEdits:
                    _session.PermissionLevel = RuntimeMode.FullAccess;
                    break;
                case RuntimeMode.FullAccess:
                    _session.PermissionLevel = RuntimeMode.ApprovalRequired;
                    break;
            }
        }

        /// <summary>Session 属性变更时更新 UI</summary>
        private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SessionState.IsExecuting):
                    SetBusy(_session.IsExecuting);
                    SendButton.Content = _session.SendButtonText;
                    SendButton.ToolTip = _session.IsExecuting ? "停止" : "发送";
                    SendButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_session.SendButtonBackground)!);
                    break;
                case nameof(SessionState.DraftInput):
                    if (InputBox.Text != _session.DraftInput)
                        InputBox.Text = _session.DraftInput;
                    break;
                case nameof(SessionState.Mode):
                    ModeToggleButton.Content = _session.ModeLabel;
                    break;
                case nameof(SessionState.PermissionLevel):
                    PermissionBtn.Content = _session.PermissionLabel;
                    break;
                case nameof(SessionState.HasTasks):
                    ActivityLogHost.Visibility = _session.HasTasks ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        }

        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (InputPlaceholder != null)
                InputPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void InputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (InputPlaceholder != null)
                InputPlaceholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Window chrome ──────────────────────────────────
        private void TitleBar_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void WinMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void WinMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (_maximized)
            {
                Left = _normalLeft;
                Top = _normalTop;
                Width = _normalWidth;
                Height = _normalHeight;
                _maximized = false;
            }
            else
            {
                _normalLeft = Left;
                _normalTop = Top;
                _normalWidth = Width;
                _normalHeight = Height;
                var wa = SystemParameters.WorkArea;
                Left = wa.Left;
                Top = wa.Top;
                Width = wa.Width;
                Height = wa.Height;
                _maximized = true;
            }
        }

        private void WinClose_Click(object sender, RoutedEventArgs e)
        {
            KillJob();
            Close();
        }

        // ═══ 主题切换 ═══
        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var themes = new[] { "Default", "GitHub", "Apple", "Monokai", "Dracula", "WarmPaper" };
            var current = ThemeManager.Instance.CurrentThemeName;
            var nextIndex = (Array.IndexOf(themes, current) + 1) % themes.Length;
            ThemeManager.Instance.SwitchTheme(themes[nextIndex]);
            StatusBar.Text = $"主题切换为: {themes[nextIndex]}";
        }

        public void OnThemeChanged(ThemeChangedEventArgs e)
        {
            InvalidateVisual();
        }

        // ════════════════════════════════════════════════════════
        //  SIDEBAR DUAL-TAB
        // ════════════════════════════════════════════════════════

        /// <summary>切换侧边栏到技能标签</summary>
        private void SidebarTabSkills_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarTabIndex == 0) return;
            _sidebarTabIndex = 0;
            UpdateSidebarTabUI();
            SidebarActions.Children[0].Visibility = Visibility.Visible; // SkillAddBtn
            SidebarActions.Children[1].Visibility = Visibility.Visible; // SkillFolderBtn
            SidebarActions.Children[2].Visibility = Visibility.Visible; // SkillGenerateBtn
            SidebarActions.Children[3].Visibility = Visibility.Visible; // SkillRefreshBtn
            SidebarActions.Children[4].Visibility = Visibility.Collapsed; // ProjectAddBtn
        }

        /// <summary>切换侧边栏到项目标签</summary>
        private void SidebarTabProjects_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarTabIndex == 1) return;
            _sidebarTabIndex = 1;
            UpdateSidebarTabUI();
            SidebarActions.Children[0].Visibility = Visibility.Collapsed; // SkillAddBtn
            SidebarActions.Children[1].Visibility = Visibility.Collapsed; // SkillFolderBtn
            SidebarActions.Children[2].Visibility = Visibility.Collapsed; // SkillGenerateBtn
            SidebarActions.Children[3].Visibility = Visibility.Collapsed; // SkillRefreshBtn
            SidebarActions.Children[4].Visibility = Visibility.Visible;   // ProjectAddBtn
        }

        /// <summary>切换侧边栏到文件标签</summary>
        private void SidebarTabFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarTabIndex == 2) return;
            _sidebarTabIndex = 2;
            UpdateSidebarTabUI();
            foreach (UIElement child in SidebarActions.Children)
                child.Visibility = Visibility.Collapsed;

            // 自动加载当前工作目录
            var dir = _session.WorkingDirectory;
            if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                FilesSidebarCtrl.LoadDirectory(dir);
        }

        /// <summary>切换侧边栏到搜索标签</summary>
        private void SidebarTabSearch_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarTabIndex == 3) return;
            _sidebarTabIndex = 3;
            UpdateSidebarTabUI();
            foreach (UIElement child in SidebarActions.Children)
                child.Visibility = Visibility.Collapsed;
        }

        /// <summary>切换侧边栏到问题标签</summary>
        private void SidebarTabProblems_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarTabIndex == 4) return;
            _sidebarTabIndex = 4;
            UpdateSidebarTabUI();
            foreach (UIElement child in SidebarActions.Children)
                child.Visibility = Visibility.Collapsed;
        }

        /// <summary>更新侧边栏标签按钮的视觉样式</summary>
        private void UpdateSidebarTabUI()
        {
            // Reset all tabs to inactive
            Border[] tabs = { SidebarSkillsTab, SidebarProjectsTab, SidebarFilesTab, SidebarSearchTab, SidebarProblemsTab };
            Grid[] panels = { SkillsPanel, ProjectsPanel, FilesPanel, SearchPanelGrid, ProblemsPanelGrid };
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Background = new SolidColorBrush(Colors.Transparent);
                if (tabs[i].Child is TextBlock tb)
                {
                    tb.FontWeight = FontWeights.Normal;
                    tb.Foreground = H("#636366");
                }
                panels[i].Visibility = i == _sidebarTabIndex ? Visibility.Visible : Visibility.Collapsed;
            }
            // Highlight active tab
            tabs[_sidebarTabIndex].Background = H("#2C2C2E");
            if (tabs[_sidebarTabIndex].Child is TextBlock activeTb)
            {
                activeTb.FontWeight = FontWeights.SemiBold;
                activeTb.Foreground = H("#0A84FF");
            }
        }

        /// <summary>添加项目对话框</summary>
        private void AddProject_Click(object sender, RoutedEventArgs e)
        {
            // Use ThemeManager colors for theme-consistent dialog
            var bgSurface1 = ThemeManager.GetColor("ThemeSurface1");
            var bgSurface2 = ThemeManager.GetColor("ThemeSurface2");
            var bgSurface3 = ThemeManager.GetColor("ThemeSurface3");
            var txtPrimary = ThemeManager.GetColor("ThemeTextPrimary");
            var txtSecondary = ThemeManager.GetColor("ThemeTextSecondary");
            var txtMuted = ThemeManager.GetColor("ThemeTextMuted");
            var accent = ThemeManager.GetColor("ThemeAccent");
            var border1 = ThemeManager.GetColor("ThemeBorder1");

            var win = new Window
            {
                Title = "添加项目",
                Width = 460, Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = bgSurface1,
                Foreground = txtPrimary,
                FontFamily = new FontFamily("Segoe UI"),
            };

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Project name
            var nameLabel = new TextBlock
            {
                Text = "项目名称", Foreground = txtSecondary,
                FontSize = 11, Margin = new Thickness(0, 0, 0, 4),
            };
            Grid.SetRow(nameLabel, 0);
            root.Children.Add(nameLabel);

            var nameBox = new TextBox
            {
                Background = bgSurface2,
                Foreground = txtPrimary,
                BorderBrush = border1,
                BorderThickness = new Thickness(1),
                CaretBrush = accent,
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                FontFamily = new FontFamily("Consolas, Courier New"),
                Height = 34,
            };
            Grid.SetRow(nameBox, 1);
            root.Children.Add(nameBox);

            // Directory
            var dirLabel = new TextBlock
            {
                Text = "工作目录", Foreground = txtSecondary,
                FontSize = 11, Margin = new Thickness(0, 10, 0, 4),
            };
            Grid.SetRow(dirLabel, 2);
            root.Children.Add(dirLabel);

            var dirBox = new TextBox
            {
                Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Background = bgSurface2,
                Foreground = txtPrimary,
                BorderBrush = border1,
                BorderThickness = new Thickness(1),
                CaretBrush = accent,
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                FontFamily = new FontFamily("Consolas, Courier New"),
                Height = 34,
            };
            Grid.SetRow(dirBox, 3);
            root.Children.Add(dirBox);

            // Buttons
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
            };
            Grid.SetRow(btnRow, 5);

            Button MakeBtn2(string label, SolidColorBrush bg, SolidColorBrush fg)
            {
                return new Button
                {
                    Content = label,
                    Padding = new Thickness(16, 7, 16, 7),
                    FontSize = 12,
                    FontFamily = new FontFamily("Segoe UI"),
                    Background = bg,
                    Foreground = fg,
                    BorderBrush = border1,
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
            }

            var cancelBtn = MakeBtn2("取消", bgSurface3, txtMuted);
            cancelBtn.Click += (_, _) => win.DialogResult = false;
            var browseBtn = MakeBtn2("浏览...", bgSurface3, accent);
            browseBtn.Click += (_, _) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择工作目录 — 在文件夹中任意选择一个文件",
                    CheckFileExists = false,
                    FileName = "Select Folder",
                    Filter = "Folder|*.none",
                    InitialDirectory = Directory.Exists(dirBox.Text)
                        ? dirBox.Text
                        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                };
                if (dlg.ShowDialog() == true)
                {
                    string dir = Path.GetDirectoryName(dlg.FileName) ?? dlg.FileName;
                    dirBox.Text = dir;
                    if (string.IsNullOrEmpty(nameBox.Text))
                        nameBox.Text = Path.GetFileName(dir);
                }
            };
            var okBtn = MakeBtn2("添加", new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)), H("#FFFFFF"));
            okBtn.FontWeight = FontWeights.Bold;
            okBtn.Click += (_, _) => win.DialogResult = true;

            btnRow.Children.Add(browseBtn);
            btnRow.Children.Add(new Border { Width = 8 });
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(new Border { Width = 8 });
            btnRow.Children.Add(okBtn);
            root.Children.Add(btnRow);

            win.Content = root;
            win.Loaded += (_, _) => nameBox.Focus();

            if (win.ShowDialog() != true) return;

            string pname = nameBox.Text.Trim();
            string dir = dirBox.Text.Trim();
            if (string.IsNullOrEmpty(pname) || !Directory.Exists(dir))
            {
                SysLine("项目名称或目录无效。", BrYellow);
                return;
            }

            _projectManager.AddProject(pname, dir);
            ProjectSidebarCtrl.RefreshTree();
            StatusBar.Text = $"已添加项目：{pname}";

            // Switch to projects tab
            if (_sidebarTabIndex == 0)
                SidebarTabProjects_Click(null!, null!);
        }

        /// <summary>从项目树中激活一个会话</summary>
        private void Sidebar_SessionActivated(object? sender, SessionItem session)
        {
            var project = _projectManager.Projects
                .FirstOrDefault(p => p.Sessions.Contains(session));
            if (project == null)
            {
                SysLine("无法找到会话所属的项目。", BrYellow);
                return;
            }

            // 检查是否已在标签中打开
            var existingTab = _sessionManager.Tabs
                .FirstOrDefault(t => t.Id == session.TabId);
            if (existingTab != null)
            {
                _sessionManager.ActivateTab(existingTab);
                StatusBar.Text = $"切换到会话：{project.Name}/{session.Name}";
                return;
            }

            // 创建新标签，恢复 claudecode 会话 UUID
            var state = new SessionState();
            state.Name = session.Name;
            state.WorkingDirectory = project.WorkingDirectory;
            state.SessionItemId = session.Id;
            state.ClaudeSessionId = session.ClaudeSessionId;

            // SQLite 中创建记录（向后兼容：已有会话但无 SQLite 记录）
            if (session.ClaudeSessionId != null)
                _conversationStore.CreateConversation(session.Id, session.ClaudeSessionId);

            // 从 SQLite 判断是否已有对话历史，决定用 --session-id 还是 --resume
            bool hasStarted = _conversationStore.HasStarted(session.Id);

            var tab = _sessionManager.CreateTab(session.Name, state);
            tab.HasContinue = hasStarted;

            // 从 SQLite 恢复聊天记录到标签的 TimelineModel
            var messages = _conversationStore.GetMessages(session.Id);
            LoadMessagesToTimeline(tab, messages);
            // 同时渲染到 ChatBubbles（兼容旧的 save/restore 机制）
            foreach (var (role, content, _) in messages)
            {
                AddStoredBubble(tab, role, content);
            }

            session.TabId = tab.Id;
            _projectManager.Save();

            // 为项目目录设置文件权限（如已设置则跳过）
            if (_claudeExe != null)
                WritePermissions(project.WorkingDirectory, _skillsRoot);

            _sessionManager.ActivateTab(tab);
            StatusBar.Text = $"打开会话：{project.Name}/{session.Name}";
            UpdateStatusBarProjectPath();
            UpdateStatusBarGitStatus();
            UpdateStatusBarModel();
        }

        /// <summary>从项目树删除会话 → 关闭关联标签</summary>
        private void Sidebar_SessionDeleted(object? sender, SessionItem session)
        {
            var tab = _sessionManager.Tabs.FirstOrDefault(t => t.Id == session.TabId);
            if (tab != null)
            {
                _sessionManager.CloseTab(tab);
                session.TabId = null;
                StatusBar.Text = $"已关闭会话关联的标签";
            }

            // 清理 SQLite 中的对话记录
            try { _conversationStore.DeleteConversation(session.Id); } catch { }
        }

        /// <summary>从项目树重命名会话 → 同步到标签标题</summary>
        private void Sidebar_SessionRenamed(object? sender, SessionItem session)
        {
            var tab = _sessionManager.Tabs.FirstOrDefault(t => t.Id == session.TabId);
            if (tab != null)
                tab.Name = session.Name;
        }

        private void NewTask_Click(object sender, RoutedEventArgs e)
        {
            // Launch a fresh instance of this app
            try
            {
                string? exePath = Environment.ProcessPath;
                if (File.Exists(exePath))
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                else
                {
                    // Fallback: re-launch via dotnet or the process itself
                    string? proc = Process.GetCurrentProcess().MainModule?.FileName;
                    if (proc != null)
                        Process.Start(new ProcessStartInfo(proc) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { SysLine("无法打开新窗口：" + ex.Message, BrRed); }
        }

        // ════════════════════════════════════════════════════════
        //  STATUS BAR
        // ════════════════════════════════════════════════════════

        private void UpdateStatusBarProjectPath()
        {
            var dir = _session.WorkingDirectory;
            ProjectPathLabel.Text = !string.IsNullOrEmpty(dir) ? Path.GetFileName(dir) : "";
        }

        private void UpdateStatusBarModel()
        {
            ModelLabel.Text = _session.SelectedModel ?? "";
            ModeLabel.Text = _session.Mode == InteractionMode.Build ? "Build" : "Plan";
        }

        private async void UpdateStatusBarGitStatus()
        {
            var dir = _session.WorkingDirectory;
            if (string.IsNullOrEmpty(dir) || !GitService.IsGitRepository(dir))
            {
                GitBranchLabel.Text = "";
                GitChangesLabel.Text = "";
                return;
            }
            try
            {
                var branch = await Task.Run(() => GitService.GetCurrentBranch(dir));
                var (staged, unstaged) = await Task.Run(() => GitService.GetStatusCount(dir));
                GitBranchLabel.Text = branch;
                if (staged > 0 && unstaged > 0) GitChangesLabel.Text = $"{staged}/{unstaged}";
                else if (staged > 0) GitChangesLabel.Text = $"{staged}↑";
                else if (unstaged > 0) GitChangesLabel.Text = $"{unstaged}✗";
                else GitChangesLabel.Text = "";
            }
            catch { GitBranchLabel.Text = ""; GitChangesLabel.Text = ""; }
        }

        // ════════════════════════════════════════════════════════
        //  TERMINAL PANEL
        // ════════════════════════════════════════════════════════

        private void TerminalClose_Click(object sender, RoutedEventArgs e)
        {
            TerminalPanel.Visibility = Visibility.Collapsed;
        }

        private void ToggleTerminal_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            TerminalPanel.Visibility = TerminalPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            if (TerminalPanel.Visibility == Visibility.Visible)
                TerminalContent.ScrollToEnd();
        }

        public void AppendToTerminal(string text)
        {
            if (TerminalPanel.Visibility != Visibility.Visible)
                TerminalPanel.Visibility = Visibility.Visible;
            TerminalText.Text += text;
            TerminalContent.ScrollToEnd();
        }

        // ════════════════════════════════════════════════════════
        //  CODE VIEWER
        // ════════════════════════════════════════════════════════

        private void CodeViewer_CloseRequested(object sender, EventArgs e)
        {
            CodeViewerPanel.Visibility = Visibility.Collapsed;
        }

        private void OpenCodeViewer(string filePath, int? lineNumber = null)
        {
            if (!File.Exists(filePath)) return;
            CodeViewerPanel.Visibility = Visibility.Visible;
            CodeViewerCtrl.LoadFile(filePath, lineNumber);
        }
    }
}
