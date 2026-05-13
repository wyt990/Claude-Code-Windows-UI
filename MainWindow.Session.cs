using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClaudeCodeGUI;

/// <summary>会话管理：发送消息、进程控制、状态切换</summary>
public partial class MainWindow : Window
{
    /// <summary>Claude Code 通过 TodoWrite 工具下发任务步骤。</summary>
    private static bool IsTodoWriteTool(string? toolName) =>
        toolName != null
        && toolName.Equals("TodoWrite", StringComparison.OrdinalIgnoreCase);

    private async void SendMessage_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private async void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control
                || Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Ctrl+Enter / Shift+Enter = insert newline
                // Don't handle — let TextBox (AcceptsReturn=True) do it naturally
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                // Enter = send — intercept before TextBox inserts newline
                e.Handled = true;
                await RunAsync();
            }
        }
    }

    private void OutputScroller_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Gather all text from chat panel
            var sb = new StringBuilder();
            foreach (UIElement el in ChatPanel.Children)
            {
                if (el is Border b && b.Child is StackPanel sp)
                {
                    foreach (UIElement child in sp.Children)
                    {
                        if (child is TextBlock tb)
                            sb.AppendLine(tb.Text);
                    }
                    sb.AppendLine();
                }
                else if (el is TextBlock stb)
                {
                    sb.AppendLine(stb.Text);
                }
            }
            string text = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                StatusBar.Text = "聊天内容已复制到剪贴板";
            }
            e.Handled = true;
        }
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (InputPlaceholder != null)
            InputPlaceholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Drag & Drop ───────────────────────────────────────
    private void InputBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else e.Effects = DragDropEffects.None;
    }

    private void InputBox_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 0) return;
        var file = files[0];
        var projectDir = _session.WorkingDirectory;
        if (string.IsNullOrEmpty(projectDir) || !file.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
        {
            StatusBar.Text = "只能拖入项目目录内的文件";
            return;
        }
        var relativePath = file.Substring(projectDir.Length).TrimStart('\\', '/');
        InputBox.Text += $" @{relativePath} ";
        InputBox.CaretIndex = InputBox.Text.Length;
        InputBox.Focus();
        StatusBar.Text = $"已附加文件：{relativePath}";
    }

    // ── Core run ──────────────────────────────────────────
    private async Task RunAsync()
    {
        if (_busy || !_ready || _claudeExe == null) return;
        string msg = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(msg)) return;

        // BUG FIX #4: SetBusy early to close race window
        SetBusy(true);

        InputBox.Clear();
        AddUserBubble(msg);
        string fullMsg = msg;
        if (!string.IsNullOrEmpty(_attachedImagePath) && File.Exists(_attachedImagePath))
        {
            AddImageBubble(_attachedImagePath);
            fullMsg = "[Image: " + _attachedImagePath + "]\n" + msg;
            string sentPath = _attachedImagePath;
            _attachedImagePath = null;
            _ = Dispatcher.InvokeAsync(() => {
                ImagePreviewBar.Visibility = Visibility.Collapsed;
                ImagePreviewThumb.Source = null;
            });
        }

        _cts = new CancellationTokenSource();
        var tok = _cts.Token;
        var (bubble, panel, tb) = AddAssistantBubble();
        var renderer = new StreamingRenderer(tb, OutputScroller);

        try
        {
            string safe = fullMsg.Replace("\r", "").Replace("\n", " ").Replace("\"", "'");

            // 构建会话参数：使用 --output-format stream-json 获取结构化事件
            var args = new List<string>();

            if (!string.IsNullOrEmpty(_session.ClaudeSessionId))
            {
                // 结构化流输出
                args.Add("--output-format"); args.Add("stream-json");
                args.Add("--verbose");
                args.Add("--include-partial-messages");

                if (_hasContinue)
                {
                    args.Add("--resume"); args.Add(_session.ClaudeSessionId);
                }
                else
                {
                    args.Add("--session-id"); args.Add(_session.ClaudeSessionId);
                }
            }
            else
            {
                // 防御性兜底：不应发生，因所有新建会话都生成 UUID
                SysLine("[警告] 会话缺少 ClaudeSessionId，请通过侧边栏重新创建会话。", BrYellow);
                if (_hasContinue)
                    args.Add("--continue");
            }

            args.Add("-p");
            args.Add($"\"{safe}\"");

            // Add interaction mode
            if (_session.Mode == InteractionMode.Plan)
                args.Add("--interaction-mode=plan");

            // Add permission level
            if (_session.PermissionLevel == RuntimeMode.AutoAcceptEdits)
                args.Add("--accept-edits");
            else if (_session.PermissionLevel == RuntimeMode.FullAccess)
                args.Add("--dangerously-skip-permissions");

            // Add model selection
            if (!string.IsNullOrEmpty(_session.SelectedModel))
                args.Add($"--model={_session.SelectedModel}");

            string claudeArgs = string.Join(" ", args);

            _job = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{_claudeExe}\" {claudeArgs}\"",
                    WorkingDirectory = _workDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,   // 为审批流程准备
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };
            _job.StartInfo.Environment["NO_COLOR"] = "1";
            _job.StartInfo.Environment["TERM"] = "dumb";
            _job.StartInfo.Environment["FORCE_COLOR"] = "0";
            _job.Start();

            // BUG FIX #5: sync process/CTS to active tab so context menu "Stop" works
            if (_lastActiveTab != null)
            {
                _lastActiveTab.Job = _job;
                _lastActiveTab.CTS = _cts;
            }

            // 使用事件解析器流式读取 stdout
            var parser = new ClaudeEventParser();
            var eventBus = EventBus.Instance;

            // ── 工具事件跟踪状态 ──────────────────────────────
            string? currentToolId = null;
            string? currentToolName = null;
            StringBuilder? currentToolInput = null;
            TimelineEntry? currentToolEntry = null;

            // BUG FIX #3: declare handlers outside try so finally can access them
            EventHandler<TextDeltaEvent>? textHandler = null;
            EventHandler<MessageStopEvent>? stopHandler = null;
            EventHandler<ResultEvent>? resultHandler = null;
            EventHandler<ToolBlockStartEvent>? toolStartHandler = null;
            EventHandler<ToolInputDeltaEvent>? toolInputHandler = null;
            EventHandler<ContentBlockStopEvent>? toolBlockHandler = null;
            EventHandler<ToolResultEvent>? toolResultHandler = null;

            try
            {
                textHandler = (_, delta) =>
                {
                    renderer?.AppendText(delta.Text);
                    if (!string.IsNullOrEmpty(delta.Text))
                    {
                        _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            _lastOutputTime = DateTime.Now));
                    }
                };
                eventBus.TextDeltaReceived += textHandler;

                stopHandler = (_, _) =>
                {
                    renderer?.Flush();
                    FinalizeAssistantEntry(tb);
                };
                eventBus.MessageStopped += stopHandler;

                resultHandler = (_, result) =>
                {
                    var tracker = new TokenUsageTracker(_session);
                    tracker.UpdateFromResult(
                        result.ContextWindow,
                        result.InputTokens,
                        result.OutputTokens);
                };
                eventBus.ResultReceived += resultHandler;

                toolStartHandler = (_, evt) =>
                {
                    currentToolId = evt.ToolId;
                    currentToolName = evt.ToolName;
                    currentToolInput = new StringBuilder();

                    var status = _session.PermissionLevel == RuntimeMode.FullAccess
                        ? ToolCallStatus.Running
                        : ToolCallStatus.PendingApproval;
                    currentToolEntry = _sessionTabTimeline?.AddToolCall(evt.ToolName, evt.ToolId, status);
                };
                eventBus.ToolUseStarted += toolStartHandler;

                toolInputHandler = (_, evt) =>
                {
                    currentToolInput?.Append(evt.PartialJson);
                };
                eventBus.ToolInputDelta += toolInputHandler;

                toolBlockHandler = (_, _) =>
                {
                    if (currentToolId == null || currentToolEntry == null) return;

                    string inputJson = currentToolInput?.ToString() ?? "";
                    currentToolEntry.Text = inputJson;

                    bool approvedForTools = true;

                    if (_session.PermissionLevel != RuntimeMode.FullAccess)
                    {
                        bool approved;

                        if (_session.IsToolAllowed(currentToolName ?? ""))
                        {
                            approved = true;
                        }
                        else if (_session.PermissionLevel == RuntimeMode.ApprovalRequired)
                        {
                            var dialog = new ApprovalDialog(
                                this, currentToolName ?? "", "", inputJson);
                            var dialogResult = dialog.ShowDialog();

                            if (dialogResult == true)
                            {
                                switch (dialog.Result)
                                {
                                    case ApprovalResult.AllowAll:
                                        _session.PermissionLevel = RuntimeMode.FullAccess;
                                        approved = true;
                                        break;
                                    case ApprovalResult.AllowAlways:
                                        _session.AllowTool(currentToolName ?? "");
                                        approved = true;
                                        break;
                                    case ApprovalResult.Allow:
                                        approved = true;
                                        break;
                                    default:
                                        approved = false;
                                        break;
                                }
                            }
                            else
                            {
                                approved = false;
                            }
                        }
                        else
                        {
                            approved = true;
                        }

                        approvedForTools = approved;

                        try
                        {
                            _job?.StandardInput.WriteLine(approved ? "y" : "n");
                        }
                        catch { }

                        currentToolEntry.ToolStatus = approved
                            ? ToolCallStatus.Running
                            : ToolCallStatus.Completed;
                    }

                    if (IsTodoWriteTool(currentToolName)
                        && (_session.PermissionLevel == RuntimeMode.FullAccess || approvedForTools))
                    {
                        string jsonCopy = inputJson;
                        _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            _session.ApplyTodoWriteFromJson(jsonCopy)));
                    }

                    currentToolId = null;
                    currentToolName = null;
                    currentToolInput = null;
                    currentToolEntry = null;
                };
                eventBus.ToolBlockCompleted += toolBlockHandler;

                toolResultHandler = (_, evt) =>
                {
                    if (_sessionTabTimeline == null) return;
                    foreach (var e in _sessionTabTimeline.Entries)
                    {
                        if (e.Kind == TimelineEntryKind.ToolCall && e.ToolId == evt.ToolUseId)
                        {
                            e.ToolStatus = ToolCallStatus.Completed;
                            if (!string.IsNullOrEmpty(evt.Content))
                                e.Text = evt.Content.Length > 200
                                    ? evt.Content[..200] + "…"
                                    : evt.Content;
                            break;
                        }
                    }
                };
                eventBus.ToolResultReceived += toolResultHandler;

                // ── 逐行读取并解析 stdout ──
                string? line;
                while ((line = await _job.StandardOutput.ReadLineAsync()) != null)
                {
                    if (tok.IsCancellationRequested) break;

                    try
                    {
                        var evt = parser.ParseLine(line);
                        if (evt == null) continue;

                        eventBus.Publish(evt);
                    }
                    catch (Exception lineEx)
                    {
                        if (StreamParseLogWindow.ShouldAutoShow(lineEx))
                            StreamParseLogWindow.ShowDiagnostic(this, lineEx, line);
                        continue; // skip bad line instead of killing the conversation
                    }
                }

                // ── 读取 stderr 日志输出 ──
                var stderrBuilder = new StringBuilder();
                await Task.Run(() =>
                {
                    string? errLine;
                    while ((errLine = _job.StandardError.ReadLine()) != null)
                    {
                        if (tok.IsCancellationRequested) break;
                        lock (stderrBuilder)
                            stderrBuilder.AppendLine(errLine);
                    }
                }, tok);

                await Task.Run(() => _job.WaitForExit(), tok);

                // BUG FIX #2: show stderr output if any
                string stderrText;
                lock (stderrBuilder)
                    stderrText = stderrBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(stderrText))
                {
                    tb.Text += $"\n── stderr ──\n{stderrText}";
                    tb.Foreground = BrYellow;
                }
            }
            finally
            {
                // ── 取消订阅事件 ──
                eventBus.TextDeltaReceived -= textHandler;
                eventBus.MessageStopped -= stopHandler;
                eventBus.ResultReceived -= resultHandler;
                eventBus.ToolUseStarted -= toolStartHandler;
                eventBus.ToolInputDelta -= toolInputHandler;
                eventBus.ToolBlockCompleted -= toolBlockHandler;
                eventBus.ToolResultReceived -= toolResultHandler;
            }

            int exitCode = _job?.ExitCode ?? 0;
            if (exitCode != 0 && string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = $"进程退出，退出码 {exitCode}。\n";
                tb.Text += "请检查：在终端中运行 claude --version 确认 CLI 工作正常。";
                tb.Foreground = BrYellow;
            }
            _hasContinue = true; _turns++;
            TurnCounter.Text = $"{_turns} 轮";
            SaveSkillBtn.IsEnabled = true;  // conversation exists, can now save as skill

            // 流式完成后重建气泡：将纯文本按代码围栏解析为 CodeBlock / diff 块
            if (renderer != null)
            {
                renderer.Flush();
                string fullText = renderer.FullText;
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    // 保存头部（"Claude  HH:mm"），重建后重新添加
                    UIElement header = panel.Children[0];
                    try
                    {
                        panel.Children.Clear();
                        panel.Children.Add(header);
                        BuildMessageContent(panel, fullText);
                        BubbleActions.AddActions(panel, () => fullText, false, bubbleBorder: bubble);
                    }
                    catch (Exception ex)
                    {
                        // 重建失败时退回原始文本
                        System.Diagnostics.Debug.WriteLine($"[Rebuild] {ex.Message}");
                        panel.Children.Clear();
                        panel.Children.Add(header);
                        panel.Children.Add(new TextBlock
                        {
                            Text = fullText,
                            Foreground = BrDefault, FontSize = 14,
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            TextWrapping = TextWrapping.Wrap, LineHeight = 22
                        });
                        BubbleActions.AddActions(panel, () => fullText, false, bubbleBorder: bubble);
                    }
                }
            }

            // 持久化到 SQLite
            string persistedText = renderer?.FullText ?? tb.Text;
            PersistConversationTurn(fullMsg, persistedText);
        }
        catch (OperationCanceledException) { renderer?.Flush(); tb.Text += " [已停止]"; }
        catch (Exception ex) { renderer?.Flush(); tb.Text += $"\n错误：{ex.Message}"; tb.Foreground = BrRed; }
        finally
        {
            renderer?.Dispose();
            _job?.Dispose(); _job = null;
            SetBusy(false);
            OutputScroller.ScrollToBottom();
            InputBox.Focus();
        }
    }

    // ── Legacy StreamTo (for SaveAsSkill) ─────────────────
    private async Task StreamTo(StreamReader r, TextBlock tb, bool isErr, CancellationToken tok)
    {
        var buf = new char[128];
        try
        {
            while (!tok.IsCancellationRequested)
            {
                int n = await r.ReadAsync(buf, 0, buf.Length);
                if (n == 0) break;
                string clean = Strip(new string(buf, 0, n));
                if (string.IsNullOrEmpty(clean)) continue;
                foreach (var ln in clean.Split('\n'))
                {
                    string rawLine = ln.TrimEnd('\r');
                    if (rawLine.Trim().Length == 0) continue;
                }
                await Dispatcher.InvokeAsync(() =>
                {
                    tb.Text += clean;
                    OutputScroller.ScrollToBottom();
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Process helpers ───────────────────────────────────
    private void KillJob()
    {
        _cts?.Cancel();
        try { _job?.Kill(entireProcessTree: true); } catch { }
    }

    // ── Conversation persistence ──────────────────────────
    private void PersistConversationTurn(string userMsg, string assistantMsg)
    {
        string? sid = _session.SessionItemId;
        if (string.IsNullOrEmpty(sid)) return;

        try
        {
            _conversationStore.MarkAsStarted(sid);
            _conversationStore.SaveMessage(sid, "user", userMsg);
            _conversationStore.SaveMessage(sid, "assistant", assistantMsg);

            // 持久化工具调用条目
            if (_sessionTabTimeline != null)
            {
                foreach (var e in _sessionTabTimeline.Entries)
                {
                    if (e.Kind != TimelineEntryKind.ToolCall) continue;
                    string content = $"{{\"tool\":\"{e.ToolName}\",\"input\":{e.Text}}}";
                    _conversationStore.SaveEntry(sid, "tool", content, "tool_call");
                }
            }
        }
        catch (Exception ex)
        {
            SysLine($"[SQLite] 保存消息失败：{ex.Message}", BrYellow);
        }
    }

    // ── State ──────────────────────────────────────────────
    private void SetReady(bool ok)
    {
        _ready = ok;
        BrowseButton.Background = ok ? BrBrowseGreen : BrBrowseGray;
        SendButton.IsEnabled = ok;
        StopButton.IsEnabled = false;
        StatusDot.Fill  = ok ? BrGreen : BrMuted;
        StatusText.Text = ok ? "就绪" : "空闲";
        SubtitleLabel.Text = ok && _claudeExe != null ? Path.GetFileName(_claudeExe) : "未启动";
        StatusBar.Text = ok ? "就绪 — 输入消息后按 Enter 发送"
                            : "未找到 claudecode — 请查看上方消息";
    }

    private void SetBusy(bool on)
    {
        _busy = on;
        StopButton.IsEnabled = on;
        StatusDot.Fill  = on ? BrOrange : BrGreen;
        StatusText.Text = on ? "运行中" : "就绪";
        ThinkingBar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        if (on)
        {
            _fi = 0;
            _session.ClearTasks();
            _lastOutputTime = DateTime.Now;
            SpinnerText.Text = Frames[0];
            ThinkingLabel.Text = "发送中";
            LiveOutputLabel.Text = "等待回复…";
            LiveOutputLabel.Foreground = BrMuted;
            ElapsedLabel.Text = "0s";
            ElapsedLabel.Foreground = BrBlue;
            _sw.Restart();
            _timer.Start();
            StatusBar.Text = "已发送 — 等待 Claude 回复…";
            SendButton.Content = "\u280b  运行中";
            SendButton.Background = H("#5C3000");
            SendButton.Foreground = H("#FF9F0A");
            SendButton.IsEnabled = false;
        }
        else
        {
            _timer.Stop(); _sw.Stop();
            string secs = $"{_sw.Elapsed.TotalSeconds:F1}s";
            ProgressBarFill.Width = 0;
            ThinkingLabel.Text = "完成";
            LiveOutputLabel.Text = "已收到回复。";
            LiveOutputLabel.Foreground = BrGreen;
            ElapsedLabel.Text = secs;
            ElapsedLabel.Foreground = BrGreen;
            StatusBar.Text = $"就绪  ·  {_turns} 轮  ·  回复耗时 {secs}";
            SendButton.Content = _session.SendButtonText;
            SendButton.Background = H("#0A84FF");
            SendButton.Foreground = H("#FFFFFF");
            SendButton.IsEnabled = _ready;
        }
    }
}
