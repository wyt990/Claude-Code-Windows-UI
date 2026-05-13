using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeCodeGUI;

public partial class FilesSidebar : UserControl
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".vs", ".svn",
        ".idea", "packages", ".cache", "Debug", "Release", "x64", "x86"
    };

    public event EventHandler<string>? FileActivated;

    public FilesSidebar()
    {
        InitializeComponent();
    }

    public void LoadDirectory(string path)
    {
        FileTree.Items.Clear();

        if (!Directory.Exists(path))
            return;

        var root = new TreeViewItem
        {
            Header = new FileTreeItem
            {
                Name = Path.GetFileName(path) + " (root)",
                FullPath = path,
                IsDirectory = true
            },
            Tag = path,
            IsExpanded = true
        };

        // Add dummy item for lazy loading
        root.Items.Add(null);
        root.Expanded += Root_Expanded;

        FileTree.Items.Add(root);

        // Load immediate children for the root
        LoadChildren(root, path);
    }

    private void Root_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Tag is string path)
        {
            LoadChildren(item, path);
        }
    }

    private void LoadChildren(TreeViewItem parentItem, string directoryPath)
    {
        parentItem.Items.Clear();

        try
        {
            // Load subdirectories
            foreach (var dir in Directory.EnumerateDirectories(directoryPath)
                         .Where(d => !ExcludedDirectories.Contains(Path.GetFileName(d)))
                         .OrderBy(d => Path.GetFileName(d)))
            {
                var childItem = new TreeViewItem
                {
                    Header = new FileTreeItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true
                    },
                    Tag = dir
                };

                // Add dummy for lazy loading if directory has sub-items
                if (Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    childItem.Items.Add(null);
                }

                childItem.Expanded += ChildItem_Expanded;
                parentItem.Items.Add(childItem);
            }

            // Load files
            foreach (var file in Directory.EnumerateFiles(directoryPath)
                         .OrderBy(f => Path.GetFileName(f)))
            {
                parentItem.Items.Add(new TreeViewItem
                {
                    Header = new FileTreeItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsDirectory = false
                    },
                    Tag = file
                });
            }
        }
        catch
        {
            // Skip inaccessible directories
        }
    }

    private void ChildItem_Expanded(object? sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Tag is string path)
        {
            LoadChildren(item, path);
        }
    }

    private void FileTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var selectedItem = FileTree.SelectedItem as TreeViewItem;
        var fileItem = selectedItem?.Header as FileTreeItem;
        if (fileItem != null && !fileItem.IsDirectory)
        {
            FileActivated?.Invoke(this, fileItem.FullPath);
        }
    }
}
