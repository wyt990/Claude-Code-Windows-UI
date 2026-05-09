using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeCodeGUI;

/// <summary>项目标签页 — 以树形展示项目→会话列表</summary>
public partial class ProjectSidebar : UserControl
{
    private ProjectManager? _manager;

    public ProjectSidebar()
    {
        InitializeComponent();
    }

    /// <summary>设置项目数据源</summary>
    public void SetProjects(ProjectManager manager)
    {
        _manager = manager;
        ProjectTree.ItemsSource = manager.Projects;
    }

    /// <summary>刷新树</summary>
    public void RefreshTree()
    {
        var source = ProjectTree.ItemsSource;
        ProjectTree.ItemsSource = null;
        ProjectTree.ItemsSource = source;
    }

    // ── 事件 ───────────────────────────────────────────

    /// <summary>用户激活了一个会话（双击或菜单打开）</summary>
    public event EventHandler<SessionItem>? SessionActivated;

    /// <summary>会话被删除（需同步关闭关联的标签）</summary>
    public event EventHandler<SessionItem>? SessionDeleted;

    /// <summary>会话被重命名（需同步更新关联标签的标题）</summary>
    public event EventHandler<SessionItem>? SessionRenamed;

    // ── 事件处理 ────────────────────────────────────────

    private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTree.SelectedItem is SessionItem item)
            SessionActivated?.Invoke(this, item);
    }

    // ═══════════════════════════════════════════════════
    //  项目右键菜单
    // ═══════════════════════════════════════════════════

    private void AddSession_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        var project = GetItemFromMenu<ProjectItem>(sender);
        if (project == null) return;

        int count = project.Sessions.Count + 1;
        string claudeSessionId = Guid.NewGuid().ToString("D");
        _manager.AddSession(project, $"会话 {count}", claudeSessionId);
        RefreshTree();
    }

    private void RenameProject_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        var project = GetItemFromMenu<ProjectItem>(sender);
        if (project == null) return;

        string? newName = PromptDialog("重命名项目", "项目名称：", project.Name);
        if (!string.IsNullOrEmpty(newName))
        {
            project.Name = newName;
            _manager.Save();
            RefreshTree();
        }
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        var project = GetItemFromMenu<ProjectItem>(sender);
        if (project == null) return;

        var result = MessageBox.Show(
            $"确定要从列表中移除项目「{project.Name}」吗？\n（不会删除磁盘上的文件。）",
            "删除项目", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _manager.RemoveProject(project);
            RefreshTree();
        }
    }

    // ═══════════════════════════════════════════════════
    //  会话右键菜单
    // ═══════════════════════════════════════════════════

    private void OpenSession_Click(object sender, RoutedEventArgs e)
    {
        var session = GetItemFromMenu<SessionItem>(sender);
        if (session != null)
            SessionActivated?.Invoke(this, session);
    }

    private void RenameSession_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        var session = GetItemFromMenu<SessionItem>(sender);
        if (session == null) return;

        var project = FindParentProject(session);
        if (project == null) return;

        string? newName = PromptDialog("重命名会话", "会话名称：", session.Name);
        if (!string.IsNullOrEmpty(newName))
        {
            _manager.RenameSession(project, session, newName);
            RefreshTree();
            SessionRenamed?.Invoke(this, session);
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        var session = GetItemFromMenu<SessionItem>(sender);
        if (session == null) return;

        var project = FindParentProject(session);
        if (project == null) return;

        var result = MessageBox.Show(
            $"确定要删除会话「{session.Name}」吗？",
            "删除会话", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _manager.RemoveSession(project, session);
            RefreshTree();
            SessionDeleted?.Invoke(this, session);
        }
    }

    // ═══════════════════════════════════════════════════
    //  辅助方法
    // ═══════════════════════════════════════════════════

    /// <summary>从 MenuItem 回溯获取 DataContext 并转型</summary>
    private static T? GetItemFromMenu<T>(object sender) where T : class
    {
        if (sender is MenuItem mi)
        {
            if (mi.DataContext is T t) return t;
            if (mi.Parent is ContextMenu ctx && ctx.DataContext is T t2) return t2;
        }
        return null;
    }

    /// <summary>查找会话所属的项目</summary>
    private ProjectItem? FindParentProject(SessionItem session)
    {
        if (_manager == null) return null;
        foreach (ProjectItem p in _manager.Projects)
            if (p.Sessions.Contains(session))
                return p;
        return null;
    }

    /// <summary>弹出简单的输入对话框</summary>
    private static string? PromptDialog(string title, string label, string defaultValue)
    {
        var inputBox = new TextBox
        {
            Text = defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xF0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            FontFamily = new FontFamily("Consolas, Courier New"),
            Height = 34,
        };

        var popup = new Window
        {
            Title = title,
            Width = 340, Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1C, 0x1C, 0x1E)),
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = label,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x9D)),
                            FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                            Margin = new Thickness(0, 0, 0, 8),
                        },
                        inputBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 10, 0, 0),
                            Children =
                            {
                                new Button
                                {
                                    Content = "取消",
                                    Padding = new Thickness(14, 5, 14, 5),
                                    FontSize = 12, FontFamily = new FontFamily("Segoe UI"),
                                    Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                                    Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x9D)),
                                    BorderThickness = new Thickness(1),
                                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                                    Cursor = Cursors.Hand,
                                    Margin = new Thickness(0, 0, 8, 0),
                                },
                                new Button
                                {
                                    Content = "确定",
                                    Padding = new Thickness(14, 5, 14, 5),
                                    FontSize = 12, FontFamily = new FontFamily("Segoe UI"),
                                    Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)),
                                    Foreground = new SolidColorBrush(Colors.White),
                                    FontWeight = FontWeights.Bold,
                                    BorderThickness = new Thickness(0),
                                    Cursor = Cursors.Hand,
                                },
                            }
                        },
                    }
                },
            },
        };

        // Wire up buttons
        var sp = (StackPanel)((Border)popup.Content).Child;
        var btnCancel = (Button)((StackPanel)sp.Children[2]).Children[0];
        var btnOk = (Button)((StackPanel)sp.Children[2]).Children[1];

        btnCancel.Click += (_, _) => popup.DialogResult = false;
        btnOk.Click += (_, _) => popup.DialogResult = true;
        inputBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter) popup.DialogResult = true;
            if (ke.Key == Key.Escape) popup.DialogResult = false;
        };

        popup.Loaded += (_, _) => { inputBox.SelectAll(); inputBox.Focus(); };

        if (popup.ShowDialog() != true) return null;
        string result = inputBox.Text.Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    // ── 公共方法（供 MainWindow 调用） ──────────────────

    /// <summary>获取当前选中的项目（可能为 null）</summary>
    public ProjectItem? GetSelectedProject()
    {
        return ProjectTree.SelectedItem as ProjectItem;
    }

    /// <summary>获取当前选中的会话（可能为 null）</summary>
    public SessionItem? GetSelectedSession()
    {
        return ProjectTree.SelectedItem as SessionItem;
    }

    /// <summary>获取当前选中的项目（无论选中的是项目还是会话，都返回所属项目）</summary>
    public ProjectItem? GetParentProject()
    {
        var item = ProjectTree.SelectedItem;
        if (item is ProjectItem project) return project;
        if (item is SessionItem session)
        {
            foreach (ProjectItem p in ProjectTree.ItemsSource)
                if (p.Sessions.Contains(session))
                    return p;
        }
        return null;
    }

    private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { }
}
