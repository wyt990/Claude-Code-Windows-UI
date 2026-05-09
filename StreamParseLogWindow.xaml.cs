using System;
using System.Text;
using System.Windows;

namespace ClaudeCodeGUI;

/// <summary>
/// 展示 stream-json 单行解析失败时的诊断信息（异常详情 + 原始 stdout 行）。
/// </summary>
public partial class StreamParseLogWindow : Window
{
    public StreamParseLogWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 是否应在本次异常时自动弹出诊断窗口（聚焦 JsonElement 类型不匹配等解析问题）。
    /// </summary>
    public static bool ShouldAutoShow(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is System.Text.Json.JsonException)
                return true;
            if (e.Message.Contains("requires an element of type", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 构造异常报告正文。
    /// </summary>
    public static string BuildDetailText(Exception ex, string? rawLine)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"时间 (本地): {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"异常类型: {ex.GetType().FullName}");
        sb.AppendLine($"消息: {ex.Message}");
        sb.AppendLine();

        sb.AppendLine("--- InnerException 链 ---");
        Exception? inner = ex.InnerException;
        int depth = 0;
        while (inner != null && depth < 12)
        {
            depth++;
            sb.AppendLine($"[{depth}] {inner.GetType().FullName}");
            sb.AppendLine($"    {inner.Message}");
            inner = inner.InnerException;
        }
        if (depth == 0)
            sb.AppendLine("(无)");
        sb.AppendLine();

        sb.AppendLine("--- Stack trace ---");
        sb.AppendLine(ex.StackTrace ?? "(null)");
        sb.AppendLine();

        sb.AppendLine("=== 待解析的完整 stdout 行（原始字符串）===");
        if (rawLine == null)
            sb.AppendLine("(null)");
        else
            sb.AppendLine(rawLine);
        return sb.ToString();
    }

    /// <summary>
    /// 显示非模态诊断窗口并置顶激活。
    /// </summary>
    public static void ShowDiagnostic(Window owner, Exception ex, string? rawLine)
    {
        string body = BuildDetailText(ex, rawLine);
        var win = new StreamParseLogWindow
        {
            Owner = owner,
        };
        win.DetailBox.Text = body;
        win.Show();
        win.Activate();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(DetailBox.Text);
        }
        catch
        {
            /* 剪贴板不可用 */
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
