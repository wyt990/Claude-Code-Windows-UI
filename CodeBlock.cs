using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>
/// 代码语法高亮块 — 行号 + 着色 + 复制按钮
/// 支持 6 种语言：csharp, python, javascript, xml/html, json, bash
/// </summary>
public static class CodeBlock
{
    private static readonly Dictionary<string, HashSet<string>> KeywordsByLang = new()
    {
        ["csharp"] = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "var", "virtual", "void", "volatile", "while",
        },
        ["python"] = new()
        {
            "False", "None", "True", "and", "as", "assert", "async", "await", "break",
            "class", "continue", "def", "del", "elif", "else", "except", "finally",
            "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal",
            "not", "or", "pass", "raise", "return", "try", "while", "with", "yield",
        },
        ["javascript"] = new()
        {
            "async", "await", "break", "case", "catch", "class", "const", "continue",
            "debugger", "default", "delete", "do", "else", "export", "extends", "false",
            "finally", "for", "function", "if", "import", "in", "instanceof", "let",
            "new", "null", "of", "return", "super", "switch", "this", "throw", "true",
            "try", "typeof", "undefined", "var", "void", "while", "with", "yield",
        },
        ["bash"] = new()
        {
            "if", "then", "else", "elif", "fi", "for", "while", "do", "done", "case",
            "esac", "in", "function", "return", "break", "continue", "exit", "export",
            "local", "readonly", "declare", "typeset", "unset", "echo", "cd", "pwd",
            "mkdir", "rm", "cp", "mv", "ls", "cat", "grep", "sed", "awk", "source",
            "true", "false",
        },
    };

    private static readonly HashSet<string> JsonKeywords = new() { "true", "false", "null" };

    /// <summary>构建语法高亮的代码块</summary>
    public static Border Build(string code, string? language = null)
    {
        string displayLang = FormatLanguage(language);
        string normalized = NormalizeLanguage(language);
        lineCount = CountLines(code);

        // ── 主体 ──
        bool isDiff = normalized == "diff" || AutoDetectDiff(code);
        if (isDiff) displayLang = "Diff";
        var bodyGrid = isDiff ? BuildDiffBody(code) : BuildBody(code, normalized);
        var header = BuildHeader(displayLang, code);

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(bodyGrid);

        return new Border
        {
            Background = ThemeManager.GetColor("ThemeCodeBg"),
            BorderBrush = ThemeManager.GetColor("ThemeCodeBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Margin = new Thickness(0, 4, 0, 4),
            Child = stack,
        };
    }

    // ── line count cache for BuildBody ──
    // (static method, not instance — safe single-use per Build call)
    private static int lineCount;

    // ── Language helpers ──
    private static string NormalizeLanguage(string? lang)
    {
        if (string.IsNullOrEmpty(lang)) return "";
        return lang.ToLowerInvariant() switch
        {
            "c#" or "csharp" or "cs" => "csharp",
            "py" or "python" => "python",
            "js" or "javascript" or "ecmascript" => "javascript",
            "ts" or "typescript" => "typescript",
            "html" or "htm" or "xml" or "svg" or "xaml" => "xml",
            "json" => "json",
            "bash" or "sh" or "shell" or "zsh" => "bash",
            "diff" or "patch" or "git" => "diff",
            _ => lang?.ToLowerInvariant() ?? "",
        };
    }

    private static string FormatLanguage(string? lang)
    {
        return (lang?.ToLowerInvariant()) switch
        {
            "c#" or "csharp" or "cs" => "C#",
            "py" or "python" => "Python",
            "js" or "javascript" or "ecmascript" => "JavaScript",
            "ts" or "typescript" => "TypeScript",
            "html" or "htm" or "xml" or "svg" or "xaml" => "XML/HTML",
            "json" => "JSON",
            "bash" or "sh" or "shell" or "zsh" => "Bash",
            "diff" or "patch" or "git" => "Diff",
            null or "" => "Code",
            _ => lang!,
        };
    }

    private static int CountLines(string text)
    {
        int count = 1;
        foreach (char c in text)
            if (c == '\n') count++;
        return count;
    }

    // ── Header ──
    private static Border BuildHeader(string displayLang, string rawCode)
    {
        var langLabel = new TextBlock
        {
            Text = displayLang,
            Foreground = ThemeManager.GetColor("ThemeTextSecondary"),
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var copyBtn = new Button
        {
            Content = "复制",
            FontSize = 11,
            Padding = new Thickness(8, 2, 8, 2),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Style = TryFindStyle("FlatButton"),
        };
        copyBtn.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(rawCode);
                copyBtn.Content = "已复制";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5),
                    IsEnabled = true,
                };
                timer.Tick += (_, _) =>
                {
                    copyBtn.Content = "复制";
                    timer.Stop();
                };
                timer.Start();
            }
            catch { /* clipboard access denied */ }
        };

        var headerGrid = new Grid
        {
            Margin = new Thickness(12, 6, 12, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        Grid.SetColumn(langLabel, 0);
        Grid.SetColumn(copyBtn, 1);
        headerGrid.Children.Add(langLabel);
        headerGrid.Children.Add(copyBtn);

        return new Border
        {
            Background = ThemeManager.GetColor("ThemeCodeHeaderBg"),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Child = headerGrid,
        };
    }

    // ── Body: line numbers + highlighted code ──
    private static Border BuildBody(string code, string normalized)
    {
        // Build line numbers
        var lineNumTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 14,
            LineHeight = 22,
            Foreground = ThemeManager.GetColor("ThemeCodeLineNum"),
            TextAlignment = TextAlignment.Right,
            Padding = new Thickness(8, 8, 8, 8),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var lnBuilder = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            if (i > 1) lnBuilder.Append('\n');
            lnBuilder.Append(i);
        }
        lineNumTb.Text = lnBuilder.ToString();

        // Build syntax-highlighted code
        var codeTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 14,
            LineHeight = 22,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(0, 8, 8, 8),
        };

        var tokens = Tokenize(code, normalized);
        foreach (var (tokenText, colorKey) in tokens)
        {
            var run = new Run(tokenText);
            if (colorKey != null)
                run.Foreground = ThemeManager.GetColor(colorKey);
            codeTb.Inlines.Add(run);
        }

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = codeTb,
        };

        var grid = new Grid
        {
            Background = ThemeManager.GetColor("ThemeCodeBg"),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        Grid.SetColumn(lineNumTb, 0);
        Grid.SetColumn(scroller, 1);
        grid.Children.Add(lineNumTb);
        grid.Children.Add(scroller);

        return new Border
        {
            Child = grid,
        };
    }

    // ── Syntax tokenizer ──
    private static IEnumerable<(string Text, string? ColorKey)> Tokenize(string code, string normalized)
    {
        var keywordSet = GetKeywordSet(normalized);
        var tokenizers = BuildPatterns(normalized, keywordSet);

        int pos = 0;
        while (pos < code.Length)
        {
            bool matched = false;
            foreach (var (regex, colorKey) in tokenizers)
            {
                var m = regex.Match(code, pos);
                if (!m.Success || m.Index != pos) continue;

                if (colorKey != null || m.Length > 0)
                    yield return (m.Value, colorKey);
                pos += m.Length;
                matched = true;
                break;
            }

            if (!matched)
            {
                // Advance to next potential match
                int nextPos = code.Length;
                foreach (var (regex, _) in tokenizers)
                {
                    var m = regex.Match(code, pos);
                    if (m.Success && m.Index < nextPos)
                        nextPos = m.Index;
                }

                if (nextPos > pos)
                {
                    yield return (code[pos..nextPos], null);
                    pos = nextPos;
                }
                else
                {
                    yield return (code[pos].ToString(), null);
                    pos++;
                }
            }
        }
    }

    private static HashSet<string>? GetKeywordSet(string normalized)
    {
        if (normalized == "json")
            return JsonKeywords;
        return KeywordsByLang.GetValueOrDefault(normalized);
    }

    private static List<(Regex Regex, string? ColorKey)> BuildPatterns(
        string normalized, HashSet<string>? keywordSet)
    {
        var list = new List<(Regex, string?)>();

        // 1. Strings (highest priority — prevent comment/keyword match inside strings)
        list.Add((new Regex(
            @"""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'",
            RegexOptions.Compiled), "SyntaxString"));

        // 2. Comments (language-specific)
        if (normalized is "python" or "bash")
            list.Add((new Regex(@"#[^\n]*", RegexOptions.Compiled), "SyntaxComment"));
        if (normalized is "csharp" or "javascript" or "typescript")
        {
            list.Add((new Regex(@"//[^\n]*", RegexOptions.Compiled), "SyntaxComment"));
            list.Add((new Regex(@"/\*[\s\S]*?\*/", RegexOptions.Compiled), "SyntaxComment"));
        }
        if (normalized is "xml")
            list.Add((new Regex(@"<!--[\s\S]*?-->", RegexOptions.Compiled), "SyntaxComment"));

        // 3. Keywords
        if (keywordSet is { Count: > 0 })
        {
            var kwPattern = @"\b(" + string.Join("|", keywordSet.Select(Regex.Escape)) + @")\b";
            list.Add((new Regex(kwPattern, RegexOptions.Compiled), "SyntaxKeyword"));
        }

        // 4. Numbers
        list.Add((new Regex(@"\b\d+\.?\d*(?:[eE][+-]?\d+)?\b", RegexOptions.Compiled), "SyntaxNumber"));

        // 5. Types (capitalized identifiers)
        list.Add((new Regex(@"\b[A-Z][a-zA-Z0-9_]*\b", RegexOptions.Compiled), "SyntaxType"));

        // 6. Methods (identifier followed by parenthesized param list)
        list.Add((new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\()", RegexOptions.Compiled), "SyntaxMethod"));

        // 7. Operators / punctuation
        list.Add((new Regex(@"[{}()\[\];:.,<>=!+\-*/%&|^~]+", RegexOptions.Compiled), "SyntaxOperator"));

        return list;
    }

    private static Style? TryFindStyle(string key)
    {
        try { return Application.Current?.FindResource(key) as Style; }
        catch { return null; }
    }

    // ── Diff auto-detection ──
    private static bool AutoDetectDiff(string code)
    {
        // Unified diff headers: "--- " / "+++ " at line start, or "@@ " hunk headers
        int idx;
        if ((idx = code.IndexOf("--- ", StringComparison.Ordinal)) >= 0
            && code.IndexOf("+++ ", idx, StringComparison.Ordinal) >= 0)
            return true;
        if (code.Contains("\n@@ -") || code.StartsWith("@@ -"))
            return true;

        // Lenient: count +/- prefixed lines (Claude often shows diff without headers)
        int plus = 0, minus = 0, nonEmpty = 0;
        foreach (var rawLine in code.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;
            nonEmpty++;
            if (line[0] == '+') plus++;
            else if (line[0] == '-') minus++;
        }
        // Both + and - present, each appearing at least twice
        return plus >= 2 && minus >= 2 && (plus + minus) >= nonEmpty / 2;
    }

    // ── Diff body: line-by-line colored rendering ──
    //
    // Color scheme (GitHub-inspired, works across all themes):
    //   +  prefix  → green  fg (#4ECB71) + dark green bg (#1A3D2E)  — added line
    //   -  prefix  → red    fg (#F97583) + dark red   bg (#3D1F2A)  — removed line
    //   @@ prefix  → cyan   fg (#79C0FF)                             — hunk header
    //   --- / +++  → theme keyword color                              — file path header
    //   no prefix  → theme secondary text color                       — context line
    private static Border BuildDiffBody(string code)
    {
        var codePanel = new StackPanel { Margin = new Thickness(0, 8, 8, 8) };

        var lines = code.Split('\n');
        var lnBuilder = new System.Text.StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) lnBuilder.Append('\n');
            lnBuilder.Append(i + 1);
        }

        // Line numbers
        var lineNumTb = new TextBlock
        {
            Text = lnBuilder.ToString(),
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 14,
            LineHeight = 22,
            Foreground = ThemeManager.GetColor("ThemeCodeLineNum"),
            TextAlignment = TextAlignment.Right,
            Padding = new Thickness(8, 8, 8, 8),
            VerticalAlignment = VerticalAlignment.Top,
        };

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            var row = new Grid
            {
                Height = 22,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(20) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
            };

            // Prefix icon column
            string prefix = line.Length > 0 ? line[0].ToString() : " ";
            var prefixTb = new TextBlock
            {
                Text = prefix,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 14,
                LineHeight = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            // Content
            string content = line.Length > 1 ? line[1..] : "";
            var contentTb = new TextBlock
            {
                Text = content,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 14,
                LineHeight = 22,
                TextWrapping = TextWrapping.NoWrap,
            };

            // Color by prefix
            if (prefix == "+")
            {
                row.Background = H("#1A3D2E");
                prefixTb.Foreground = H("#4ECB71");
                contentTb.Foreground = H("#4ECB71");
            }
            else if (prefix == "-")
            {
                row.Background = H("#3D1F2A");
                prefixTb.Foreground = H("#F97583");
                contentTb.Foreground = H("#F97583");
            }
            else if (prefix == "@")
            {
                prefixTb.Foreground = H("#79C0FF");
                contentTb.Foreground = H("#79C0FF");
            }
            else if (line.StartsWith("---") || line.StartsWith("+++"))
            {
                prefixTb.Foreground = ThemeManager.GetColor("SyntaxKeyword");
                contentTb.Foreground = ThemeManager.GetColor("SyntaxKeyword");
            }
            else
            {
                prefixTb.Foreground = ThemeManager.GetColor("ThemeTextSecondary");
                contentTb.Foreground = ThemeManager.GetColor("ThemeTextSecondary");
            }

            Grid.SetColumn(prefixTb, 0);
            Grid.SetColumn(contentTb, 1);
            row.Children.Add(prefixTb);
            row.Children.Add(contentTb);
            codePanel.Children.Add(row);
        }

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = codePanel,
        };

        var grid = new Grid
        {
            Background = ThemeManager.GetColor("ThemeCodeBg"),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        Grid.SetColumn(lineNumTb, 0);
        Grid.SetColumn(scroller, 1);
        grid.Children.Add(lineNumTb);
        grid.Children.Add(scroller);

        return new Border { Child = grid };
    }

    // ── Color helper (avoid creating brushes inline) ──
    private static SolidColorBrush H(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
