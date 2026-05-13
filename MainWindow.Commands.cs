using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeCodeGUI;

/// <summary>命令操作：模型加载、浏览目录、启动会话、保存技能、图片附件</summary>
public partial class MainWindow : Window
{
    /// <summary>刷新模型列表</summary>
    private async void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        if (_claudeExe == null)
        {
            SysLine("[刷新模型] _claudeExe 为 null — 请先选择目录启动会话", BrRed);
            StatusBar.Text = "请先启动会话。";
            return;
        }

        SysLine($"[刷新模型] 开始刷新，_claudeExe={_claudeExe}", BrDim);
        StatusBar.Text = "正在刷新模型列表...";

        string resolvedExe;
        bool useCmdExe;

        if (_claudeExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            resolvedExe = _claudeExe;
            useCmdExe = false;
            SysLine($"[刷新模型] 检测到 .exe，直接使用", BrDim);
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string exePath = Path.Combine(localAppData, "claude-code-local", "claudecode.exe");
            if (File.Exists(exePath))
            {
                resolvedExe = exePath;
                useCmdExe = false;
                SysLine($"[刷新模型] 从 .cmd 解析出 .exe: {exePath}", BrDim);
            }
            else
            {
                resolvedExe = _claudeExe;
                useCmdExe = true;
                SysLine($"[刷新模型] 未找到 .exe，将用 cmd.exe 执行 .cmd: {resolvedExe}", BrYellow);
            }
        }

        string fileName, arguments;
        if (useCmdExe)
        {
            fileName = "cmd.exe";
            arguments = $"/c \"{resolvedExe}\" --list-models --json";
        }
        else
        {
            fileName = resolvedExe;
            arguments = "--list-models --json";
        }

        SysLine($"[刷新模型] 启动进程：fileName={fileName}, arguments={arguments}", BrDim);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = _workDir ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.Environment["NO_COLOR"] = "1";
            startInfo.Environment["TERM"] = "dumb";
            startInfo.Environment["FORCE_COLOR"] = "0";
            if (!useCmdExe)
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                startInfo.Environment["CLAUDE_CODE_INSTALL_PREFIX"] = Path.Combine(localAppData, "claude-code-local");
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                SysLine($"[刷新模型] Process.Start 返回 null — 无法启动进程", BrRed);
                StatusBar.Text = "无法启动 claudecode 进程。";
                return;
            }

            SysLine($"[刷新模型] 进程已启动，PID={process.Id}，等待输出...", BrDim);

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            SysLine($"[刷新模型] 进程退出，ExitCode={process.ExitCode}, stdout={output.Length} 字符", BrDim);
            if (!string.IsNullOrEmpty(error))
                SysLine($"[刷新模型] stderr={error}", BrYellow);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var response = JsonSerializer.Deserialize<ModelListResponse>(output);
                if (response != null)
                {
                    var allModelNames = new HashSet<string>();

                    foreach (var model in response.CustomModels)
                    {
                        if (!string.IsNullOrEmpty(model.Id))
                            allModelNames.Add(model.Id);
                    }

                    foreach (var provider in response.OpenAICompat.Providers)
                    {
                        foreach (var model in provider.Models)
                        {
                            if (!string.IsNullOrEmpty(model.OriginalName))
                                allModelNames.Add(model.OriginalName);
                        }
                    }

                    foreach (var model in response.ZenFreeModels.Models)
                    {
                        if (!string.IsNullOrEmpty(model.RoutedValue))
                            allModelNames.Add(model.RoutedValue);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        _allModels = allModelNames.OrderBy(name => name).ToList();
                        ModelComboBox.ItemsSource = _allModels;
                        // 同步到所有标签的 AvailableModels
                        foreach (var t in _sessionManager.Tabs)
                            t.State.AvailableModels = _allModels;
                        // 选中当前标签的模型
                        if (_allModels.Contains(_session.SelectedModel))
                            ModelComboBox.SelectedItem = _session.SelectedModel;
                        else if (_allModels.Count > 0)
                        {
                            _session.SelectedModel = _allModels[0];
                            ModelComboBox.SelectedItem = _session.SelectedModel;
                        }
                        SysLine($"[刷新模型] 成功加载 {allModelNames.Count} 个模型：{string.Join(", ", allModelNames.Take(5))}{(allModelNames.Count > 5 ? "..." : "")}", BrGreen);
                        StatusBar.Text = $"已加载 {allModelNames.Count} 个模型";
                    });
                }
                else
                {
                    SysLine($"[刷新模型] JSON 反序列化为 null", BrRed);
                    SysLine($"[刷新模型] JSON 片段：{output[..Math.Min(300, output.Length)]}", BrYellow);
                }
            }
            else
            {
                SysLine($"[刷新模型] 失败 — ExitCode={process.ExitCode}, stdout='{output.Trim()}', stderr='{error.Trim()}'", BrRed);
                StatusBar.Text = "获取模型列表失败，请检查 claudecode 安装。";
            }
        }
        catch (Exception ex)
        {
            SysLine($"[刷新模型] 异常：{ex.GetType().Name}: {ex.Message}", BrRed);
            StatusBar.Text = $"错误：{ex.Message}";
        }
    }

    // ── Browse & Session ──────────────────────────────────
    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select working directory — pick any file inside it",
            CheckFileExists = false,
            FileName = "Select Folder",
            Filter = "Folder|*.none",
            InitialDirectory = Directory.Exists(WorkingDirBox.Text)
                ? WorkingDirBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dlg.ShowDialog() == true)
        {
            WorkingDirBox.Text = Path.GetDirectoryName(dlg.FileName) ?? dlg.FileName;
            DoStartSession();
        }
    }

    private void DoStartSession()
    {
        string dir = WorkingDirBox.Text.Trim();
        if (!Directory.Exists(dir))
        { SysLine($"目录不存在: {dir}", BrYellow); Debug.WriteLine($"[DoStartSession] 目录不存在: {dir}"); return; }

        _workDir = dir;
        _claudeExe = FindClaude();
        Debug.WriteLine($"[DoStartSession] _claudeExe={_claudeExe ?? "(null)"}");
        AddDivider();

        if (_claudeExe == null)
        {
            SysLine("无法找到 claudecode 程序.", BrRed);
            foreach (var p in Candidates()) SysLine($"  {p}", BrMuted);
            SysLine("请确保 claudecode 已安装且在 PATH 中。", BrYellow);
            SetReady(false);
        }
        else
        {
            SysLine($"会话就绪  ·  {_claudeExe}", BrGreen);
            SysLine($"工作目录    ·  {_workDir}", BrDim);
            bool permOk = WritePermissions(_workDir, _skillsRoot);
            SysLine(permOk
                ? "权限    ·  已授予 (settings.json 已更新)"
                : "权限    ·  已设置或跳过", BrDim);
            SetReady(true);
            string projSkills = Path.Combine(_workDir, ".claude", "skills");
            if (Directory.Exists(projSkills))
                SysLine($"项目技能 ·  {projSkills}", BrDim);
            LoadSkillsTree();
            RefreshModels_Click(null!, null!);
        }
    }

    private void StopSession_Click(object sender, RoutedEventArgs e)
    {
        KillJob(); SetBusy(false);
        SysLine("已停止。", BrYellow);
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        ChatPanel.Children.Clear(); ShowWelcome();
        SaveSkillBtn.IsEnabled = false;

        // 同时清空当前标签的气泡缓存
        if (_lastActiveTab != null)
        {
            _lastActiveTab.ChatBubbles.Clear();
            _lastActiveTab.SaveChatBubbles(ChatPanel.Children); // 保存欢迎气泡
        }
    }

    // ── Permissions ───────────────────────────────────────
    private static bool WritePermissions(string workDir, string skillsDir)
    {
        try
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string settingsDir = System.IO.Path.Combine(home, ".claude");
            string settingsPath = System.IO.Path.Combine(settingsDir, "settings.json");
            System.IO.Directory.CreateDirectory(settingsDir);

            var newAllows = new List<string>
            {
                workDir.TrimEnd('\\') + "\\*",
                skillsDir.TrimEnd('\\') + "\\*",
            };

            string existing = System.IO.File.Exists(settingsPath)
                ? System.IO.File.ReadAllText(settingsPath).Trim()
                : "{}";

            var existingAllows = new List<string>();
            var mAllow = System.Text.RegularExpressions.Regex.Match(
                existing, "\"allow\"\\s*:\\s*\\[(.*?)\\]",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (mAllow.Success)
            {
                foreach (System.Text.RegularExpressions.Match m in
                    System.Text.RegularExpressions.Regex.Matches(
                        mAllow.Groups[1].Value, "\"([^\"]+)\""))
                    existingAllows.Add(m.Groups[1].Value);
            }

            bool changed = false;
            foreach (var a in newAllows)
                if (!existingAllows.Contains(a))
                { existingAllows.Add(a); changed = true; }

            if (!changed) return false;

            var lines = existingAllows.Select(a => "      \"" + a + "\"");
            string allowBlock = string.Join(",\n", lines);
            string newJson =
                "{\n" +
                "  \"permissions\": {\n" +
                "    \"allow\": [\n" +
                allowBlock + "\n" +
                "    ]\n" +
                "  }\n" +
                "}";

            System.IO.File.WriteAllText(settingsPath, newJson);
            return true;
        }
        catch { return false; }
    }

    // ── Save as Skill ─────────────────────────────────────
    private async void SaveAsSkill_Click(object sender, RoutedEventArgs e)
    {
        if (_claudeExe == null || !_ready)
        { SysLine("请先启动会话。", BrYellow); return; }

        var win = new Window
        {
            Title = "将对话保存为技能",
            Width = 420, Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
        };
        var root = new StackPanel { Margin = new Thickness(20) };

        root.Children.Add(new TextBlock
        {
            Text = "技能名称（即为 /command）",
            Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x9D)),
            FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        string suggestion = string.IsNullOrEmpty(_workDir) ? "my-skill"
            : System.Text.RegularExpressions.Regex.Replace(
                Path.GetFileName(_workDir).TrimEnd().ToLowerInvariant(),
                @"[^a-z0-9\-]", "-").Trim('-');
        if (string.IsNullOrEmpty(suggestion)) suggestion = "my-skill";

        var nameBox = InputBox2(suggestion);
        root.Children.Add(nameBox);

        root.Children.Add(new TextBlock
        {
            Text = "Claude 将把本次对话总结为可复用的技能。",
            Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A)),
            FontSize = 10, FontFamily = new FontFamily("Segoe UI"),
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        });

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancelBtn = MakeBtn("取消", "#2C2C2E", "#98989D");
        cancelBtn.Click += (_, _) => win.DialogResult = false;
        var goBtn = MakeBtn("生成技能", "#0A2540", "#0A84FF");
        goBtn.FontWeight = FontWeights.Bold;
        goBtn.Click += (_, _) => win.DialogResult = true;
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(new Border { Width = 8 });
        btnRow.Children.Add(goBtn);
        root.Children.Add(btnRow);

        win.Content = root;
        win.Loaded += (_, _) => { nameBox.SelectAll(); nameBox.Focus(); };

        if (win.ShowDialog() != true) return;

        string skillName = nameBox.Text.Trim()
            .Replace(" ", "-").Replace("/", "-").ToLowerInvariant();
        if (string.IsNullOrEmpty(skillName)) skillName = "my-skill";

        string mdPath = Path.Combine(_skillsRoot, skillName + ".md");

        string nl = "\n";
        string prompt =
            "Please summarize our entire conversation into a reusable Claude Code skill file. " +
            "Write a single markdown file with this exact structure:" + nl +
            "---" + nl +
            "name: " + skillName + nl +
            "description: >" + nl +
            "  [what it does and when to use it]" + nl +
            "---" + nl + nl +
            "# " + skillName + nl + nl +
            "## What this skill does" + nl +
            "[brief overview]" + nl + nl +
            "## Steps" + nl +
            "[numbered steps Claude should follow]" + nl + nl +
            "## Key details learned" + nl +
            "[specific facts, paths, formats discovered this session]" + nl + nl +
            "## Example usage" + nl +
            "[how to invoke this skill]" + nl + nl +
            "Save the file to: " + mdPath + nl +
            "Use the Write tool. Do not ask for permission.";

        AddUserBubble("[Save as Skill: /" + skillName + "]");
        SetBusy(true);

        _cts = new CancellationTokenSource();
        var tok = _cts.Token;
        var (_, _, tb) = AddAssistantBubble();

        try
        {
            string safe = prompt.Replace("\n", " ").Replace("\r", " ");
            string args = "/c \"\"" + _claudeExe + "\" --continue --dangerously-skip-permissions -p \"" + safe + "\"\"";

            _job = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe", Arguments = args,
                    WorkingDirectory = _workDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                }
            };
            _job.StartInfo.Environment["NO_COLOR"] = "1";
            _job.StartInfo.Environment["TERM"] = "dumb";
            _job.Start();

            await Task.WhenAll(
                StreamTo(_job.StandardOutput, tb, false, tok),
                StreamTo(_job.StandardError, tb, true, tok));
            await Task.Run(() => _job.WaitForExit(), tok);

            LoadSkillsTree();
            StatusBar.Text = "技能已保存：/" + skillName + "  —  可在技能面板中查看";
        }
        catch (OperationCanceledException) { tb.Text += " [已取消]"; }
        catch (Exception ex) { tb.Text += "\n错误：" + ex.Message; tb.Foreground = BrRed; }
        finally
        {
            _job?.Dispose(); _job = null;
            SetBusy(false);
            OutputScroller.ScrollToBottom();
            InputBox.Focus();
        }
    }

    // ── Image attach ──────────────────────────────────────
    private void AttachImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select an image to send",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;
        _attachedImagePath = dlg.FileName;
        ShowImagePreview(_attachedImagePath);
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        _attachedImagePath = null;
        ImagePreviewBar.Visibility = Visibility.Collapsed;
        ImagePreviewThumb.Source = null;
    }

    private void ShowImagePreview(string path)
    {
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelHeight = 80;
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            ImagePreviewThumb.Source = bmp;
            ImagePreviewName.Text = System.IO.Path.GetFileName(path);

            var info = new System.IO.FileInfo(path);
            ImagePreviewSize.Text = info.Length < 1024 * 1024
                ? $"{info.Length / 1024} KB"
                : $"{info.Length / (1024 * 1024.0):F1} MB";

            ImagePreviewBar.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SysLine("无法加载图片：" + ex.Message, BrRed);
            _attachedImagePath = null;
        }
    }

        // ════════════════════════════════════════════════════════
        //  STATIC COMMAND DEFINITIONS
        // ════════════════════════════════════════════════════════

        public static readonly RoutedCommand NewSessionCmd = new();
        public static readonly RoutedCommand CloseTabCmd = new();
        public static readonly RoutedCommand SwitchTabCmd = new();
        public static readonly RoutedCommand QuickOpenCmd = new();
        public static readonly RoutedCommand FindInChatCmd = new();
        public static readonly RoutedCommand CommandPaletteCmd = new();
        public static readonly RoutedCommand ExportSessionCmd = new();
        public static readonly RoutedCommand OpenSettingsCmd = new();
        public static readonly RoutedCommand ToggleTerminalCmd = new();

        private void NewSession_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            AddTab_Click(null!, null!);
        }
        private void NewSession_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _sessionManager != null;
        }
        private void CloseTab_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var tabs = _sessionManager?.Tabs;
            if (tabs == null || tabs.Count <= 1) return;
            var current = _sessionManager?.ActiveTab;
            if (current != null) _sessionManager?.CloseTab(current);
        }
        private void CloseTab_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _sessionManager?.Tabs?.Count > 1;
        }
        private void SwitchTab_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var tabs = _sessionManager?.Tabs;
            if (tabs == null || tabs.Count < 2) return;
            var current = _sessionManager?.ActiveTab;
            if (current == null) return;
            var idx = tabs.IndexOf(current);
            _sessionManager?.ActivateTab(tabs[(idx + 1) % tabs.Count]);
        }
        private void SwitchTab_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _sessionManager?.Tabs?.Count > 1;
        }
        private void QuickOpen_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            StatusBar.Text = "快速打开功能开发中...";
            if (_sidebarTabIndex != 2) SidebarTabFiles_Click(null!, null!);
        }
        private void FindInChat_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            StatusBar.Text = "聊天搜索功能开发中...";
        }
        private void CommandPalette_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var palette = new CommandPaletteWindow();
            palette.Owner = this;
            palette.ShowDialog();
        }
        private void ExportSession_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            StatusBar.Text = "导出功能开发中...";
        }
        private void ExportSession_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _sessionTabTimeline?.Entries.Count > 0;
        }
        private void OpenSettings_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var settings = new SettingsWindow();
            settings.ShowDialog();
        }
        private void OpenSettings_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
}

