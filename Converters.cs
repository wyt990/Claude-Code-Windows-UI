using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClaudeCodeGUI
{
    public class TaskStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TodoStatus status)
            {
                return status switch
                {
                    TodoStatus.Pending => Application.Current.FindResource("ThemeTextMuted"),
                    TodoStatus.InProgress => Application.Current.FindResource("ThemeAccent"),
                    TodoStatus.Completed => Application.Current.FindResource("ThemeSuccess"),
                    _ => Application.Current.FindResource("ThemeTextSecondary")
                };
            }
            return Application.Current.FindResource("ThemeTextSecondary");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}