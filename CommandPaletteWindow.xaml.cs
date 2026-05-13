using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ClaudeCodeGUI;

public partial class CommandPaletteWindow : Window
{
    private readonly List<CommandItem> _allCommands = new();

    public CommandPaletteWindow()
    {
        InitializeComponent();
    }

    public void SetCommands(List<CommandItem> commands)
    {
        _allCommands.Clear();
        _allCommands.AddRange(commands);
        FilterCommands("");
    }

    private void FilterCommands(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            CommandList.ItemsSource = _allCommands;
            return;
        }

        var lowerFilter = filter.ToLowerInvariant();
        var filtered = _allCommands
            .Where(c => c.Name.Contains(lowerFilter, StringComparison.OrdinalIgnoreCase) ||
                        c.Description.Contains(lowerFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c =>
            {
                // Prioritize name matches over description matches
                if (c.Name.StartsWith(lowerFilter, StringComparison.OrdinalIgnoreCase)) return 0;
                if (c.Name.Contains(lowerFilter, StringComparison.OrdinalIgnoreCase)) return 1;
                return 2;
            })
            .ToList();

        CommandList.ItemsSource = filtered;
    }

    private void ExecuteSelectedCommand()
    {
        if (CommandList.SelectedItem is CommandItem command && command.Execute != null)
        {
            Close();
            command.Execute();
        }
    }

    private void CommandPaletteWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        CommandList.SelectedIndex = _allCommands.Count > 0 ? 0 : -1;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        FilterCommands(SearchBox.Text);
        CommandList.SelectedIndex = (CommandList.ItemsSource as System.Collections.IList)?.Count > 0 ? 0 : -1;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteSelectedCommand();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void CommandList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelectedCommand();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelectedCommand();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void MoveSelection(int direction)
    {
        var items = CommandList.ItemsSource as System.Collections.IList;
        if (items == null || items.Count == 0) return;

        var currentIndex = CommandList.SelectedIndex;
        var newIndex = Math.Clamp(currentIndex + direction, 0, items.Count - 1);
        CommandList.SelectedIndex = newIndex;
        CommandList.ScrollIntoView(CommandList.SelectedItem);
    }
}
