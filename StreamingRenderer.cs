using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ClaudeCodeGUI;

/// <summary>
/// 流式文本渲染器 — 以 60fps 将缓冲文本逐字/逐块渲染到目标 TextBlock。
/// 防止大批量文本一次性刷新导致的 UI 卡顿，提供平滑的逐字出现效果。
/// </summary>
public class StreamingRenderer : IDisposable
{
    private readonly StringBuilder _buffer = new();
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _target;
    private readonly ScrollViewer _scroller;
    private readonly Action<string>? _fullTextCallback;

    /// <summary>完整文本（含已渲染 + 缓冲中），用于复制等操作</summary>
    private readonly StringBuilder _fullText = new();

    private bool _isDirty;
    private int _charsPerFrame = 40;
    private bool _paused;

    /// <summary>当前缓冲中的字符数</summary>
    public int BufferedLength => _buffer.Length;

    /// <summary>完整文本内容</summary>
    public string FullText => _fullText.ToString();

    /// <summary>每帧渲染字符数（可动态调整，默认 40）</summary>
    public int CharsPerFrame
    {
        get => _charsPerFrame;
        set => _charsPerFrame = Math.Clamp(value, 5, 200);
    }

    /// <summary>是否正在流式渲染中</summary>
    public bool IsActive => _timer.IsEnabled && !_paused;

    /// <summary>
    /// 创建流式渲染器
    /// </summary>
    /// <param name="target">目标 TextBlock</param>
    /// <param name="scroller">滚动容器（渲染后自动滚动到底部）</param>
    /// <param name="fullTextCallback">完整文本回调（每次追加时触发，用于外部持久化）</param>
    public StreamingRenderer(TextBlock target, ScrollViewer? scroller = null, Action<string>? fullTextCallback = null)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _scroller = scroller!;
        _fullTextCallback = fullTextCallback;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>追加文本到渲染缓冲区</summary>
    public void AppendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        _buffer.Append(text);
        _fullText.Append(text);
        _isDirty = true;

        if (!_timer.IsEnabled && !_paused)
            _timer.Start();
    }

    /// <summary>暂停渲染（保留缓冲区）</summary>
    public void Pause()
    {
        _paused = true;
    }

    /// <summary>恢复渲染</summary>
    public void Resume()
    {
        _paused = false;
        if (_isDirty && !_timer.IsEnabled)
            _timer.Start();
    }

    /// <summary>立即刷新所有缓冲内容到 UI</summary>
    public void Flush()
    {
        if (_buffer.Length == 0) return;

        string remaining = _buffer.ToString();
        _buffer.Clear();
        _isDirty = false;

        _target.Text += remaining;

        if (_scroller != null)
        {
            _scroller.ScrollToBottom();
        }

        _fullTextCallback?.Invoke(_fullText.ToString());

        if (_timer.IsEnabled)
            _timer.Stop();
    }

    /// <summary>重置渲染器（清除缓存和计时器）</summary>
    public void Reset()
    {
        _timer.Stop();
        _buffer.Clear();
        _fullText.Clear();
        _isDirty = false;
        _paused = false;
    }

    /// <summary>以纯文本方式直接设置完整内容（用于恢复历史消息，无动画）</summary>
    public void SetTextDirect(string text)
    {
        Reset();
        _fullText.Append(text);
        _target.Text = text;
    }

    // ── 计时器回调 ─────────────────────────────────────

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_paused || !_isDirty || _buffer.Length == 0)
        {
            if (_buffer.Length == 0)
            {
                _isDirty = false;
                _timer.Stop();
            }
            return;
        }

        int charsToRender = Math.Min(_charsPerFrame, _buffer.Length);
        var chunk = _buffer.ToString(0, charsToRender);
        _buffer.Remove(0, charsToRender);

        _target.Text += chunk;
        _isDirty = _buffer.Length > 0;

        // 每 N 帧滚动一次（避免频繁滚动导致性能问题）
        if (_scroller != null && _fi++ % 4 == 0)
        {
            _scroller.ScrollToBottom();
        }

        _fullTextCallback?.Invoke(_fullText.ToString());
    }

    private int _fi;

    // ── IDisposable ─────────────────────────────────────

    public void Dispose()
    {
        Flush();
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
