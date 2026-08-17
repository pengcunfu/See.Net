using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace See.Net.Services;

/// <summary>系统托盘图标：重新打开主窗口、退出应用。</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action _showMain;
    private readonly Action _exit;

    public TrayIconService(Action showMain, Action exit)
    {
        _showMain = showMain;
        _exit = exit;
        _icon = new NotifyIcon
        {
            Text = "See.Net 空格预览",
            Visible = true,
        };

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
        menu.Items.Add("打开 See.Net", null, (_, _) => _showMain());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _exit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => _showMain();
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
