using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ClaudeCodeGUI.Themes
{
    /// <summary>
    /// 主题管理器 - 支持多主题动态切换
    /// </summary>
    public class ThemeManager
    {
        private static ThemeManager? _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();

        private readonly Dictionary<string, Theme> _themes = new();
        private Theme? _currentTheme;
        private string _currentThemeName = "";

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public Theme CurrentTheme => _currentTheme ?? Theme.CreateDefaultDark();
        public string CurrentThemeName => _currentThemeName;
        public IReadOnlyDictionary<string, Theme> AvailableThemes => _themes;

        private ThemeManager()
        {
            RegisterBuiltInThemes();
            LoadSavedTheme();
        }

        private void RegisterBuiltInThemes()
        {
            // 默认深色主题 - 专业开发者风格
            RegisterTheme("Default", Theme.CreateDefaultDark());

            // GitHub 风格
            RegisterTheme("GitHub", Theme.CreateGitHub());

            // Apple 风格
            RegisterTheme("Apple", Theme.CreateApple());

            // Monokai 风格
            RegisterTheme("Monokai", Theme.CreateMonokai());

            // Dracula 风格
            RegisterTheme("Dracula", Theme.CreateDracula());

            // Warm Paper 风格 - 温暖的纸张色调浅色主题
            RegisterTheme("WarmPaper", Theme.CreateWarmPaper());
        }

        public void RegisterTheme(string name, Theme theme)
        {
            _themes[name] = theme;
        }

        public bool SwitchTheme(string name)
        {
            if (!_themes.TryGetValue(name, out var theme))
                return false;

            _currentTheme = theme;
            _currentThemeName = name;
            ApplyTheme(theme);
            SaveThemePreference(name);
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(name, theme));
            return true;
        }

        private void ApplyTheme(Theme theme)
        {
            // 更新 Application 资源
            var app = Application.Current;
            if (app == null) return;

            // 背景颜色
            app.Resources["ThemeBackground"] = theme.Background;
            app.Resources["ThemeSurface1"] = theme.Surface1;
            app.Resources["ThemeSurface2"] = theme.Surface2;
            app.Resources["ThemeSurface3"] = theme.Surface3;
            app.Resources["ThemeSurface4"] = theme.Surface4;

            // 边框颜色
            app.Resources["ThemeBorder1"] = theme.Border1;
            app.Resources["ThemeBorder2"] = theme.Border2;
            app.Resources["ThemeBorder3"] = theme.Border3;

            // 文字颜色
            app.Resources["ThemeTextPrimary"] = theme.TextPrimary;
            app.Resources["ThemeTextSecondary"] = theme.TextSecondary;
            app.Resources["ThemeTextMuted"] = theme.TextMuted;
            app.Resources["ThemeTextDisabled"] = theme.TextDisabled;

            // 功能颜色
            app.Resources["ThemeAccent"] = theme.Accent;
            app.Resources["ThemeAccentHover"] = theme.AccentHover;
            app.Resources["ThemeSuccess"] = theme.Success;
            app.Resources["ThemeWarning"] = theme.Warning;
            app.Resources["ThemeError"] = theme.Error;
            app.Resources["ThemeInfo"] = theme.Info;

            // 特殊颜色
            app.Resources["ThemeBrand"] = theme.Brand;
            app.Resources["ThemeUserBg"] = theme.UserBubbleBg;
            app.Resources["ThemeAssistantBg"] = theme.AssistantBubbleBg;
            app.Resources["ThemeSkillChipBg"] = theme.SkillChipBg;
            app.Resources["ThemeSkillChipText"] = theme.SkillChipText;

            // 功能色背景
            app.Resources["ThemeSuccessBg"] = theme.SuccessBg;
            app.Resources["ThemeErrorBg"] = theme.ErrorBg;
            app.Resources["ThemeWarningBg"] = theme.WarningBg;

            // 窗口控制按钮颜色
            app.Resources["ThemeWindowBtnBg"] = theme.WindowButtonBg;
            app.Resources["ThemeWindowBtnBgHover"] = theme.WindowButtonBgHover;
            app.Resources["ThemeWindowBtnCloseHover"] = theme.WindowButtonCloseHover;

            // 滚动条颜色
            app.Resources["ThemeScrollBarThumb"] = theme.ScrollBarThumb;
            app.Resources["ThemeScrollBarThumbHover"] = theme.ScrollBarThumbHover;

            // 输入框颜色
            app.Resources["ThemeInputBorder"] = theme.InputBorder;
            app.Resources["ThemeInputBorderFocus"] = theme.InputBorderFocus;
            app.Resources["ThemeInputBg"] = theme.InputBg;

            // 代码块
            app.Resources["ThemeCodeBg"] = theme.CodeBg;
            app.Resources["ThemeCodeLineNum"] = theme.CodeLineNum;
            app.Resources["ThemeCodeHeaderBg"] = theme.CodeHeaderBg;
            app.Resources["ThemeCodeBorder"] = theme.CodeBorder;

            // 语法高亮
            app.Resources["SyntaxKeyword"] = theme.SyntaxKeyword;
            app.Resources["SyntaxString"] = theme.SyntaxString;
            app.Resources["SyntaxComment"] = theme.SyntaxComment;
            app.Resources["SyntaxNumber"] = theme.SyntaxNumber;
            app.Resources["SyntaxType"] = theme.SyntaxType;
            app.Resources["SyntaxMethod"] = theme.SyntaxMethod;
            app.Resources["SyntaxOperator"] = theme.SyntaxOperator;

            // 工具调用
            app.Resources["ThemeToolCallBg"] = theme.ToolCallBg;
            app.Resources["ThemeToolCallBorder"] = theme.ToolCallBorder;
            app.Resources["ThemeToolCallRunning"] = theme.ToolCallRunning;
            app.Resources["ThemeToolCallPending"] = theme.ToolCallPending;
            app.Resources["ThemeToolCallDone"] = theme.ToolCallDone;

            // 气泡增强
            app.Resources["ThemeBubbleHeaderFg"] = theme.BubbleHeaderFg;
            app.Resources["ThemeBubbleTimeFg"] = theme.BubbleTimeFg;
            app.Resources["ThemeBubbleAccentBorder"] = theme.BubbleAccentBorder;

            // 审批对话框
            app.Resources["ThemeApprovalBg"] = theme.ApprovalBg;
            app.Resources["ThemeApprovalBorder"] = theme.ApprovalBorder;

            // 变更文件摘要
            app.Resources["ThemeDiffAdded"] = theme.DiffAdded;
            app.Resources["ThemeDiffRemoved"] = theme.DiffRemoved;
        }

        private void SaveThemePreference(string name)
        {
            try
            {
                var path = GetThemeConfigPath();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, name);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SaveThemePreference] {ex.Message}"); }
        }

        private void LoadSavedTheme()
        {
            try
            {
                var path = GetThemeConfigPath();
                if (System.IO.File.Exists(path))
                {
                    var savedName = System.IO.File.ReadAllText(path).Trim();
                    if (_themes.ContainsKey(savedName))
                    {
                        SwitchTheme(savedName);
                        return;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LoadSavedTheme] {ex.Message}"); }

            // 默认使用 Default 主题
            SwitchTheme("Default");
        }

        private static string GetThemeConfigPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return System.IO.Path.Combine(home, ".claude", "gui-theme.txt");
        }

        /// <summary>
        /// 获取颜色资源（便捷方法）
        /// </summary>
        public static SolidColorBrush GetColor(string resourceKey)
        {
            var app = Application.Current;
            if (app?.Resources[resourceKey] is SolidColorBrush brush)
                return brush;
            return new SolidColorBrush(Colors.Transparent);
        }

        /// <summary>
        /// 创建纯色画刷并冻结（性能优化）
        /// </summary>
        public static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 从十六进制字符串创建颜色
        /// </summary>
        public static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }

    public class ThemeChangedEventArgs : EventArgs
    {
        public string ThemeName { get; }
        public Theme Theme { get; }

        public ThemeChangedEventArgs(string name, Theme theme)
        {
            ThemeName = name;
            Theme = theme;
        }
    }
}