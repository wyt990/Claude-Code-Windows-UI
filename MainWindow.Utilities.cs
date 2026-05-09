using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ClaudeCodeGUI;

/// <summary>工具方法：Claude 查找、ANSI 清理、颜色辅助</summary>
public partial class MainWindow : Window
{
    private static IEnumerable<string> Candidates()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            // claudecode (custom fork) paths — user's actual tool
            Path.Combine(home,    ".local", "bin", "claudecode.cmd"),
            Path.Combine(local,   "claude-code-local", "claudecode.exe"),
            // official claude paths (fallback)
            Path.Combine(local,   "AnthropicClaude", "claude.exe"),
            Path.Combine(local,   "Programs", "claude", "claude.exe"),
            Path.Combine(home,    ".claude", "local", "claude.exe"),
            Path.Combine(local,   "Claude", "claude.exe"),
            Path.Combine(roaming, "npm", "claude.cmd"),
            Path.Combine(local,   "npm", "claude.cmd"),
        };
    }

    private static string? FindClaude()
    {
        foreach (var p in Candidates()) if (File.Exists(p)) return p;
        try
        {
            using var w = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c where claudecode claude",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            string? r = w?.StandardOutput.ReadLine(); w?.WaitForExit();
            if (!string.IsNullOrWhiteSpace(r) && File.Exists(r.Trim())) return r.Trim();
        }
        catch { }
        return null;
    }

    private static string Strip(string s)
    {
        s = Ansi.Replace(s, ""); s = s.Replace("\x1B", "");
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        return s;
    }

    private static SolidColorBrush H(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze(); return b;
    }
}
