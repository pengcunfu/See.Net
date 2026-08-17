using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Core;
using See.Net.Core.Office;

namespace See.Net.ViewModels;

/// <summary>
/// Office 文档内容视图模型：双引擎状态机。
/// 结构化引擎在后台线程解析（Core 产出 Word/Sheet/Slides 模型），
/// 网页引擎由视图侧 WebView2 承载，二者经 UseWeb 一键切换。
/// </summary>
public sealed partial class OfficeContentViewModel : ObservableObject, IDisposable
{
    /// <summary>网页引擎可渲染的扩展名（WebView2 视图按此选择 JS 渲染库）。</summary>
    public static readonly HashSet<string> WebRenderableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".docm", ".xlsx", ".xlsm", ".xls", ".pptx", ".pptm",
    };

    private readonly string _path;
    private readonly string _extension;

    public OfficeContentViewModel(string path)
    {
        _path = path;
        _extension = Path.GetExtension(path).ToLowerInvariant();
        IsWebOnly = _extension is ".xls"; // 旧版 Excel 仅 SheetJS 可读
        UseWeb = IsWebOnly;
        CanUseWeb = WebRenderableExtensions.Contains(_extension) && IsWebViewRuntimeAvailable();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    /// <summary>本机是否安装 WebView2 运行时（放 VM 层，避免视图层反向依赖）。</summary>
    private static bool IsWebViewRuntimeAvailable()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch (Exception ex)
        {
            // WebView2 运行时不可用或初始化失败
            System.Diagnostics.Debug.WriteLine($"WebView2 runtime check failed: {ex.Message}");
            return false;
        }
    }

    public string FilePath => _path;

    /// <summary>传给网页渲染器的 kind 参数（office-preview.html?kind=…）。</summary>
    public string WebKind => _extension switch
    {
        ".docx" or ".docm" => "docx",
        ".xlsx" or ".xlsm" or ".xls" => "xlsx",
        ".pptx" or ".pptm" => "pptx",
        _ => "",
    };

    /// <summary>旧版二进制格式（.xls）没有结构化引擎，网页是唯一视图。</summary>
    public bool IsWebOnly { get; }

    /// <summary>本机是否存在 WebView2 运行时（缺失时网页模式整体降级）。</summary>
    public bool CanUseWeb { get; }

    public IAsyncRelayCommand LoadCommand { get; }

    /// <summary>结构化解析状态：null=加载中；异常时为 Error。</summary>
    public enum LoadState { Loading, Loaded, Error, Unsupported }

    [ObservableProperty]
    private LoadState _state = LoadState.Loading;

    /// <summary>结构化模型：WordBlocksModel / SheetSetModel / SlidesModel 之一。</summary>
    [ObservableProperty]
    private object? _structured;

    [ObservableProperty]
    private string? _error;

    /// <summary>结构化视图不可用时给出的提示标题（如“旧版格式仅网页预览支持”）。</summary>
    [ObservableProperty]
    private string? _structuredNotice;

    /// <summary>当前是否处于网页渲染视图。</summary>
    [ObservableProperty]
    private bool _useWeb;

    /// <summary>网页渲染失败信息（由 WebView postMessage 回传），非空时提示并可切回。</summary>
    [ObservableProperty]
    private string? _webError;

    partial void OnUseWebChanged(bool value)
    {
        if (value) WebError = null;
        RaiseEngineChanged();
    }

    partial void OnStructuredChanged(object? value) => RaiseEngineChanged();

    partial void OnStateChanged(LoadState value) => RaiseEngineChanged();

    private void RaiseEngineChanged()
    {
        OnPropertyChanged(nameof(ShowStructured));
        OnPropertyChanged(nameof(ShowWeb));
        OnPropertyChanged(nameof(ShowWord));
        OnPropertyChanged(nameof(ShowSheet));
        OnPropertyChanged(nameof(ShowSlides));
    }

    public bool ShowStructured => !UseWeb && State is LoadState.Loaded;
    public bool ShowWeb => UseWeb && CanUseWeb;
    public bool ShowWord => ShowStructured && Structured is WordBlocksModel;
    public bool ShowSheet => ShowStructured && Structured is SheetSetModel;
    public bool ShowSlides => ShowStructured && Structured is SlidesModel;

    [RelayCommand]
    private void UseStructuredView() => UseWeb = false;

    [RelayCommand]
    private void UseWebView() => UseWeb = true;

    private async Task LoadAsync()
    {
        if (!OfficeDocumentReader.CanReadStructured(_extension))
        {
            State = LoadState.Unsupported;
            StructuredNotice = _extension is ".xls"
                ? "旧版 .xls 二进制格式由网页引擎（SheetJS）读取。"
                : $"旧版 {_extension} 二进制格式暂不支持文本提取，可用网页预览或十六进制查看。";
            return;
        }

        try
        {
            var model = await Task.Run(() => OfficeDocumentReader.Read(_path));
            Structured = model;
            State = LoadState.Loaded;
        }
        catch (NotSupportedException)
        {
            State = LoadState.Unsupported;
            StructuredNotice = $"此格式暂不支持结构化解析（{_extension}）。";
        }
        catch (IOException ex)
        {
            // 文件读取错误（文件被占用、权限问题等）
            State = LoadState.Error;
            Error = $"文件读取失败: {ex.Message}";
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"文件读取错误（{ex.Message}），已切换到网页预览。";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            // 权限不足
            State = LoadState.Error;
            Error = $"访问权限不足: {ex.Message}";
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"访问权限不足（{ex.Message}），已切换到网页预览。";
            }
        }
        catch (InvalidDataException ex)
        {
            // 文档格式错误或损坏
            State = LoadState.Error;
            Error = ex.Message;
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"文档格式问题（{ex.Message}），已切换到网页预览。";
            }
        }
        catch (OutOfMemoryException)
        {
            // 内存不足
            State = LoadState.Error;
            Error = "文档过大，内存不足";
            StructuredNotice = "文档过大，结构化解析失败。建议使用网页预览或十六进制查看。";
        }
        catch (Exception ex)
        {
            // 其他未知异常
            State = LoadState.Error;
            Error = $"解析失败: {ex.Message}";
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"结构化解析失败（{ex.Message}），已切换到网页预览。";
            }
        }
    }

    public void Dispose()
    {
        Structured = null;
    }
}

