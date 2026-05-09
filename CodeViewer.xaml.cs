using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

/// <summary>
/// CodeViewer - Displays file content with line numbers and syntax highlighting.
/// </summary>
public partial class CodeViewer : UserControl
{
    public event EventHandler? CloseRequested;

    private string? _currentFilePath;
    private int? _highlightLine;
    private int _totalLines;
    private bool _isPartialDisplay;

        // Markdown preview components
        private WebBrowser? _markdownView; // 使用WebBrowser控件渲染Markdown
        private bool _isPreviewMode = false;

        // Large file thresholds
        private const int MaxDisplayLines = 500;
    private const long MaxFileSizeBytes = 100 * 1024; // 100KB

    public CodeViewer()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Load and display a file with syntax highlighting.
    /// </summary>
    /// <param name="filePath">Absolute path to the file</param>
    /// <param name="lineNumber">Optional line number to scroll to (1-based)</param>
    public void LoadFile(string filePath, int? lineNumber = null)
    {
        _currentFilePath = filePath;
        _highlightLine = lineNumber;

        // Hide mode buttons by default
        SourceModeBtn.Visibility = Visibility.Collapsed;
        PreviewModeBtn.Visibility = Visibility.Collapsed;

        // Check file exists
        if (!File.Exists(filePath))
        {
            FileNameLabel.Text = filePath;
            LanguageLabel.Text = "Error";
            CodeText.Inlines.Clear();
            CodeText.Inlines.Add(new Run("文件不存在") { Foreground = ThemeManager.GetColor("ThemeError") });
            LineNumbersText.Text = "";
            LargeFileWarning.Visibility = Visibility.Collapsed;
            return;
        }

        // Get file info
        var fileInfo = new FileInfo(filePath);
        FileNameLabel.Text = Path.GetFileName(filePath);

        // Detect language
        string lang = DetectLanguage(filePath);
        LanguageLabel.Text = FormatLanguage(lang);

        // Show mode buttons for markdown files
        if (Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            SourceModeBtn.Visibility = Visibility.Visible;
            PreviewModeBtn.Visibility = Visibility.Visible;
            SwitchToSourceMode();
        }
        else
        {
            SourceModeBtn.Visibility = Visibility.Collapsed;
            PreviewModeBtn.Visibility = Visibility.Collapsed;
            SwitchToSourceMode();
        }

        // Large file protection
        bool isLarge = fileInfo.Length > MaxFileSizeBytes;

        try
        {
            string content;
            if (isLarge)
            {
                // Only read first MaxDisplayLines
                content = ReadFirstLines(filePath, MaxDisplayLines);
                _isPartialDisplay = true;
                _totalLines = CountFileLines(filePath);
                LargeFileWarning.Visibility = Visibility.Visible;
                WarningText.Text = $"文件过大 ({FormatSize(fileInfo.Length)})，仅显示前 {MaxDisplayLines} 行";
                WarningDetail.Text = $"共 {_totalLines} 行";
            }
            else
            {
                content = File.ReadAllText(filePath);
                _isPartialDisplay = false;
                _totalLines = CountLines(content);
                LargeFileWarning.Visibility = Visibility.Collapsed;
            }

            // Render code with syntax highlighting
            RenderCode(content, lang);

            // Render line numbers
            RenderLineNumbers(_isPartialDisplay ? MaxDisplayLines : _totalLines);

            // Scroll to line if specified
            if (lineNumber.HasValue && lineNumber.Value > 0)
            {
                ScrollToLine(lineNumber.Value);
            }
        }
        catch (Exception ex)
        {
            CodeText.Inlines.Clear();
            CodeText.Inlines.Add(new Run($"无法读取文件: {ex.Message}") { Foreground = ThemeManager.GetColor("ThemeError") });
            LineNumbersText.Text = "";
            LargeFileWarning.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Render line numbers.
    /// </summary>
    private void RenderLineNumbers(int lineCount)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            if (i > 1) builder.Append('\n');
            if (_highlightLine.HasValue && i == _highlightLine.Value)
            {
                builder.Append($"▶ {i}");
            }
            else
            {
                builder.Append(i);
            }
        }
        LineNumbersText.Text = builder.ToString();
    }

    /// <summary>
    /// Render code content with syntax highlighting.
    /// </summary>
    private void RenderCode(string code, string language)
    {
        CodeText.Inlines.Clear();

        // Use syntax tokenizer for highlighting
        var tokens = Tokenize(code, language);
        foreach (var (tokenText, colorKey) in tokens)
        {
            var run = new Run(tokenText);
            if (colorKey != null)
                run.Foreground = ThemeManager.GetColor(colorKey);
            CodeText.Inlines.Add(run);
        }
    }

    /// <summary>
    /// Scroll to a specific line.
    /// </summary>
    private void ScrollToLine(int lineNumber)
    {
        // Each line is ~22px height
        double offset = (lineNumber - 1) * 22;
        CodeContentScroller.ScrollToVerticalOffset(offset);
    }

    /// <summary>
    /// Read first N lines from file.
    /// </summary>
    private static string ReadFirstLines(string filePath, int maxLines)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(filePath);
        string? line;
        while ((line = reader.ReadLine()) != null && lines.Count < maxLines)
        {
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Count total lines in file.
    /// </summary>
    private static int CountFileLines(string filePath)
    {
        int count = 0;
        using var reader = new StreamReader(filePath);
        while (reader.ReadLine() != null)
            count++;
        return count;
    }

    /// <summary>
    /// Count lines in text.
    /// </summary>
    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 1;
        foreach (char c in text)
            if (c == '\n') count++;
        return count;
    }

    /// <summary>
    /// Detect language from file extension.
    /// </summary>
    private static string DetectLanguage(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".json" => "json",
            ".xml" or ".xaml" or ".html" or ".htm" or ".svg" => "xml",
            ".sh" or ".bash" or ".zsh" => "bash",
            ".md" or ".markdown" => "markdown",
            ".yaml" or ".yml" => "yaml",
            ".toml" => "toml",
            ".css" => "css",
            ".sql" => "sql",
            ".diff" or ".patch" => "diff",
            _ => "",
        };
    }

    /// <summary>
    /// Format language name for display.
    /// </summary>
    private static string FormatLanguage(string lang)
    {
        return lang switch
        {
            "csharp" => "C#",
            "python" => "Python",
            "javascript" => "JavaScript",
            "typescript" => "TypeScript",
            "xml" => "XML/HTML",
            "json" => "JSON",
            "bash" => "Bash",
            "markdown" => "Markdown",
            "yaml" => "YAML",
            "toml" => "TOML",
            "css" => "CSS",
            "sql" => "SQL",
            "diff" => "Diff",
            "" => "Text",
            _ => lang,
        };
    }

    // ══════════════════════════════════════════════════════════
    // Syntax Highlighting
    // ══════════════════════════════════════════════════════════

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

    private static IEnumerable<(string Text, string? ColorKey)> Tokenize(string code, string language)
    {
        var keywordSet = GetKeywordSet(language);
        var tokenizers = BuildPatterns(language, keywordSet);

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

    private static HashSet<string>? GetKeywordSet(string language)
    {
        if (language == "json")
            return JsonKeywords;
        return KeywordsByLang.GetValueOrDefault(language);
    }

    private static List<(Regex Regex, string? ColorKey)> BuildPatterns(
        string language, HashSet<string>? keywordSet)
    {
        var list = new List<(Regex, string?)>();

        // 1. Strings (highest priority)
        list.Add((new Regex(
            @"""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'",
            RegexOptions.Compiled), "SyntaxString"));

        // 2. Comments (language-specific)
        if (language is "python" or "bash")
            list.Add((new Regex(@"#[^\n]*", RegexOptions.Compiled), "SyntaxComment"));
        if (language is "csharp" or "javascript" or "typescript")
        {
            list.Add((new Regex(@"//[^\n]*", RegexOptions.Compiled), "SyntaxComment"));
            list.Add((new Regex(@"/\*[\s\S]*?\*/", RegexOptions.Compiled), "SyntaxComment"));
        }
        if (language is "xml")
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

        // 6. Methods
        list.Add((new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\()", RegexOptions.Compiled), "SyntaxMethod"));

        // 7. Operators
        list.Add((new Regex(@"[{}()\[\];:.,<>=!+\-*/%&|^~]+", RegexOptions.Compiled), "SyntaxOperator"));

        return list;
    }

    /// <summary>
    /// Format file size in human-readable format.
    /// </summary>
    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
        return $"{bytes / (1024 * 1024)} MB";
    }

        private void SourceMode_Click(object sender, RoutedEventArgs e)
        {
            SwitchToSourceMode();
        }

        private void PreviewMode_Click(object sender, RoutedEventArgs e)
        {
            SwitchToPreviewMode();
        }

        /// <summary>
        /// Switch to source code view mode
        /// </summary>
        private void SwitchToSourceMode()
        {
            if (_isPreviewMode)
            {
                _isPreviewMode = false;
                SourceModeBtn.Background = H("#0A84FF"); // 强调色
                PreviewModeBtn.Background = Brushes.Transparent;
                CodeContentScroller.Visibility = Visibility.Visible;
                _markdownView?.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Switch to markdown preview mode
        /// </summary>
        private void SwitchToPreviewMode()
        {
            if (!_isPreviewMode)
            {
                _isPreviewMode = true;
                SourceModeBtn.Background = Brushes.Transparent;
                PreviewModeBtn.Background = H("#0A84FF"); // 强调色
                CodeContentScroller.Visibility = Visibility.Collapsed;

                // 延迟初始化MarkdownView以提高性能
                if (_markdownView == null)
                {
                    _markdownView = new WebBrowser();
                    ViewerContent.Children.Add(_markdownView);
                }

                _markdownView.Visibility = Visibility.Visible;
                RenderMarkdown();
            }
        }

        /// <summary>
        /// Render markdown content to HTML and display in WebBrowser
        /// </summary>
        private void RenderMarkdown()
        {
            if (_markdownView != null && !string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                var content = File.ReadAllText(_currentFilePath);
                var html = ConvertMarkdownToHtml(content);
                _markdownView.NavigateToString(html);
            }
        }

        /// <summary>
        /// Convert markdown text to HTML
        /// </summary>
        private string ConvertMarkdownToHtml(string markdown)
        {
            // 使用Markdig库将Markdown转换为HTML
            // 现在返回一个简单的HTML包装
            return $"<html><body style='font-family:Segoe UI; font-size:12px; margin:8px;'>{System.Web.HttpUtility.HtmlEncode(markdown).Replace("\\n", "<br/>")}</body></html>";
        }

        /// <summary>
        /// Sync line numbers scroll with code content scroll.
        /// </summary>
        private void CodeContent_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        LineNumbersScroller.ScrollToVerticalOffset(CodeContentScroller.VerticalOffset);
    }

    /// <summary>
    /// Helper method to convert hex color string to SolidColorBrush
    /// </summary>
    private static Brush H(string hex) => new SolidColorBrush(
        (Color)ColorConverter.ConvertFromString(hex));

    /// <summary>
    /// Close button click - raise event for parent to handle.
    /// </summary>
    private void CodeViewer_CloseRequested(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

}
