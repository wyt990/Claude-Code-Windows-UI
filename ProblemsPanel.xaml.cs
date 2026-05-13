using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeCodeGUI;

public partial class ProblemsPanel : UserControl
{
    private ProblemSeverity _currentFilter = ProblemSeverity.Error;

    public event EventHandler<(string filePath, int lineNumber)>? ProblemDoubleClicked;

    public ProblemsPanel()
    {
        InitializeComponent();
        ProblemService.ProblemAdded += OnProblemAdded;
        ProblemService.ProblemsCleared += OnProblemsCleared;
        RefreshList();
    }

    private void OnProblemAdded(object? sender, ProblemEventArgs e)
    {
        Dispatcher.Invoke(RefreshList);
    }

    private void OnProblemsCleared(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshList);
    }

    private void RefreshList()
    {
        // Guard: XAML may not be fully initialized when ComboBox SelectionChanged fires
        if (ProblemListView == null || CountText == null) return;

        var allProblems = ProblemService.Problems;

        var filtered = FilterCombo.SelectedIndex switch
        {
            1 => allProblems.Where(p => p.Severity == ProblemSeverity.Error),
            2 => allProblems.Where(p => p.Severity == ProblemSeverity.Warning),
            _ => allProblems
        };

        ProblemListView.ItemsSource = filtered.ToList();
        CountText.Text = $"{ProblemService.Problems.Count} problems";
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshList();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshList();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ProblemService.Clear();
    }

    private void ProblemListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProblemListView.SelectedItem is ProblemItem problem)
        {
            ProblemDoubleClicked?.Invoke(this, (problem.FilePath, problem.LineNumber));
        }
    }
}
