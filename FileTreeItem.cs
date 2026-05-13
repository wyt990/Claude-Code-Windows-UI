using System.Collections.ObjectModel;

namespace ClaudeCodeGUI;

public class FileTreeItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public ObservableCollection<FileTreeItem> Children { get; set; } = new();

    public string Icon => IsDirectory ? "\U0001f4c1" : "\U0001f4c4";
}
