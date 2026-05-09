using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ClaudeCodeGUI
{
    /// <summary>
    /// 会话状态 - 封装单个标签页的所有状态
    /// </summary>
    public class SessionState : INotifyPropertyChanged
    {
        // ── 标识 ─────────────────────────────────────────────────
        private string _sessionId = Guid.NewGuid().ToString();
        private string _name = "新会话";
        private string? _claudeSessionId;
        private string? _sessionItemId;

        // ── 输入状态 ─────────────────────────────────────────────
        private string _draftInput = "";
        private bool _isExecuting = false;

        // ── 模式与权限 ───────────────────────────────────────────
        private InteractionMode _mode = InteractionMode.Build;
        private RuntimeMode _permissionLevel = RuntimeMode.ApprovalRequired;
        private HashSet<string> _allowedTools = new();

        // ── 模型 ─────────────────────────────────────────────────
        private string _selectedModel = "";
        private List<string> _availableModels = new();

        // ── Tokens ───────────────────────────────────────────────
        private long _usedTokens = 0;
        private long _maxTokens = 200_000;

        // ── 任务（TodoWrite 工具同步；ObservableCollection 供侧栏与 ThinkingBar 绑定）──
        private readonly ObservableCollection<TodoItem> _tasks = new();

        // ── 工作目录 ─────────────────────────────────────────────
        private string _workingDirectory = "";

        // ════════════════════════════════════════════════════════
        // 属性定义（带变更通知）
        // ════════════════════════════════════════════════════════

        public SessionState()
        {
            _tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTasks));
        }

        /// <summary>会话唯一标识</summary>
        public string SessionId
        {
            get => _sessionId;
            set { _sessionId = value; OnPropertyChanged(); }
        }

        /// <summary>会话名称（显示在标签上）</summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>claudecode 会话 UUID，用于 --session-id / --resume</summary>
        public string? ClaudeSessionId
        {
            get => _claudeSessionId;
            set { _claudeSessionId = value; OnPropertyChanged(); }
        }

        /// <summary>关联的 SessionItem.Id（projects.json 中的会话 ID），用于 SQLite 关联</summary>
        public string? SessionItemId
        {
            get => _sessionItemId;
            set { _sessionItemId = value; OnPropertyChanged(); }
        }

        /// <summary>输入框草稿内容</summary>
        public string DraftInput
        {
            get => _draftInput;
            set { _draftInput = value; OnPropertyChanged(); }
        }

        /// <summary>是否正在执行中</summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
                // 触发相关派生属性变更
                OnPropertyChanged(nameof(SendButtonText));
                OnPropertyChanged(nameof(SendButtonBackground));
            }
        }

        /// <summary>交互模式（构建/计划）</summary>
        public InteractionMode Mode
        {
            get => _mode;
            set { _mode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModeLabel)); }
        }

        /// <summary>权限级别</summary>
        public RuntimeMode PermissionLevel
        {
            get => _permissionLevel;
            set { _permissionLevel = value; OnPropertyChanged(); }
        }

        /// <summary>已批准的工具集合（AllowAlways 功能）</summary>
        public HashSet<string> AllowedTools
        {
            get => _allowedTools;
            set { _allowedTools = value; OnPropertyChanged(); }
        }

        /// <summary>检查工具是否被允许</summary>
        public bool IsToolAllowed(string toolName) => _allowedTools.Contains(toolName);

        /// <summary>添加已批准的工具</summary>
        public void AllowTool(string toolName) => _allowedTools.Add(toolName);

        /// <summary>清除已批准的工具列表</summary>
        public void ClearAllowedTools() => _allowedTools.Clear();

        /// <summary>当前选中的模型</summary>
        public string SelectedModel
        {
            get => _selectedModel;
            set { _selectedModel = value; OnPropertyChanged(); }
        }

        /// <summary>可用模型列表</summary>
        public List<string> AvailableModels
        {
            get => _availableModels;
            set { _availableModels = value; OnPropertyChanged(); }
        }

        /// <summary>已使用 Tokens</summary>
        public long UsedTokens
        {
            get => _usedTokens;
            set
            {
                _usedTokens = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TokensPercentage));
            }
        }

        /// <summary>最大 Tokens（根据模型动态）</summary>
        public long MaxTokens
        {
            get => _maxTokens;
            set
            {
                _maxTokens = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TokensPercentage));
            }
        }

        // ── 新增：Token 明细 ──────────────────────────────────
        private long _inputTokens = 0;
        private long _outputTokens = 0;

        /// <summary>本次对话已使用的输入 Tokens</summary>
        public long InputTokens
        {
            get => _inputTokens;
            set
            {
                _inputTokens = value;
                OnPropertyChanged();
            }
        }

        /// <summary>本次对话已使用的输出 Tokens</summary>
        public long OutputTokens
        {
            get => _outputTokens;
            set
            {
                _outputTokens = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Token 用量更新通知（用于 TokenUsageTracker 触发 UI 更新）</summary>
        public event EventHandler? TokenUsageUpdated;

        /// <summary>触发 TokenUsageUpdated 事件</summary>
        public void NotifyTokenChanged()
        {
            TokenUsageUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>任务列表（与 ThinkingBar 共用同一集合引用）</summary>
        public ObservableCollection<TodoItem> Tasks => _tasks;

        /// <summary>工作目录</summary>
        public string WorkingDirectory
        {
            get => _workingDirectory;
            set { _workingDirectory = value; OnPropertyChanged(); }
        }

        // ════════════════════════════════════════════════════════
        // 派生属性（只读）
        // ════════════════════════════════════════════════════════

        /// <summary>是否有任务</summary>
        public bool HasTasks => _tasks.Count > 0;

        /// <summary>Tokens 使用百分比（0-100）</summary>
        public double TokensPercentage => _maxTokens > 0 ? (_usedTokens * 100.0 / _maxTokens) : 0;

        /// <summary>发送按钮文本</summary>
        public string SendButtonText => _isExecuting ? "⏹" : "▶";

        /// <summary>发送按钮背景色（运行时转换）</summary>
        public string SendButtonBackground => _isExecuting ? "#FF4444" : "#0A84FF";

        /// <summary>权限级别显示文本</summary>
        public string PermissionLabel => _permissionLevel switch
        {
            RuntimeMode.AutoAcceptEdits => "自动接受编辑",
            RuntimeMode.FullAccess => "完全访问",
            _ => "监督模式"
        };

        /// <summary>模式显示文本</summary>
        public string ModeLabel => _mode == InteractionMode.Build ? "构建" : "计划";

        // ════════════════════════════════════════════════════════
        // INotifyPropertyChanged 实现
        // ════════════════════════════════════════════════════════

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ════════════════════════════════════════════════════════
        // 辅助方法
        // ════════════════════════════════════════════════════════

        /// <summary>切换构建/计划模式</summary>
        public void ToggleMode()
        {
            Mode = _mode == InteractionMode.Build ? InteractionMode.Plan : InteractionMode.Build;
        }

        /// <summary>添加或更新任务</summary>
        public void AddOrUpdateTask(string content, TodoStatus status, string? activeForm = null)
        {
            TodoItem? existing = null;
            foreach (var t in _tasks)
            {
                if (t.Content == content)
                {
                    existing = t;
                    break;
                }
            }

            if (existing != null)
            {
                existing.Status = status;
                existing.ActiveForm = activeForm;
            }
            else
            {
                _tasks.Add(new TodoItem
                {
                    Content = content,
                    Status = status,
                    ActiveForm = activeForm
                });
            }
        }

        /// <summary>解析 Claude Code <c>TodoWrite</c> 工具 JSON，同步步骤列表。</summary>
        public void ApplyTodoWriteFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool merge = true;
                if (root.TryGetProperty("merge", out var mergeProp) && mergeProp.ValueKind == JsonValueKind.False)
                    merge = false;

                if (!root.TryGetProperty("todos", out var todos) || todos.ValueKind != JsonValueKind.Array)
                    return;

                if (!merge)
                    _tasks.Clear();

                foreach (var item in todos.EnumerateArray())
                {
                    var content = item.TryGetProperty("content", out var c) ? (c.GetString() ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(content)) continue;

                    var statusStr = item.TryGetProperty("status", out var s)
                        ? (s.GetString() ?? "pending").Replace(' ', '_')
                        : "pending";

                    string? activeForm = null;
                    if (item.TryGetProperty("activeForm", out var af) && af.ValueKind == JsonValueKind.String)
                        activeForm = af.GetString();
                    else if (item.TryGetProperty("active_form", out var af2) && af2.ValueKind == JsonValueKind.String)
                        activeForm = af2.GetString();

                    var status = statusStr switch
                    {
                        "in_progress" => TodoStatus.InProgress,
                        "completed" => TodoStatus.Completed,
                        "cancelled" => TodoStatus.Completed,
                        _ => TodoStatus.Pending
                    };

                    AddOrUpdateTask(content, status, activeForm);
                }
            }
            catch (JsonException)
            {
                /* 忽略单次损坏 JSON */
            }
        }

        /// <summary>清除所有任务</summary>
        public void ClearTasks() => _tasks.Clear();

        /// <summary>更新 Token 用量（兼容旧调用）</summary>
        public void UpdateTokenUsage(long used, long? max = null)
        {
            UsedTokens = used;
            if (max.HasValue) MaxTokens = max.Value;
        }

        /// <summary>更新 Token 用量明细（含 input/output 分解）</summary>
        public void UpdateTokenUsageDetail(long inputTokens, long outputTokens, long? max = null)
        {
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            UsedTokens = inputTokens + outputTokens;
            if (max.HasValue) MaxTokens = max.Value;
            NotifyTokenChanged();
        }
    }
}
