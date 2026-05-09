using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeCodeGUI;

/// <summary>技能面板：加载、搜索、创建、编辑、删除技能文件</summary>
public partial class MainWindow : Window
{
    // ══════════════════════════════════════════════════════
    //  SKILLS SIDEBAR  — flat .md file model
    //  ~/.claude/skills/[group/]skill-name.md
    //  Folders are optional groups only.
    // ══════════════════════════════════════════════════════

    private string _skillSearchText = "";

    private static (string name, string description) ParseFrontmatter(string mdPath)
    {
        try
        {
            var lines = File.ReadAllLines(mdPath);
            if (lines.Length < 2 || lines[0].Trim() != "---") return ("", "");
            string name = "", desc = ""; bool inDesc = false;
            var dp = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---") break;
                if (lines[i].StartsWith("name:"))
                    name = lines[i]["name:".Length..].Trim().Trim('"');
                else if (lines[i].StartsWith("description:"))
                {
                    string d = lines[i]["description:".Length..].Trim().Trim('"');
                    if (d == ">") inDesc = true; else desc = d;
                }
                else if (inDesc && (lines[i].StartsWith("  ") || lines[i].StartsWith("\t")))
                    dp.Add(lines[i].Trim());
                else if (inDesc) inDesc = false;
            }
            if (dp.Count > 0) desc = string.Join(" ", dp);
            return (name, desc);
        }
        catch { return ("", ""); }
    }

    private static string SkillCmd(string mdPath)
    {
        var (n, _) = ParseFrontmatter(mdPath);
        return string.IsNullOrEmpty(n) ? Path.GetFileNameWithoutExtension(mdPath) : n;
    }

    private void LoadSkillsTree()
    {
        var items = new List<SkillItem>();
        int total = 0;

        CollectSkills(_skillsRoot, "", items, ref total);

        // Project-level skills
        if (!string.IsNullOrEmpty(_workDir))
        {
            string proj = Path.Combine(_workDir, ".claude", "skills");
            if (Directory.Exists(proj) && proj != _skillsRoot)
                CollectSkills(proj, "project/", items, ref total);
        }

        // Apply search filter
        string q = _skillSearchText.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
            items = items.Where(i =>
                i.CmdDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.DescSnippet.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.GroupTag.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        SkillsList.ItemsSource = items;
        SkillsCountLabel.Text = total > 0 ? $"  {total}" : "";
    }

    private static void CollectSkills(string dir, string groupPrefix,
        List<SkillItem> items, ref int total)
    {
        if (!Directory.Exists(dir))
            try { Directory.CreateDirectory(dir); } catch { return; }

        // Recurse into sub-folders first
        foreach (var sub in Directory.GetDirectories(dir).OrderBy(d => d))
        {
            string groupName = groupPrefix + Path.GetFileName(sub);
            CollectSkills(sub, groupName + "/", items, ref total);
        }

        // .md files in this dir
        foreach (var f in Directory.GetFiles(dir, "*.md").OrderBy(x => x))
        {
            var (_, desc) = ParseFrontmatter(f);
            string cmd = SkillCmd(f);
            string snip = desc.Length > 55 ? desc[..55] + "…" : desc;
            int dot = snip.IndexOf(". ", StringComparison.Ordinal);
            if (dot > 6 && dot < 50) snip = snip[..(dot + 1)];

            items.Add(new SkillItem
            {
                FilePath   = f,
                CmdDisplay = "/" + cmd,
                DescSnippet = snip,
                GroupTag    = groupPrefix.TrimEnd('/')
            });
            total++;
        }
    }

    // ── Search ────────────────────────────────────────────
    private void SkillSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _skillSearchText = SkillSearchBox.Text;
        LoadSkillsTree();
    }

    // ── Card events ───────────────────────────────────────
    private void SkillCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SkillItem item)
            SelectSkillItem(item);
    }

    private void SkillContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && menu.PlacementTarget is FrameworkElement fe
            && fe.DataContext is SkillItem item)
        {
            _selectedSkillPath = item.FilePath;
            _selectedSkillItem = item;
            SkillPreviewCmd.Text = item.CmdDisplay;
            SkillPreviewCmd.Foreground = H("#0A84FF");
            SkillPreviewDesc.Text = item.DescSnippet.Length > 0 ? item.DescSnippet : "(无描述)";
            SkillPreviewDesc.Foreground = H("#636366");
            SkillInsertBtn.IsEnabled = true;
        }
    }

    private void SkillCardInsert_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SkillItem item)
        {
            SelectSkillItem(item);
            InsertCommand(item.FilePath);
            e.Handled = true;
        }
    }

    private void SelectSkillItem(SkillItem item)
    {
        _selectedSkillItem = item;
        _selectedSkillPath = item.FilePath;
        SkillPreviewCmd.Text = item.CmdDisplay;
        SkillPreviewCmd.Foreground = H("#0A84FF");
        SkillPreviewDesc.Text = item.DescSnippet.Length > 0 ? item.DescSnippet : "(无描述)";
        SkillPreviewDesc.Foreground = H("#636366");
        SkillInsertBtn.IsEnabled = true;
    }

    private void ClearPreview()
    {
        _selectedSkillItem = null;
        SkillPreviewCmd.Text = "未选中技能";
        SkillPreviewCmd.Foreground = H("#48484A");
        SkillPreviewDesc.Text = "";
        SkillInsertBtn.IsEnabled = false;
    }

    // ── Keep TreeView stubs so old event names still compile ──
    private void SkillsTree_SelectedItemChanged(object s, RoutedPropertyChangedEventArgs<object> e) { }
    private void SkillsTree_DoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e) { }

    // ── Actions ───────────────────────────────────────────
    private void SkillUse_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSkillPath != null && File.Exists(_selectedSkillPath))
            InsertCommand(_selectedSkillPath);
    }

    private void InsertCommand(string mdPath)
    {
        string insert = "/" + SkillCmd(mdPath);
        InputBox.Text = string.IsNullOrEmpty(InputBox.Text)
            ? insert + " "
            : InputBox.Text.TrimEnd() + " " + insert + " ";
        InputBox.CaretIndex = InputBox.Text.Length;
        InputBox.Focus();
        StatusBar.Text = "已插入 " + insert + "  —  请在后面添加参数";
    }

    private void SkillOpenEditor_Click(object sender, RoutedEventArgs e)
    {
        string? target = _selectedSkillPath;
        if (target == null || !File.Exists(target)) return;
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { SysLine("无法打开：" + ex.Message, BrRed); }
    }

    private void SkillNewSkill_Click(object sender, RoutedEventArgs e)
    {
        string parentDir = GetSelectedDirectory();
        string mdPath = GetUniquePath(parentDir, "new-skill", ".md");
        string skillName = Path.GetFileNameWithoutExtension(mdPath);
        try
        {
            string template =
                "---\n" +
                "name: " + skillName + "\n" +
                "description: >\n" +
                "  Describe what this skill does and when to use it.\n" +
                "---\n\n" +
                "# " + skillName + "\n\n" +
                "## What to do\n" +
                "描述 Claude 应遵循的步骤。\n\n" +
                "## 示例\n" +
                "用户：[示例请求]\n" +
                "操作：[Claude 执行的操作]\n";
            File.WriteAllText(mdPath, template);
            LoadSkillsTree();
            StatusBar.Text = "已创建：" + skillName + ".md  —  请编辑内容";
            Process.Start(new ProcessStartInfo(mdPath) { UseShellExecute = true });
        }
        catch (Exception ex) { SysLine("无法创建技能：" + ex.Message, BrRed); }
    }

    private void SkillNewFolder_Click(object sender, RoutedEventArgs e)
    {
        string parentDir = GetSelectedDirectory();
        string newDir = GetUniquePath(parentDir, "group", "");
        try
        {
            Directory.CreateDirectory(newDir);
            LoadSkillsTree();
            StatusBar.Text = "已创建分组：" + Path.GetFileName(newDir);
        }
        catch (Exception ex) { SysLine("无法创建文件夹：" + ex.Message, BrRed); }
    }

    private void SkillRename_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSkillPath == null || !File.Exists(_selectedSkillPath)) return;

        var (curName, curDesc) = ParseFrontmatter(_selectedSkillPath);
        if (string.IsNullOrEmpty(curName))
            curName = Path.GetFileNameWithoutExtension(_selectedSkillPath);

        // ── Build a small popup window ──
        var win = new Window
        {
            Title          = "编辑技能",
            Width          = 440,
            Height         = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner          = this,
            ResizeMode     = ResizeMode.NoResize,
            Background     = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
            FontFamily     = new FontFamily("Segoe UI"),
        };

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Name label + box
        root.Children.Add(Label(0, "技能名称（用作 /command）"));
        var nameBox = InputBox2(curName);
        Grid.SetRow(nameBox, 1);
        root.Children.Add(nameBox);

        // Description label + box
        root.Children.Add(Label(2, "描述（何时使用该技能）"));
        var descBox = InputBox2(curDesc);
        descBox.Height = 60;
        descBox.AcceptsReturn = true;
        descBox.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(descBox, 3);
        root.Children.Add(descBox);

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(btnRow, 5);

        var cancelBtn = MakeBtn("取消", "#2C2C2E", "#98989D");
        cancelBtn.Click += (_, _) => win.DialogResult = false;
        var saveBtn   = MakeBtn("保存", "#0A2540", "#0A84FF");
        saveBtn.FontWeight = FontWeights.Bold;
        saveBtn.Click += (_, _) => win.DialogResult = true;

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(new Border { Width = 8 });
        btnRow.Children.Add(saveBtn);
        root.Children.Add(btnRow);

        win.Content = root;
        win.Loaded += (_, _) => nameBox.Focus();

        if (win.ShowDialog() != true) return;

        // ── Apply changes ──
        string newName = nameBox.Text.Trim()
            .Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
        string newDesc = descBox.Text.Trim();

        if (string.IsNullOrEmpty(newName)) return;

        try
        {
            // Rewrite frontmatter in the file
            string[] fileLines = File.ReadAllLines(_selectedSkillPath);
            var output = new List<string>();
            bool inFm = false; bool nameWritten = false; bool descWritten = false;

            for (int i = 0; i < fileLines.Length; i++)
            {
                if (i == 0 && fileLines[i].Trim() == "---") { inFm = true; output.Add(fileLines[i]); continue; }
                if (inFm && fileLines[i].Trim() == "---")
                {
                    // Inject any missing fields before closing ---
                    if (!nameWritten) output.Add("name: " + newName);
                    if (!descWritten && !string.IsNullOrEmpty(newDesc))
                    { output.Add("description: >"); output.Add("  " + newDesc); }
                    inFm = false; output.Add(fileLines[i]); continue;
                }
                if (inFm && fileLines[i].StartsWith("name:"))
                { output.Add("name: " + newName); nameWritten = true; continue; }
                if (inFm && fileLines[i].StartsWith("description:"))
                {
                    // Skip old description lines (may be multi-line)
                    descWritten = true;
                    if (!string.IsNullOrEmpty(newDesc))
                    { output.Add("description: >"); output.Add("  " + newDesc); }
                    // Skip continuation indent lines
                    while (i + 1 < fileLines.Length &&
                           (fileLines[i + 1].StartsWith("  ") || fileLines[i + 1].StartsWith("\t")))
                        i++;
                    continue;
                }
                output.Add(fileLines[i]);
            }

            // If no frontmatter existed, prepend it
            if (!inFm && output.Count > 0 && output[0].Trim() != "---")
            {
                var header = new List<string> { "---", "name: " + newName };
                if (!string.IsNullOrEmpty(newDesc)) { header.Add("description: >"); header.Add("  " + newDesc); }
                header.Add("---");
                output.InsertRange(0, header);
            }

            File.WriteAllLines(_selectedSkillPath, output);

            // Rename file if name changed
            string dir = Path.GetDirectoryName(_selectedSkillPath)!;
            string newFile = Path.Combine(dir, newName + ".md");
            if (!string.Equals(_selectedSkillPath, newFile, StringComparison.OrdinalIgnoreCase)
                && !File.Exists(newFile))
            {
                File.Move(_selectedSkillPath, newFile);
                _selectedSkillPath = newFile;
            }

            LoadSkillsTree();
            StatusBar.Text = "已保存：/" + newName;
        }
        catch (Exception ex) { SysLine("重命名失败：" + ex.Message, BrRed); }
    }

    // ── Popup helpers ─────────────────────────────────────
    private static UIElement Label(int row, string text)
    {
        var tb = new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x9D)),
            FontSize = 11, Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 4)
        };
        Grid.SetRow(tb, row);
        return tb;
    }

    private static TextBox InputBox2(string text)
    {
        return new TextBox
        {
            Text = text,
            Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xF0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
            BorderThickness = new Thickness(1),
            CaretBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            FontFamily = new FontFamily("Consolas, Courier New"),
            Height = 34,
        };
    }

    private static Button MakeBtn(string label, string bg, string fg)
    {
        return new Button
        {
            Content = label,
            Padding = new Thickness(16, 7, 16, 7),
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI"),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
    }

    private void SkillDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSkillPath == null) return;
        string name = Path.GetFileName(_selectedSkillPath);
        bool isDir = Directory.Exists(_selectedSkillPath);
        string msg = isDir
            ? "Delete folder [" + name + "] and all its contents?"
            : "Delete [" + name + "]?";
        if (MessageBox.Show(msg, "Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            if (isDir)
            {
                foreach (var f in Directory.GetFiles(_selectedSkillPath, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(_selectedSkillPath, recursive: true);
            }
            else if (File.Exists(_selectedSkillPath))
            {
                File.SetAttributes(_selectedSkillPath, FileAttributes.Normal);
                File.Delete(_selectedSkillPath);
            }
            _selectedSkillPath = null;
            ClearPreview();
            LoadSkillsTree();
            StatusBar.Text = "已删除：" + name;
        }
        catch (Exception ex) { SysLine("无法删除：" + ex.Message, BrRed); }
    }

    private async void SkillGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_claudeExe == null) { SysLine("请先启动会话。", BrYellow); return; }

        AddDivider();
        var b = Bubble(BrBgAst, BrBdrA);
        var sp = (StackPanel)b.Child;
        sp.Children.Add(new TextBlock
        {
            Text = "✨  生成技能",
            Foreground = H("#0A84FF"), FontSize = 13, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 0, 0, 8)
        });
        foreach (var (t, c) in new (string, SolidColorBrush)[]
        {
            ("在输入框中编辑文字，描述你想要的功能，", BrDim),
            ("然后按 Enter。Claude 会创建一个 .md 技能文件。", BrDim),
            ("", BrMuted),
            ("示例：", BrDim),
            ("  将文件夹中所有 .ogg 文件用 ffmpeg 转换为 .wav", H("#0A84FF")),
        })
            sp.Children.Add(new TextBlock
            {
                Text = t, Foreground = c, FontSize = 12,
                FontFamily = new FontFamily("Consolas, Courier New"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1)
            });
        ChatPanel.Children.Add(b);
        OutputScroller.ScrollToBottom();

        InputBox.Text =
            "请为我创建一个技能 .md 文件。该技能的功能：[在此描述]。 " +
            "保存为单个 .md 文件到：" + _skillsRoot + "。 " +
            "以 ---  name: 技能名称  description: > ...  --- 开头，后面是 markdown 指令。";
        InputBox.SelectAll();
        InputBox.Focus();
        await Task.CompletedTask;
    }

    private void SkillRefresh_Click(object sender, RoutedEventArgs e) => LoadSkillsTree();

    private string GetSelectedDirectory()
    {
        if (_selectedSkillPath != null)
        {
            if (Directory.Exists(_selectedSkillPath)) return _selectedSkillPath;
            if (File.Exists(_selectedSkillPath))
                return Path.GetDirectoryName(_selectedSkillPath) ?? _skillsRoot;
        }
        return _skillsRoot;
    }

    private static string GetUniquePath(string dir, string baseName, string ext)
    {
        string candidate = Path.Combine(dir, baseName + ext);
        int i = 1;
        while (File.Exists(candidate) || Directory.Exists(candidate))
            candidate = Path.Combine(dir, baseName + "-" + i++ + ext);
        return candidate;
    }
}
