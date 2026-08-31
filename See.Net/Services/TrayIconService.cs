using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace See.Services;

/// <summary>系统托盘图标：打开文件预览、设置、关于、退出应用。</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action _openFile;
    private readonly Action _showLauncher;
    private readonly Action _showSettings;
    private readonly Action _checkUpdates;
    private readonly Action _showAbout;
    private readonly Action _exit;

    /// <summary>托盘气泡被点击（如"有可用更新"提示），由外部订阅打开更新窗口。</summary>
    public event Action? BalloonTipClicked;

    public TrayIconService(Action openFile, Action showLauncher, Action showSettings, Action checkUpdates, Action showAbout, Action exit)
    {
        _openFile = openFile;
        _showLauncher = showLauncher;
        _showSettings = showSettings;
        _checkUpdates = checkUpdates;
        _showAbout = showAbout;
        _exit = exit;
        _icon = new NotifyIcon
        {
            Text = "See.Net 空格预览",
            Visible = true,
        };
        _icon.BalloonTipClicked += (_, _) => BalloonTipClicked?.Invoke();

        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null && File.Exists(exe))
            {
                using var extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted is not null) _icon.Icon = (Icon)extracted.Clone();
            }
        }
        catch { /* 使用默认图标 */ }
        _icon.Icon ??= SystemIcons.Application;

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开文件预览…", null, (_, _) => _openFile());
        menu.Items.Add("设置", null, (_, _) => _showSettings());
        menu.Items.Add("检查更新", null, (_, _) => _checkUpdates());
        menu.Items.Add("关于", null, (_, _) => _showAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _exit());
        _icon.ContextMenuStrip = menu;
        // 左键单击打开启动器（搜索框）
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _showLauncher();
        };
    }

    public void ShowBalloon(string title, string text)
    {
        try { _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info); } catch { }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
