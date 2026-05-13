using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClaudeCodeGUI;

public partial class SearchPanel : UserControl
{
    private string _currentDirectory = "";
    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _debounceTimer;

    public event EventHandler<(string filePath, int lineNumber)>? OpenFileRequested;

    public SearchPanel()
    {
        InitializeComponent();
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;
    }

    public void SetSearchDirectory(string directory)
    {
        _currentDirectory = directory;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _debounceTimer.Stop();
            _ = PerformSearchAsync();
        }
    }

    private void SearchOptions_Changed(object sender, RoutedEventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _ = PerformSearchAsync();
    }

    private async System.Threading.Tasks.Task PerformSearchAsync()
    {
        var keyword = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrEmpty(_currentDirectory))
        {
            ResultsList.ItemsSource = null;
            StatusText.Text = "";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        StatusText.Text = "Searching...";

        try
        {
            var results = await SearchService.SearchInDirectory(
                _currentDirectory,
                keyword,
                MatchCaseCheckBox.IsChecked ?? false,
                WholeWordCheckBox.IsChecked ?? false);

            if (token.IsCancellationRequested) return;

            ResultsList.ItemsSource = results;
            var totalMatches = results.Sum(r => r.Matches.Count);
            StatusText.Text = $"{results.Count} files, {totalMatches} matches";
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as FrameworkElement;
        if (element?.DataContext is MatchLine matchLine)
        {
            // Find parent SearchResultItem
            var parent = FindParent<SearchResultItem>(element);
            if (parent != null)
            {
                OpenFileRequested?.Invoke(this, (parent.FilePath, matchLine.LineNumber));
            }
        }
        else if (element?.DataContext is SearchResultItem resultItem)
        {
            OpenFileRequested?.Invoke(this, (resultItem.FilePath, resultItem.Matches.FirstOrDefault()?.LineNumber ?? 1));
        }
    }

    private static T? FindParent<T>(DependencyObject element) where T : class
    {
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is T parent)
                return parent;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
