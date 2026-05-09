using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace ClaudeCodeGUI;

/// <summary>审批结果</summary>
public enum ApprovalResult
{
    /// <summary>拒绝执行</summary>
    Deny,
    /// <summary>批准本次执行</summary>
    Allow,
    /// <summary>全部允许</summary>
    AllowAll,
    /// <summary>本次会话始终允许此工具</summary>
    AllowAlways,
}

/// <summary>工具调用审批对话框</summary>
public partial class ApprovalDialog : Window
{
    /// <summary>用户选择的审批结果</summary>
    public ApprovalResult Result { get; private set; } = ApprovalResult.Deny;

    /// <summary>
    /// 创建审批对话框
    /// </summary>
    /// <param name="owner">父窗口（用于 CenterOwner）</param>
    /// <param name="toolName">工具名称（Bash / Read / Edit 等）</param>
    /// <param name="description">工具描述或命令</param>
    /// <param name="inputJson">工具调用参数 JSON</param>
    public ApprovalDialog(Window owner, string toolName, string description, string inputJson)
    {
        InitializeComponent();
        Owner = owner;
        ToolNameText.Text = toolName;
        ToolDescriptionText.Text = description;
        JsonTextBox.Text = FormatJson(inputJson);
    }

    private void ApprovalDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // 淡入动画（仅 opacity，避免 ScaleTransform 与 DialogResult 交互异常）
        Opacity = 0;
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
    }

    private void AllowBtn_Click(object sender, RoutedEventArgs e)
    {
        if (AllowAlwaysCheckbox.IsChecked == true)
            Result = ApprovalResult.AllowAlways;
        else
            Result = ApprovalResult.Allow;
        DialogResult = true;
    }

    private void DenyBtn_Click(object sender, RoutedEventArgs e)
    {
        Result = ApprovalResult.Deny;
        DialogResult = true;
    }

    private void AllowAllBtn_Click(object sender, RoutedEventArgs e)
    {
        Result = ApprovalResult.AllowAll;
        DialogResult = true;
    }

    private void AllowAlways_Checked(object sender, RoutedEventArgs e)
    {
        // 勾选"始终允许"时自动选中"批准"语义
    }

    /// <summary>简单格式化 JSON 字符串（缩进）</summary>
    private static string FormatJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}
