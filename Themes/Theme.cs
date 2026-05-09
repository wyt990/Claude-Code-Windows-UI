using System.Windows.Media;

namespace ClaudeCodeGUI.Themes
{
    /// <summary>
    /// 主题定义 - 包含完整的颜色系统
    /// </summary>
    public class Theme
    {
        // ═══ 背景层级 ═══
        public SolidColorBrush Background { get; set; } = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));
        public SolidColorBrush Surface1 { get; set; } = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22));  // 输入框、卡片
        public SolidColorBrush Surface2 { get; set; } = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));  // 工具栏、侧边栏头部
        public SolidColorBrush Surface3 { get; set; } = new SolidColorBrush(Color.FromRgb(0x21, 0x26, 0x2D));  // 悬浮状态
        public SolidColorBrush Surface4 { get; set; } = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));  // 最浅背景

        // ═══ 边框层级 ═══
        public SolidColorBrush Border1 { get; set; } = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));  // 主边框
        public SolidColorBrush Border2 { get; set; } = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));  // 次级边框
        public SolidColorBrush Border3 { get; set; } = new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58));  // 高亮边框

        // ═══ 文字层级 ═══
        public SolidColorBrush TextPrimary { get; set; } = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));  // 主要文字
        public SolidColorBrush TextSecondary { get; set; } = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));  // 次要文字
        public SolidColorBrush TextMuted { get; set; } = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));  // 辅助文字
        public SolidColorBrush TextDisabled { get; set; } = new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58));  // 禁用文字

        // ═══ 功能颜色 ═══
        public SolidColorBrush Accent { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));  // 蓝色强调
        public SolidColorBrush AccentHover { get; set; } = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
        public SolidColorBrush Success { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));  // 绿色成功
        public SolidColorBrush Warning { get; set; } = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));  // 黄色警告
        public SolidColorBrush Error { get; set; } = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));  // 红色错误
        public SolidColorBrush Info { get; set; } = new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4));  // 青色信息

        // ═══ 特殊颜色 ═══
        public SolidColorBrush Brand { get; set; } = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35));  // 品牌橙色
        public SolidColorBrush UserBubbleBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x0A, 0x1F, 0x38));  // 用户消息背景
        public SolidColorBrush AssistantBubbleBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));  // AI消息背景
        public SolidColorBrush SkillChipBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x0A, 0x1F, 0x35));  // 技能标签背景
        public SolidColorBrush SkillChipText { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));  // 技能标签文字

        // ═══ 功能色背景 ═══
        public SolidColorBrush SuccessBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A));  // 成功按钮背景
        public SolidColorBrush ErrorBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A));  // 错误按钮背景
        public SolidColorBrush WarningBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x1A));  // 警告按钮背景

        // ═══ 窗口控制 ═══
        public SolidColorBrush WindowButtonBg { get; set; } = new SolidColorBrush(Colors.Transparent);
        public SolidColorBrush WindowButtonBgHover { get; set; } = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));
        public SolidColorBrush WindowButtonCloseHover { get; set; } = new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A));

        // ═══ 滚动条 ═══
        public SolidColorBrush ScrollBarThumb { get; set; } = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));
        public SolidColorBrush ScrollBarThumbHover { get; set; } = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

        // ═══ 输入框 ═══
        public SolidColorBrush InputBorder { get; set; } = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
        public SolidColorBrush InputBorderFocus { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        public SolidColorBrush InputBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22));

        // ═══ 新增：代码块颜色 ═══
        public SolidColorBrush CodeBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17));
        public SolidColorBrush CodeLineNum { get; set; } = new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58));
        public SolidColorBrush CodeHeaderBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22));
        public SolidColorBrush CodeBorder { get; set; } = new SolidColorBrush(Color.FromRgb(0x21, 0x26, 0x2D));

        // ═══ 新增：语法高亮颜色 ═══
        public SolidColorBrush SyntaxKeyword { get; set; } = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        public SolidColorBrush SyntaxString { get; set; } = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
        public SolidColorBrush SyntaxComment { get; set; } = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
        public SolidColorBrush SyntaxNumber { get; set; } = new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8));
        public SolidColorBrush SyntaxType { get; set; } = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
        public SolidColorBrush SyntaxMethod { get; set; } = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
        public SolidColorBrush SyntaxOperator { get; set; } = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));

        // ═══ 新增：工具调用颜色 ═══
        public SolidColorBrush ToolCallBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22));
        public SolidColorBrush ToolCallBorder { get; set; } = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
        public SolidColorBrush ToolCallRunning { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        public SolidColorBrush ToolCallPending { get; set; } = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        public SolidColorBrush ToolCallDone { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

        // ═══ 新增：气泡增强颜色 ═══
        public SolidColorBrush BubbleHeaderFg { get; set; } = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        public SolidColorBrush BubbleTimeFg { get; set; } = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        public SolidColorBrush BubbleAccentBorder { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));

        // ═══ 新增：审批对话框颜色 ═══
        public SolidColorBrush ApprovalBg { get; set; } = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
        public SolidColorBrush ApprovalBorder { get; set; } = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));

        // ═══ 新增：变更文件摘要颜色 ═══
        public SolidColorBrush DiffAdded { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        public SolidColorBrush DiffRemoved { get; set; } = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

        // ═══ 内置主题创建 ═══

        /// <summary>
        /// 默认深色主题 - 专业开发者风格（VS Code / Cursor）
        /// </summary>
        public static Theme CreateDefaultDark()
        {
            var theme = new Theme();
            FreezeAllBrushes(theme);
            return theme;
        }

        /// <summary>
        /// GitHub 风格 - 与 GitHub Dark 保持一致
        /// </summary>
        public static Theme CreateGitHub()
        {
            var theme = new Theme
            {
                Background = CreateBrush("#010409"),
                Surface1 = CreateBrush("#0D1117"),
                Surface2 = CreateBrush("#161B22"),
                Surface3 = CreateBrush("#21262D"),
                Surface4 = CreateBrush("#30363D"),

                Border1 = CreateBrush("#21262D"),
                Border2 = CreateBrush("#30363D"),
                Border3 = CreateBrush("#484F58"),

                TextPrimary = CreateBrush("#E6EDF3"),
                TextSecondary = CreateBrush("#8B949E"),
                TextMuted = CreateBrush("#6E7681"),
                TextDisabled = CreateBrush("#484F58"),

                Accent = CreateBrush("#58A6FF"),
                AccentHover = CreateBrush("#79C0FF"),
                Success = CreateBrush("#3FB950"),
                Warning = CreateBrush("#D29922"),
                Error = CreateBrush("#F85149"),
                Info = CreateBrush("#39C5CF"),

                Brand = CreateBrush("#FF6B35"),
                UserBubbleBg = CreateBrush("#0D1117"),
                AssistantBubbleBg = CreateBrush("#161B22"),
                SkillChipBg = CreateBrush("#1F3D5C"),
                SkillChipText = CreateBrush("#58A6FF"),

                SuccessBg = CreateBrush("#1A3A1A"),
                ErrorBg = CreateBrush("#3A1A1A"),
                WarningBg = CreateBrush("#3A3A1A"),

                ScrollBarThumb = CreateBrush("#30363D"),
                ScrollBarThumbHover = CreateBrush("#484F58"),

                InputBorder = CreateBrush("#21262D"),
                InputBorderFocus = CreateBrush("#58A6FF"),
                InputBg = CreateBrush("#0D1117"),

                // 代码块
                CodeBg = CreateBrush("#010409"),
                CodeLineNum = CreateBrush("#21262D"),
                CodeHeaderBg = CreateBrush("#0D1117"),
                CodeBorder = CreateBrush("#21262D"),
                // 语法高亮
                SyntaxKeyword = CreateBrush("#58A6FF"),
                SyntaxString = CreateBrush("#A5D6FF"),
                SyntaxComment = CreateBrush("#8B949E"),
                SyntaxNumber = CreateBrush("#79C0FF"),
                SyntaxType = CreateBrush("#3FB950"),
                SyntaxMethod = CreateBrush("#D2A8FF"),
                SyntaxOperator = CreateBrush("#E6EDF3"),
                // 工具调用
                ToolCallBg = CreateBrush("#0D1117"),
                ToolCallBorder = CreateBrush("#21262D"),
                ToolCallRunning = CreateBrush("#58A6FF"),
                ToolCallPending = CreateBrush("#D29922"),
                ToolCallDone = CreateBrush("#3FB950"),
                // 气泡增强
                BubbleHeaderFg = CreateBrush("#E6EDF3"),
                BubbleTimeFg = CreateBrush("#6E7681"),
                BubbleAccentBorder = CreateBrush("#58A6FF"),
                // 审批对话框
                ApprovalBg = CreateBrush("#161B22"),
                ApprovalBorder = CreateBrush("#21262D"),
                // 变更文件摘要
                DiffAdded = CreateBrush("#3FB950"),
                DiffRemoved = CreateBrush("#F85149"),
            };
            FreezeAllBrushes(theme);
            return theme;
        }

        /// <summary>
        /// Apple 风格 - macOS Dark Mode 风格
        /// </summary>
        public static Theme CreateApple()
        {
            var theme = new Theme
            {
                Background = CreateBrush("#000000"),
                Surface1 = CreateBrush("#1C1C1E"),
                Surface2 = CreateBrush("#2C2C2E"),
                Surface3 = CreateBrush("#3A3A3C"),
                Surface4 = CreateBrush("#48484A"),

                Border1 = CreateBrush("#2C2C2E"),
                Border2 = CreateBrush("#3A3A3C"),
                Border3 = CreateBrush("#636366"),

                TextPrimary = CreateBrush("#EBEBF0"),
                TextSecondary = CreateBrush("#8E8E93"),
                TextMuted = CreateBrush("#636366"),
                TextDisabled = CreateBrush("#48484A"),

                Accent = CreateBrush("#0A84FF"),
                AccentHover = CreateBrush("#409CFF"),
                Success = CreateBrush("#30D158"),
                Warning = CreateBrush("#FFD60A"),
                Error = CreateBrush("#FF453A"),
                Info = CreateBrush("#64D2FF"),

                Brand = CreateBrush("#FF6B35"),
                UserBubbleBg = CreateBrush("#0A1F38"),
                AssistantBubbleBg = CreateBrush("#1C1C1E"),
                SkillChipBg = CreateBrush("#0A1F35"),
                SkillChipText = CreateBrush("#0A84FF"),

                SuccessBg = CreateBrush("#1A3A1A"),
                ErrorBg = CreateBrush("#3A1A1A"),
                WarningBg = CreateBrush("#3A3A1A"),

                ScrollBarThumb = CreateBrush("#3A3A3C"),
                ScrollBarThumbHover = CreateBrush("#636366"),

                InputBorder = CreateBrush("#3A3A3C"),
                InputBorderFocus = CreateBrush("#0A84FF"),
                InputBg = CreateBrush("#1C1C1E"),

                // 代码块
                CodeBg = CreateBrush("#000000"),
                CodeLineNum = CreateBrush("#48484A"),
                CodeHeaderBg = CreateBrush("#1C1C1E"),
                CodeBorder = CreateBrush("#2C2C2E"),
                // 语法高亮
                SyntaxKeyword = CreateBrush("#0A84FF"),
                SyntaxString = CreateBrush("#FF9F0A"),
                SyntaxComment = CreateBrush("#636366"),
                SyntaxNumber = CreateBrush("#BF5AF2"),
                SyntaxType = CreateBrush("#30D158"),
                SyntaxMethod = CreateBrush("#FFD60A"),
                SyntaxOperator = CreateBrush("#EBEBF0"),
                // 工具调用
                ToolCallBg = CreateBrush("#1C1C1E"),
                ToolCallBorder = CreateBrush("#3A3A3C"),
                ToolCallRunning = CreateBrush("#0A84FF"),
                ToolCallPending = CreateBrush("#FFD60A"),
                ToolCallDone = CreateBrush("#30D158"),
                // 气泡增强
                BubbleHeaderFg = CreateBrush("#EBEBF0"),
                BubbleTimeFg = CreateBrush("#636366"),
                BubbleAccentBorder = CreateBrush("#0A84FF"),
                // 审批对话框
                ApprovalBg = CreateBrush("#2C2C2E"),
                ApprovalBorder = CreateBrush("#3A3A3C"),
                // 变更文件摘要
                DiffAdded = CreateBrush("#30D158"),
                DiffRemoved = CreateBrush("#FF453A"),
            };
            FreezeAllBrushes(theme);
            return theme;
        }

        /// <summary>
        /// Monokai 风格 - 经典代码编辑器主题
        /// </summary>
        public static Theme CreateMonokai()
        {
            var theme = new Theme
            {
                Background = CreateBrush("#272822"),
                Surface1 = CreateBrush("#3E3D32"),
                Surface2 = CreateBrush("#49483E"),
                Surface3 = CreateBrush("#55554F"),
                Surface4 = CreateBrush("#75715E"),

                Border1 = CreateBrush("#49483E"),
                Border2 = CreateBrush("#55554F"),
                Border3 = CreateBrush("#75715E"),

                TextPrimary = CreateBrush("#F8F8F2"),
                TextSecondary = CreateBrush("#A6A69F"),
                TextMuted = CreateBrush("#75715E"),
                TextDisabled = CreateBrush("#49483E"),

                Accent = CreateBrush("#66D9EF"),
                AccentHover = CreateBrush("#7EE8F6"),
                Success = CreateBrush("#A6E22E"),
                Warning = CreateBrush("#E6DB74"),
                Error = CreateBrush("#F92672"),
                Info = CreateBrush("#AE81FF"),

                Brand = CreateBrush("#FD971F"),
                UserBubbleBg = CreateBrush("#3E3D32"),
                AssistantBubbleBg = CreateBrush("#49483E"),
                SkillChipBg = CreateBrush("#3E3D32"),
                SkillChipText = CreateBrush("#66D9EF"),

                SuccessBg = CreateBrush("#3E3D32"),
                ErrorBg = CreateBrush("#3E3D32"),
                WarningBg = CreateBrush("#3E3D32"),

                ScrollBarThumb = CreateBrush("#55554F"),
                ScrollBarThumbHover = CreateBrush("#75715E"),

                InputBorder = CreateBrush("#49483E"),
                InputBorderFocus = CreateBrush("#66D9EF"),
                InputBg = CreateBrush("#3E3D32"),

                // 代码块
                CodeBg = CreateBrush("#272822"),
                CodeLineNum = CreateBrush("#75715E"),
                CodeHeaderBg = CreateBrush("#3E3D32"),
                CodeBorder = CreateBrush("#49483E"),
                // 语法高亮
                SyntaxKeyword = CreateBrush("#66D9EF"),
                SyntaxString = CreateBrush("#E6DB74"),
                SyntaxComment = CreateBrush("#75715E"),
                SyntaxNumber = CreateBrush("#AE81FF"),
                SyntaxType = CreateBrush("#A6E22E"),
                SyntaxMethod = CreateBrush("#F8F8F2"),
                SyntaxOperator = CreateBrush("#F8F8F2"),
                // 工具调用
                ToolCallBg = CreateBrush("#3E3D32"),
                ToolCallBorder = CreateBrush("#49483E"),
                ToolCallRunning = CreateBrush("#66D9EF"),
                ToolCallPending = CreateBrush("#E6DB74"),
                ToolCallDone = CreateBrush("#A6E22E"),
                // 气泡增强
                BubbleHeaderFg = CreateBrush("#F8F8F2"),
                BubbleTimeFg = CreateBrush("#75715E"),
                BubbleAccentBorder = CreateBrush("#66D9EF"),
                // 审批对话框
                ApprovalBg = CreateBrush("#49483E"),
                ApprovalBorder = CreateBrush("#55554F"),
                // 变更文件摘要
                DiffAdded = CreateBrush("#A6E22E"),
                DiffRemoved = CreateBrush("#F92672"),
            };
            FreezeAllBrushes(theme);
            return theme;
        }

        /// <summary>
        /// Dracula 风格 - 热门暗色主题
        /// </summary>
        public static Theme CreateDracula()
        {
            var theme = new Theme
            {
                Background = CreateBrush("#282A36"),
                Surface1 = CreateBrush("#44475A"),
                Surface2 = CreateBrush("#44475A"),
                Surface3 = CreateBrush("#5A5C6E"),
                Surface4 = CreateBrush("#6272A4"),

                Border1 = CreateBrush("#44475A"),
                Border2 = CreateBrush("#6272A4"),
                Border3 = CreateBrush("#8BE9FD"),

                TextPrimary = CreateBrush("#F8F8F2"),
                TextSecondary = CreateBrush("#BFBFBF"),
                TextMuted = CreateBrush("#6272A4"),
                TextDisabled = CreateBrush("#44475A"),

                Accent = CreateBrush("#BD93F9"),
                AccentHover = CreateBrush("#FF79C6"),
                Success = CreateBrush("#50FA7B"),
                Warning = CreateBrush("#F1FA8C"),
                Error = CreateBrush("#FF5555"),
                Info = CreateBrush("#8BE9FD"),

                Brand = CreateBrush("#FF79C6"),
                UserBubbleBg = CreateBrush("#44475A"),
                AssistantBubbleBg = CreateBrush("#282A36"),
                SkillChipBg = CreateBrush("#44475A"),
                SkillChipText = CreateBrush("#BD93F9"),

                SuccessBg = CreateBrush("#44475A"),
                ErrorBg = CreateBrush("#44475A"),
                WarningBg = CreateBrush("#44475A"),

                ScrollBarThumb = CreateBrush("#44475A"),
                ScrollBarThumbHover = CreateBrush("#6272A4"),

                InputBorder = CreateBrush("#44475A"),
                InputBorderFocus = CreateBrush("#BD93F9"),
                InputBg = CreateBrush("#282A36"),

                // 代码块
                CodeBg = CreateBrush("#282A36"),
                CodeLineNum = CreateBrush("#6272A4"),
                CodeHeaderBg = CreateBrush("#44475A"),
                CodeBorder = CreateBrush("#44475A"),
                // 语法高亮
                SyntaxKeyword = CreateBrush("#FF79C6"),
                SyntaxString = CreateBrush("#F1FA8C"),
                SyntaxComment = CreateBrush("#6272A4"),
                SyntaxNumber = CreateBrush("#BD93F9"),
                SyntaxType = CreateBrush("#50FA7B"),
                SyntaxMethod = CreateBrush("#8BE9FD"),
                SyntaxOperator = CreateBrush("#F8F8F2"),
                // 工具调用
                ToolCallBg = CreateBrush("#44475A"),
                ToolCallBorder = CreateBrush("#6272A4"),
                ToolCallRunning = CreateBrush("#8BE9FD"),
                ToolCallPending = CreateBrush("#F1FA8C"),
                ToolCallDone = CreateBrush("#50FA7B"),
                // 气泡增强
                BubbleHeaderFg = CreateBrush("#F8F8F2"),
                BubbleTimeFg = CreateBrush("#6272A4"),
                BubbleAccentBorder = CreateBrush("#BD93F9"),
                // 审批对话框
                ApprovalBg = CreateBrush("#44475A"),
                ApprovalBorder = CreateBrush("#6272A4"),
                // 变更文件摘要
                DiffAdded = CreateBrush("#50FA7B"),
                DiffRemoved = CreateBrush("#FF5555"),
            };
            FreezeAllBrushes(theme);
            return theme;
        }

        /// <summary>
        /// Warm Paper 风格 - 温暖的纸张色调浅色主题
        /// </summary>
        public static Theme CreateWarmPaper()
        {
            var theme = new Theme
            {
                Background = CreateBrush("#F6F2E8"),
                Surface1 = CreateBrush("#EDE8DC"),
                Surface2 = CreateBrush("#E4DECF"),
                Surface3 = CreateBrush("#DAD2C0"),
                Surface4 = CreateBrush("#CFC5B0"),

                Border1 = CreateBrush("#D4C9B5"),
                Border2 = CreateBrush("#C4B8A0"),
                Border3 = CreateBrush("#A89B80"),

                TextPrimary = CreateBrush("#2D2A24"),
                TextSecondary = CreateBrush("#5C5650"),
                TextMuted = CreateBrush("#8A8478"),
                TextDisabled = CreateBrush("#A89B80"),

                Accent = CreateBrush("#4A7C59"),
                AccentHover = CreateBrush("#5A9469"),
                Success = CreateBrush("#4A7C59"),
                Warning = CreateBrush("#B8860B"),
                Error = CreateBrush("#8B4513"),
                Info = CreateBrush("#4682B4"),

                Brand = CreateBrush("#8B4513"),
                UserBubbleBg = CreateBrush("#E4DECF"),
                AssistantBubbleBg = CreateBrush("#EDE8DC"),
                SkillChipBg = CreateBrush("#D4C9B5"),
                SkillChipText = CreateBrush("#4A7C59"),

                SuccessBg = CreateBrush("#D4E4D4"),
                ErrorBg = CreateBrush("#E8D4D4"),
                WarningBg = CreateBrush("#E8E4D4"),

                ScrollBarThumb = CreateBrush("#C4B8A0"),
                ScrollBarThumbHover = CreateBrush("#A89B80"),

                InputBorder = CreateBrush("#D4C9B5"),
                InputBorderFocus = CreateBrush("#4A7C59"),
                InputBg = CreateBrush("#EDE8DC"),

                // 代码块
                CodeBg = CreateBrush("#F6F2E8"),
                CodeLineNum = CreateBrush("#C4B8A0"),
                CodeHeaderBg = CreateBrush("#EDE8DC"),
                CodeBorder = CreateBrush("#D4C9B5"),
                // 语法高亮
                SyntaxKeyword = CreateBrush("#4A7C59"),
                SyntaxString = CreateBrush("#8B4513"),
                SyntaxComment = CreateBrush("#8A8478"),
                SyntaxNumber = CreateBrush("#4682B4"),
                SyntaxType = CreateBrush("#5A9469"),
                SyntaxMethod = CreateBrush("#B8860B"),
                SyntaxOperator = CreateBrush("#2D2A24"),
                // 工具调用
                ToolCallBg = CreateBrush("#EDE8DC"),
                ToolCallBorder = CreateBrush("#C4B8A0"),
                ToolCallRunning = CreateBrush("#4682B4"),
                ToolCallPending = CreateBrush("#B8860B"),
                ToolCallDone = CreateBrush("#4A7C59"),
                // 气泡增强
                BubbleHeaderFg = CreateBrush("#2D2A24"),
                BubbleTimeFg = CreateBrush("#8A8478"),
                BubbleAccentBorder = CreateBrush("#4A7C59"),
                // 审批对话框
                ApprovalBg = CreateBrush("#EDE8DC"),
                ApprovalBorder = CreateBrush("#D4C9B5"),
                // 变更文件摘要
                DiffAdded = CreateBrush("#4A7C59"),
                DiffRemoved = CreateBrush("#8B4513"),
            };
            FreezeAllBrushes(theme);
            return theme;
        }

        // ═══ 辅助方法 ═══

        private static SolidColorBrush CreateBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static void FreezeAllBrushes(Theme theme)
        {
            // 冻结所有画刷以优化性能
            var properties = typeof(Theme).GetProperties();
            foreach (var prop in properties)
            {
                if (prop.PropertyType == typeof(SolidColorBrush) && prop.CanRead)
                {
                    var brush = prop.GetValue(theme) as SolidColorBrush;
                    if (brush != null && !brush.IsFrozen)
                        brush.Freeze();
                }
            }
        }
    }
}