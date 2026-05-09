using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClaudeCodeGUI;

/// <summary>任务项</summary>
public class TodoItem : INotifyPropertyChanged
{
    private string _content = "";
    private TodoStatus _status = TodoStatus.Pending;
    private string? _activeForm;

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    public TodoStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string? ActiveForm
    {
        get => _activeForm;
        set { _activeForm = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
