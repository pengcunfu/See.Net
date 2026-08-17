using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace See.Services;

/// <summary>系统托盘图标：打开文件预览、设置、退出应用。</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action _openFile;
    private readonly Action _showSettings;
    private readonly Action _exit;

    public TrayIconService(Action openFile, Action showSettings, Action exit)
    {
        _openFile = openFile;
        _showSettings = showSettings;
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
        menu.Items.Add("打开文件预览…", null, (_, _) => _openFile());
        menu.Items.Add("设置", null, (_, _) => _showSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _exit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => _openFile();
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
