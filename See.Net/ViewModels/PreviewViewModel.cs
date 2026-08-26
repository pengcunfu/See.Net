using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using See.Net.Core;
using See.Services;

namespace See.ViewModels;

/// <summary>预览层状态：加载并持有当前文件的内容视图模型。</summary>
public partial class PreviewViewModel : ObservableObject
{
    public const long MaxTextPreviewBytes = 100L * 1024 * 1024;

    private readonly BackupService _backup;
    private readonly SettingsService _settings;

    public PreviewViewModel(SettingsService settings, BackupService backup)
    {
        _settings = settings;
        _backup = backup;
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private FileEntry? _currentFile;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _subtitle = "";

    [ObservableProperty]
    private object? _content;

    public bool HasUnsavedChanges => Content switch
    {
        TextContentViewModel t => t.IsDirty,
        HexContentViewModel h => h.IsDirty,
        MarkdownContentViewModel m => m.Source.IsDirty,
        _ => false,
    };

    public async Task LoadAsync(FileEntry file)
    {
        if (CurrentFile?.FullPath == file.FullPath && Content is not null) return;
        CloseDocument();
        CurrentFile = file;
        Title = file.Name;
        Subtitle = $"{file.FullPath} · {file.SizeText} · {file.KindText}";

        if (file.IsDirectory)
        {
            Content = new InfoContentViewModel("文件夹", "空格预览对文件夹显示基本信息，按 Enter 进入该文件夹。");
            return;
        }

        ContentKind kind = FileTypeDetector.Detect(file.FullPath);
        switch (kind)
        {
            case ContentKind.Text:
            case ContentKind.Code:
                await LoadTextAsync(file);
                break;
            case ContentKind.Image:
                LoadImage(file);
                break;
            case ContentKind.Document:
                LoadOffice(file);
                break;
            case ContentKind.Markdown:
                await LoadMarkdownAsync(file);
                break;
            case ContentKind.WebPage:
                await LoadWebAsync(file);
                break;
            case ContentKind.Audio:
                LoadAudio(file);
                break;
            case ContentKind.Pdf:
                LoadPdf(file);
                break;
            case ContentKind.Binary:
                LoadHex(file);
                break;
            default:
                Content = new InfoContentViewModel(
                    "无法识别此文件类型",
                    "没有找到对应的预览方式，可以尝试用十六进制编辑器查看。",
                    "以十六进制打开",
                    () => LoadHex(file));
                break;
        }
    }

    public void LoadHex(FileEntry file)
    {
        try
        {
            CloseDocument();
            var doc = HexDocument.Open(file.FullPath);
            Content = new HexContentViewModel(doc, file.FullPath, _backup,
                _settings.Current.HexFontSize, _settings.Current.BytesPerRow);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("打开失败", ex.Message);
        }
    }

    public async Task<bool> SaveIfDirtyAsync()
    {
        if (!HasUnsavedChanges) return true;
        var result = MessageBox.Show(
            $"“{CurrentFile?.Name}” 有未保存的修改，是否保存？",
            "See.Net",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.No) return true;

        try
        {
            switch (Content)
            {
                case TextContentViewModel t:
                    var save = t.SaveCommand;
                    save.Execute(null);
                    if (save.ExecutionTask is not null) await save.ExecutionTask;
                    break;
                case MarkdownContentViewModel m:
                    var mdSave = m.Source.SaveCommand;
                    mdSave.Execute(null);
                    if (mdSave.ExecutionTask is not null) await mdSave.ExecutionTask;
                    break;
                case HexContentViewModel h:
                    h.SaveCommand.Execute(null);
                    break;
            }
            return !HasUnsavedChanges;
        }
        catch
        {
            return false;
        }
    }

    public void CloseDocument()
    {
        if (Content is HexContentViewModel hex) hex.Dispose();
        if (Content is OfficeContentViewModel office) office.Dispose();
        if (Content is MarkdownContentViewModel markdown) markdown.Dispose();
        if (Content is WebContentViewModel web) web.Dispose();
        Content = null;
    }

    private async Task LoadTextAsync(FileEntry file)
    {
        var loaded = await TryReadTextAsync(file, "文本加载失败");
        if (loaded is not null)
        {
            var vm = new TextContentViewModel(file.FullPath, loaded.Value.text, loaded.Value.encoding, _backup);
            vm.FontFamily = _settings.Current.TextFontFamily;
            vm.FontSize = _settings.Current.TextFontSize;
            Content = vm;
        }
    }

    /// <summary>文本类加载共用：大小守卫 + 字节读取 + 编码检测；失败时置 Info 内容并返回 null。</summary>
    private async Task<(string text, Encoding encoding)?> TryReadTextAsync(FileEntry file, string errorTitle)
    {
        try
        {
            long length = new FileInfo(file.FullPath).Length;
            if (length > MaxTextPreviewBytes)
            {
                Content = new InfoContentViewModel(
                    "文件过大，文本预览已跳过",
                    $"文件大小 {FileEntry.FormatSize(length)}，超过文本预览上限 {FileEntry.FormatSize(MaxTextPreviewBytes)}。请使用十六进制编辑器查看或编辑。",
                    "以十六进制打开",
                    () => LoadHex(file));
                return null;
            }

            var bytes = await Task.Run(() => ReadFileShared(file.FullPath));
            var encoding = EncodingService.Detect(bytes);
            return (encoding.GetString(bytes), encoding);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel(errorTitle, ex.Message);
            return null;
        }
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

    private void LoadImage(FileEntry file)
    {
        try
        {
            var bytes = ReadFileShared(file.FullPath);
            using var ms = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 2048;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            Content = new ImageContentViewModel(image, file.FullPath, file.Length);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("图片加载失败", ex.Message);
        }
    }

    /// <summary>Office 文档：结构化解析由视图模型后台执行，网页引擎按需启动。</summary>
    private void LoadOffice(FileEntry file)
    {
        Content = new OfficeContentViewModel(file.FullPath);
    }

    /// <summary>Markdown：源码复用文本编辑栈，渲染经 Markdig 后由 WebView2 承载。</summary>
    private async Task LoadMarkdownAsync(FileEntry file)
    {
        var loaded = await TryReadTextAsync(file, "Markdown 加载失败");
        if (loaded is null) return;

        var source = new TextContentViewModel(file.FullPath, loaded.Value.text, loaded.Value.encoding, _backup);
        source.FontFamily = _settings.Current.TextFontFamily;
        source.FontSize = _settings.Current.TextFontSize;
        var vm = new MarkdownContentViewModel(file.FullPath, source, Views.WebViewHostBase.IsRuntimeAvailable());
        await vm.RenderAsync(); // 先渲染，视图进入时 Html 已就绪
        Content = vm;
    }

    /// <summary>本地网页：WebView2 目录映射渲染（脚本启用），可切只读源码。</summary>
    private async Task LoadWebAsync(FileEntry file)
    {
        var vm = new WebContentViewModel(file.FullPath, Views.WebViewHostBase.IsRuntimeAvailable(), _backup, _settings);
        Content = vm;
        await Task.CompletedTask;
    }

    /// <summary>音频：WebView2 播放页；运行时缺失降级为提示卡片。</summary>
    private void LoadAudio(FileEntry file)
    {
        if (!Views.WebViewHostBase.IsRuntimeAvailable())
        {
            Content = new InfoContentViewModel(
                "音频预览需要 WebView2 运行时",
                "未检测到 WebView2 运行时，无法播放音频。可安装 Evergreen 运行时后重试，或以十六进制查看文件头。",
                "以十六进制打开",
                () => LoadHex(file));
            return;
        }
        Content = new AudioContentViewModel(file.FullPath, file.Name, file.Length);
    }

    /// <summary>PDF：WebView2 内置 PDF 查看器；运行时缺失降级为提示卡片。</summary>
    private void LoadPdf(FileEntry file)
    {
        if (!Views.WebViewHostBase.IsRuntimeAvailable())
        {
            Content = new InfoContentViewModel(
                "PDF 预览需要 WebView2 运行时",
                "未检测到 WebView2 运行时，无法预览 PDF。可安装 Evergreen 运行时后重试，或以十六进制查看文件头。",
                "以十六进制打开",
                () => LoadHex(file));
            return;
        }
        Content = new PdfContentViewModel(file.FullPath);
    }
}
