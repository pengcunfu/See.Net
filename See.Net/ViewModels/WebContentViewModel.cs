using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using See.Net.Core;
using See.Services;

namespace See.ViewModels;

/// <summary>
/// 本地网页双视图状态机：默认 WebView2 渲染（脚本启用，目录映射解析相对引用），
/// 可切换只读源码（懒加载，首次切换时读取文件）。
/// </summary>
public sealed partial class WebContentViewModel : ObservableObject, IDisposable
{
    private readonly BackupService _backup;
    private readonly SettingsService _settings;

    public WebContentViewModel(string path, bool canRender, BackupService backup, SettingsService settings)
    {
        FilePath = path;
        CanRender = canRender;
        _backup = backup;
        _settings = settings;
        if (!canRender) _useRendered = false; // WebView2 运行时缺失：初始即源码模式
    }

    public string FilePath { get; }

    /// <summary>本机是否存在 WebView2 运行时（缺失时渲染视图整体降级）。</summary>
    public bool CanRender { get; }

    /// <summary>只读源码视图模型（懒加载，GetSource 首次调用时创建）。</summary>
    public TextContentViewModel? Source { get; private set; }

    /// <summary>当前是否处于渲染视图。</summary>
    [ObservableProperty]
    private bool _useRendered = true;

    /// <summary>渲染失败信息（文件名含保留字符等场景），非空时提示并可切源码。</summary>
    [ObservableProperty]
    private string? _renderError;

    /// <summary>懒加载源码：首次切换到源码视图时由视图层调用。</summary>
    public TextContentViewModel GetSource()
    {
        if (Source is not null) return Source;

        string text;
        Encoding encoding;
        try
        {
            var bytes = ReadFileShared(FilePath);
            encoding = EncodingService.Detect(bytes);
            text = encoding.GetString(bytes);
        }
        catch (Exception ex)
        {
            RenderError = ex.Message;
            text = "";
            encoding = Encoding.UTF8;
        }
        Source = new TextContentViewModel(FilePath, text, encoding, _backup, allowEdit: false);
        Source.FontFamily = _settings.Current.TextFontFamily;
        Source.FontSize = _settings.Current.TextFontSize;
        return Source;
    }

    /// <summary>以共享只读方式读取文件全部字节，允许被其他进程占用的文件也能打开。</summary>
    private static byte[] ReadFileShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[fs.Length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = fs.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) break;
            offset += read;
        }
        return bytes;
    }

    public void Dispose()
    {
        Source = null;
    }
}
