using System;
using System.Collections.Generic;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

public enum ProblemSeverity { Error, Warning, Info }

public class ProblemItem
{
    public ProblemSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public string Location => System.IO.Path.GetFileName(FilePath) + ":" + LineNumber;

    public string Icon => Severity switch
    {
        ProblemSeverity.Error => "\u00d7",
        ProblemSeverity.Warning => "!",
        ProblemSeverity.Info => "i",
        _ => "?"
    };

    public SolidColorBrush SeverityColor => Severity switch
    {
        ProblemSeverity.Error => ThemeManager.GetColor("ThemeError"),
        ProblemSeverity.Warning => ThemeManager.GetColor("ThemeWarning"),
        ProblemSeverity.Info => ThemeManager.GetColor("ThemeTextSecondary"),
        _ => ThemeManager.GetColor("ThemeTextMuted")
    };
}

public class ProblemEventArgs : EventArgs
{
    public ProblemItem? Problem { get; }
    public ProblemEventArgs(ProblemItem? problem) => Problem = problem;
}

public static class ProblemService
{
    private static readonly List<ProblemItem> _problems = new();

    public static event EventHandler<ProblemEventArgs>? ProblemAdded;
    public static event EventHandler? ProblemsCleared;

    public static IReadOnlyList<ProblemItem> Problems => _problems.AsReadOnly();

    public static void AddProblem(ProblemItem problem)
    {
        _problems.Add(problem);
        ProblemAdded?.Invoke(null, new ProblemEventArgs(problem));
    }

    public static void Clear()
    {
        _problems.Clear();
        ProblemsCleared?.Invoke(null, EventArgs.Empty);
    }
}
