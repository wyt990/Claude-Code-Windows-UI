using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ClaudeCodeGUI;

/// <summary>项目管理器 — 项目/会话树的 JSON 持久化</summary>
public class ProjectManager
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeCodeGUI");

    private static readonly string FilePath = Path.Combine(DataDir, "projects.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>项目列表</summary>
    public ObservableCollection<ProjectItem> Projects { get; } = new();

    /// <summary>从 JSON 文件加载项目列表</summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;

            string json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<ObservableCollection<ProjectItem>>(json);
            if (list == null) return;

            Projects.Clear();
            foreach (var p in list)
                Projects.Add(p);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载项目列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>持久化项目列表到 JSON 文件</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);

            string json = JsonSerializer.Serialize(Projects, JsonOptions);

            // 原子写入：临时文件 + 替换
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存项目列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── CRUD ───────────────────────────────────────────

    /// <summary>添加新项目</summary>
    public ProjectItem AddProject(string name, string workDir)
    {
        var project = new ProjectItem
        {
            Name = name,
            WorkingDirectory = workDir,
        };
        Projects.Add(project);
        Save();
        return project;
    }

    /// <summary>移除项目（仅从列表移除，不删磁盘文件）</summary>
    public void RemoveProject(ProjectItem project)
    {
        Projects.Remove(project);
        Save();
    }

    /// <summary>在项目中添加会话记录</summary>
    public SessionItem AddSession(ProjectItem project, string name, string? claudeSessionId = null)
    {
        var session = new SessionItem
        {
            Name = name,
            ClaudeSessionId = claudeSessionId,
        };
        project.Sessions.Add(session);
        Save();
        return session;
    }

    /// <summary>从项目中移除会话记录</summary>
    public void RemoveSession(ProjectItem project, SessionItem session)
    {
        project.Sessions.Remove(session);
        Save();
    }

    /// <summary>重命名会话</summary>
    public void RenameSession(ProjectItem project, SessionItem session, string newName)
    {
        session.Name = newName;
        Save();
    }
}
