using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ClaudeCodeGUI;

/// <summary>项目 — 对应一个工作目录，是会话的容器</summary>
public class ProjectItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "";
    private string _workingDirectory = "";
    private bool _isExpanded = true;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set { _workingDirectory = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SessionItem> Sessions { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>会话记录 — 属于某个项目的历史会话</summary>
public class SessionItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "";
    private string? _claudeSessionId;
    private DateTime _createdAt = DateTime.Now;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>claudecode 会话 UUID，用于 --session-id / --resume</summary>
    public string? ClaudeSessionId
    {
        get => _claudeSessionId;
        set { _claudeSessionId = value; OnPropertyChanged(); }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    /// <summary>关联的标签页 ID（非持久化，运行时使用）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? TabId { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
