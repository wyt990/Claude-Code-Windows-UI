using System.Windows;
using System.Windows.Controls;
using ClaudeCodeGUI.Themes;

namespace ClaudeCodeGUI;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        ShowGeneralPage();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (NavList.SelectedIndex)
        {
            case 0:
                ShowGeneralPage();
                break;
            case 1:
                ShowThemePage();
                break;
            case 2:
                ShowAppearancePage();
                break;
            case 3:
                ShowEditorPage();
                break;
        }
    }

    private void ShowGeneralPage()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        stack.Children.Add(CreateSectionTitle("General Settings"));

        stack.Children.Add(CreateSettingLabel("Claude Code executable path:"));
        stack.Children.Add(new TextBox
        {
            Text = "claude",
            Margin = new Thickness(0, 2, 0, 12),
            Padding = new Thickness(6, 4, 6, 4),
            Background = TryFindResource("ThemeInputBg") as System.Windows.Media.Brush,
            Foreground = TryFindResource("ThemeTextPrimary") as System.Windows.Media.Brush,
            BorderBrush = TryFindResource("ThemeInputBorder") as System.Windows.Media.Brush,
            BorderThickness = new Thickness(1)
        });

        stack.Children.Add(CreateSettingLabel("Working directory:"));
        stack.Children.Add(new TextBox
        {
            Text = System.IO.Directory.GetCurrentDirectory(),
            Margin = new Thickness(0, 2, 0, 12),
            Padding = new Thickness(6, 4, 6, 4),
            Background = TryFindResource("ThemeInputBg") as System.Windows.Media.Brush,
            Foreground = TryFindResource("ThemeTextPrimary") as System.Windows.Media.Brush,
            BorderBrush = TryFindResource("ThemeInputBorder") as System.Windows.Media.Brush,
            BorderThickness = new Thickness(1)
        });

        ContentArea.Content = new ScrollViewer
        {
            Content = stack,
            Background = TryFindResource("ThemeBackground") as System.Windows.Media.Brush
        };
    }

    private void ShowThemePage()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        stack.Children.Add(CreateSectionTitle("Theme Settings"));

        stack.Children.Add(CreateSettingLabel("Select theme:"));
        var combo = new ComboBox
        {
            Width = 200,
            Height = 28,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = TryFindResource("ThemeInputBg") as System.Windows.Media.Brush,
            Foreground = TryFindResource("ThemeTextPrimary") as System.Windows.Media.Brush,
            BorderBrush = TryFindResource("ThemeInputBorder") as System.Windows.Media.Brush
        };
        combo.Items.Add("Default");
        combo.Items.Add("GitHub");
        combo.Items.Add("Apple");
        combo.Items.Add("Monokai");
        combo.Items.Add("Dracula");
        combo.Items.Add("WarmPaper");
        combo.SelectedValue = ThemeManager.Instance.CurrentThemeName;

        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedItem is string themeName)
            {
                ThemeManager.Instance.SwitchTheme(themeName);
            }
        };

        stack.Children.Add(combo);

        ContentArea.Content = new ScrollViewer
        {
            Content = stack,
            Background = TryFindResource("ThemeBackground") as System.Windows.Media.Brush
        };
    }

    private void ShowAppearancePage()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        stack.Children.Add(CreateSectionTitle("Appearance Settings"));

        var fontCombo = CreateSettingCombo("Font size:", new[] { "12", "13", "14", "15", "16" }, "14");
        stack.Children.Add(fontCombo);

        ContentArea.Content = new ScrollViewer
        {
            Content = stack,
            Background = TryFindResource("ThemeBackground") as System.Windows.Media.Brush
        };
    }

    private void ShowEditorPage()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        stack.Children.Add(CreateSectionTitle("Editor Settings"));

        stack.Children.Add(CreateSettingLabel("Tab size:"));
        stack.Children.Add(new ComboBox
        {
            Width = 100,
            Height = 28,
            Margin = new Thickness(0, 4, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = TryFindResource("ThemeInputBg") as System.Windows.Media.Brush,
            Foreground = TryFindResource("ThemeTextPrimary") as System.Windows.Media.Brush,
            BorderBrush = TryFindResource("ThemeInputBorder") as System.Windows.Media.Brush,
            SelectedIndex = 0
        });
        ((ComboBox)stack.Children[^1]).Items.Add("2");
        ((ComboBox)stack.Children[^1]).Items.Add("4");
        ((ComboBox)stack.Children[^1]).Items.Add("8");

        stack.Children.Add(CreateSettingLabel("Word wrap:"));
        var wrapCheck = new CheckBox
        {
            Content = "Enable word wrap",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = TryFindResource("ThemeTextPrimary") as System.Windows.Media.Brush,
            IsChecked = true
        };
        stack.Children.Add(wrapCheck);

        ContentArea.Content = new ScrollViewer
        {
            Content = stack,
            Background = TryFindResource("ThemeBackground") as System.Windows.Media.Brush
        };
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = TryFindResourceStatic("ThemeTextPrimary")
        };
    }

    private static TextBlock CreateSettingLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 2),
            Foreground = TryFindResourceStatic("ThemeTextSecondary")
        };
    }

    private static StackPanel CreateSettingCombo(string label, string[] items, string defaultItem)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 2),
            Foreground = TryFindResourceStatic("ThemeTextSecondary")
        });
        var combo = new ComboBox
        {
            Width = 100,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = TryFindResourceStatic("ThemeInputBg"),
            Foreground = TryFindResourceStatic("ThemeTextPrimary"),
            BorderBrush = TryFindResourceStatic("ThemeInputBorder")
        };
        foreach (var item in items)
            combo.Items.Add(item);
        combo.Text = defaultItem;
        panel.Children.Add(combo);
        return panel;
    }

    private static System.Windows.Media.Brush? TryFindResourceStatic(string key)
    {
        try
        {
            return Application.Current.TryFindResource(key) as System.Windows.Media.Brush;
        }
        catch
        {
            return null;
        }
    }
}
