using System;
using System.Windows;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局异常捕获
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"未处理 AppDomain 异常: {ex?.Message}\n\n{ex?.StackTrace}",
                    "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                var ex = args.Exception.InnerException ?? args.Exception;
                MessageBox.Show($"未处理异常: {ex.Message}\n\n{ex.StackTrace}",
                    "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

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
