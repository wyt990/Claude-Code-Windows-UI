using System.Windows;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 初始化主题系统
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;
            base.OnStartup(e);
        }

        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            // 主题切换时的全局响应（如需通知所有窗口）
            foreach (Window window in Current.Windows)
            {
                if (window is MainWindow mainWin)
                    mainWin.OnThemeChanged(e);
            }
        }
    }
}
