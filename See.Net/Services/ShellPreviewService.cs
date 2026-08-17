using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using See.Net.Core;
using See.ViewModels;
using See.Views;

namespace See.Services;

/// <summary>
/// 资源管理器空格预览：全局键盘钩子捕获空格，
/// 判断前台为 Explorer 且焦点不在输入控件后，读取选中文件并弹出 Quick Look 式浮窗。
/// </summary>
public sealed class ShellPreviewService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly BackupService _backup;
    private readonly Dispatcher _dispatcher;
    private readonly KeyboardHook _hook = new();
    private readonly ExplorerSelectionService _selection = new();
    private ShellPreviewViewModel? _viewModel;
    private ShellPreviewWindow? _window;
    private bool _disposed;

    public ShellPreviewService(SettingsService settings, BackupService backup, Dispatcher dispatcher)
    {
        _settings = settings;
        _backup = backup;
        _dispatcher = dispatcher;
        _hook.KeyPressed = OnKeyPressed;
    }

    public void Start() => _hook.Install();

    public bool IsPreviewVisible => _window is { IsVisible: true };

    private bool OnKeyPressed(int vkCode)
    {
        if (vkCode == KeyboardHook.VK_ESCAPE)
        {
            if (IsPreviewVisible)
            {
                _dispatcher.BeginInvoke(ClosePreview);
                return true;
            }
            return false;
        }

        if (vkCode != KeyboardHook.VK_SPACE) return false;

        // 浮窗已打开：再次按空格关闭（吞掉按键，避免传给 Explorer）
        if (IsPreviewVisible)
        {
            _dispatcher.BeginInvoke(ClosePreview);
            return true;
        }

        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        string? className = GetWindowClassName(foreground);
        if (!InputFocusClassifier.IsExplorerClass(className)) return false;
        if (IsInputFocusOnTextBox(foreground)) return false;

        // 消费按键，在 UI 线程执行 Shell COM 枚举与弹窗
        IntPtr captured = foreground;
        _dispatcher.BeginInvoke(() => ShowPreview(captured));
        return true;
    }

    private void ShowPreview(IntPtr foreground)
    {
        if (_disposed) return;

        var paths = _selection.GetSelectedFiles(foreground);
        var files = new List<FileEntry>();
        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;
            var fi = new FileInfo(path);
            files.Add(new FileEntry
            {
                Name = fi.Name,
                FullPath = fi.FullName,
                Length = fi.Length,
                LastWriteTime = fi.LastWriteTime,
                Kind = FileTypeDetector.Detect(fi.FullName),
            });
        }
        if (files.Count == 0) return;

        _viewModel ??= new ShellPreviewViewModel(_settings, _backup);
        _window ??= new ShellPreviewWindow { DataContext = _viewModel };
        if (_window.IsVisible) return;

        _viewModel.LoadFiles(files);
        _window.Show();
        _window.Activate();
    }

    public void ClosePreview()
    {
        if (_window is null) return;
        _window.Hide();
        _viewModel?.DisposeContent();
    }

    private static bool IsInputFocusOnTextBox(IntPtr window)
    {
        try
        {
            _ = GetWindowThreadProcessId(window, out uint processId);
            _ = processId;
            uint threadId = GetWindowThreadProcessId(window, out _);
            if (threadId == 0) return false;

            var info = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(threadId, ref info)) return false;
            if (info.hwndFocus == IntPtr.Zero) return false;

            string? focusClass = GetWindowClassName(info.hwndFocus);
            return InputFocusClassifier.IsLikelyInputControl(focusClass);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetWindowClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        int len = GetClassName(hwnd, sb, sb.Capacity);
        return len == 0 ? null : sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hook.Dispose();
        _viewModel?.DisposeContent();
        _window?.Close();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
