using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using See.Net.Core;
using See.Net.Services;

namespace See.Net.ViewModels;

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
        Content = null;
    }

    private async Task LoadTextAsync(FileEntry file)
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
                return;
            }

            var bytes = await Task.Run(() => File.ReadAllBytes(file.FullPath));
            var encoding = EncodingService.Detect(bytes);
            string text = encoding.GetString(bytes);
            Content = new TextContentViewModel(file.FullPath, text, encoding, _backup);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("文本加载失败", ex.Message);
        }
    }

    private void LoadImage(FileEntry file)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 2048;
            image.UriSource = new Uri(file.FullPath);
            image.EndInit();
            image.Freeze();
            Content = new ImageContentViewModel(image, file.FullPath, file.Length);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("图片加载失败", ex.Message);
        }
    }
}
