namespace ClaudeCodeGUI;

/// <summary>交互模式：构建(Build) 或 计划(Plan)</summary>
public enum InteractionMode
{
    Build,
    Plan
}

/// <summary>运行时权限模式</summary>
public enum RuntimeMode
{
    /// <summary>每个操作都需要用户确认</summary>
    ApprovalRequired,
    /// <summary>自动接受文件编辑，命令需确认</summary>
    AutoAcceptEdits,
    /// <summary>跳过所有权限提示</summary>
    FullAccess
}

/// <summary>任务项状态</summary>
public enum TodoStatus
{
    Pending,
    InProgress,
    Completed
}

/// <summary>工具调用状态（用于 TimelineEntry.ToolStatus）</summary>
public enum ToolCallStatus
{
    /// <summary>工具正在执行中</summary>
    Running,
    /// <summary>等待用户审批</summary>
    PendingApproval,
    /// <summary>工具执行完成（或被拒绝）</summary>
    Completed
}
