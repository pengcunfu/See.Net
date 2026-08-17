using System.IO;
using Microsoft.Web.WebView2.Core;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// Office 网页渲染宿主：WebView2 虚拟主机映射 webassets 目录，
/// 文件字节经 WebResourceRequested 拦截以流式回吐（大文件不经 base64 消息）。
/// 环境创建与生命周期由 WebViewHostBase 承担。
/// </summary>
public partial class OfficeWebHost : WebViewHostBase
{
    /// <summary>虚拟主机域名（映射到打包内 webassets 目录，等价于基类 AssetsHost）。</summary>
    public const string VirtualHost = AssetsHost;

    private string? _dataPath;

    public OfficeWebHost()
    {
        InitializeComponent();
    }

    /// <summary>加载文档：kind 用于选择渲染库，path 是原始文件路径。</summary>
    public Task LoadAsync(string kind, string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("文件路径不能为空", nameof(path));
            }
            
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"文件不存在: {path}", path);
            }

            _dataPath = path;
            NavigateOrPending($"https://{VirtualHost}/office-preview.html?kind={kind}");
            return Task.CompletedTask; // WebView 初始化完成后由基类流程导航
        }
        catch (Exception ex)
        {
            // 记录加载错误
            System.Diagnostics.Debug.WriteLine($"Failed to start WebView loading: {ex.Message}");
            
            // 尝试报告错误到VM
            ReportErrorToVM($"加载失败: {ex.Message}");
            
            return Task.FromException(ex);
        }
    }

    protected override void Configure(CoreWebView2 core)
    {
        MapAssets(core);

        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{VirtualHost}/data", CoreWebView2WebResourceContext.Other);
        core.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>拦截 data 请求，以 FileStream 流式回吐文件字节。</summary>
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_dataPath is null || !e.Request.Uri.EndsWith("/data", StringComparison.Ordinal))
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
            return;
        }

        try
        {
            var stream = new FileStream(_dataPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                stream, 200, "OK", "Content-Type: application/octet-stream");
        }
        catch (FileNotFoundException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "File Not Found", "");
            ReportErrorToVM($"文件不存在: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Directory Not Found", "");
            ReportErrorToVM($"目录不存在: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 403, "Forbidden", "");
            ReportErrorToVM($"访问权限不足: {ex.Message}");
        }
        catch (IOException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 500, "IO Error", "");
            ReportErrorToVM($"文件读取错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 500, "Internal Server Error", "");
            ReportErrorToVM($"服务器错误: {ex.Message}");
        }
    }

    /// <summary>接收渲染页 postMessage 的错误上报。</summary>
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var vm = DataContext as OfficeContentViewModel;
        if (vm is null) return;
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (json?.Contains("\"type\":\"error\"") == true)
                Dispatcher.Invoke(() => vm.WebError = ExtractMessage(json));
        }
        catch
        {
            // 非字符串消息忽略
        }
    }

    /// <summary>报告错误到 ViewModel。</summary>
    private void ReportErrorToVM(string errorMessage)
    {
        var vm = DataContext as OfficeContentViewModel;
        if (vm is not null)
        {
            Dispatcher.Invoke(() => vm.WebError = errorMessage);
        }
    }

    private static string ExtractMessage(string json)
    {
        // 简易提取 "message":"..." 字段，避免引入 JSON 依赖
        int idx = json.IndexOf("\"message\":\"", StringComparison.Ordinal);
        if (idx < 0) return json;
        int start = idx + 11;
        int end = json.IndexOf('"', start);
        return end > start ? json[start..end] : json;
    }

    protected override void OnDetach(CoreWebView2 core)
    {
        core.WebResourceRequested -= OnWebResourceRequested;
        core.WebMessageReceived -= OnWebMessageReceived;
    }
}
