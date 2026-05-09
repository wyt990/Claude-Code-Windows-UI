using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ClaudeCodeGUI;

/// <summary>多会话管理器 — 管理所有标签页的生命周期</summary>
public class SessionManager : INotifyPropertyChanged
{
    /// <summary>所有打开的标签页</summary>
    public ObservableCollection<SessionTabItem> Tabs { get; } = new();

    private SessionTabItem? _activeTab;

    /// <summary>当前激活的标签页</summary>
    public SessionTabItem? ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (_activeTab != null)
                _activeTab.IsActive = false;

            _activeTab = value;

            if (_activeTab != null)
                _activeTab.IsActive = true;

            OnPropertyChanged();
        }
    }

    // ── 事件 ───────────────────────────────────────────

    /// <summary>标签页被激活时触发</summary>
    public event EventHandler<SessionTabItem>? TabActivated;

    /// <summary>新标签页被创建时触发</summary>
    public event EventHandler<SessionTabItem>? TabAdded;

    /// <summary>标签页被关闭时触发</summary>
    public event EventHandler<SessionTabItem>? TabClosed;

    // ── 方法 ───────────────────────────────────────────

    /// <summary>创建并添加新标签页</summary>
    public SessionTabItem CreateTab(string name, SessionState? state = null, string? tabId = null)
    {
        var tab = new SessionTabItem(name, state);

        Tabs.Add(tab);
        TabAdded?.Invoke(this, tab);
        return tab;
    }

    /// <summary>激活指定标签页</summary>
    public void ActivateTab(SessionTabItem tab)
    {
        if (tab == _activeTab) return;

        ActiveTab = tab;
        TabActivated?.Invoke(this, tab);
    }

    /// <summary>按索引激活标签</summary>
    public void ActivateTabAt(int index)
    {
        if (index < 0 || index >= Tabs.Count) return;
        ActivateTab(Tabs[index]);
    }

    /// <summary>关闭指定标签页，返回下一个应激活的标签（或 null）</summary>
    public SessionTabItem? CloseTab(SessionTabItem tab)
    {
        int index = Tabs.IndexOf(tab);
        if (index < 0) return null;

        // 清理进程
        tab.CTS?.Cancel();
        try { tab.Job?.Kill(entireProcessTree: true); } catch { }
        tab.Job?.Dispose();

        Tabs.Remove(tab);
        TabClosed?.Invoke(this, tab);

        // 自动激活相邻标签
        SessionTabItem? next = null;
        if (Tabs.Count > 0)
        {
            int nextIndex = Math.Min(index, Tabs.Count - 1);
            next = Tabs[nextIndex];
            ActivateTab(next);
        }
        else
        {
            ActiveTab = null;
        }

        return next;
    }

    /// <summary>获取标签总数</summary>
    public int Count => Tabs.Count;

    /// <summary>是否有激活的标签</summary>
    public bool HasActiveTab => _activeTab != null;

    // ── INotifyPropertyChanged ─────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
