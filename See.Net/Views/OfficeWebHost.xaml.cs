using System.IO;
using Microsoft.Web.WebView2.Core;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// Office 网页渲染宿主：
/// - AssetsHost（映射 webassets）：office-preview 页与 mammoth / SheetJS / PPTXjs 等离线库
/// - see-office-data.local（未映射）：文档字节经 WebResourceRequested 回吐
/// 映射域上不触发 WebResourceRequested，故 /data 绝不能挂在 AssetsHost。
/// </summary>
public partial class OfficeWebHost : WebViewHostBase
{
    /// <summary>静态资源虚拟主机（映射 webassets）。</summary>
    public const string VirtualHost = AssetsHost;

    /// <summary>文档字节专用域（不映射，保证可拦截）。</summary>
    public const string DataHost = "see-office-data.local";

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
                throw new ArgumentException("文件路径不能为空", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException($"文件不存在: {path}", path);

            _dataPath = path;
            NavigateOrPending($"https://{VirtualHost}/office-preview.html?kind={Uri.EscapeDataString(kind)}");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start WebView loading: {ex.Message}");
            ReportErrorToVM($"加载失败: {ex.Message}");
            return Task.FromException(ex);
        }
    }

    protected override void Configure(CoreWebView2 core)
    {
        MapAssets(core);

        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{DataHost}/*", CoreWebView2WebResourceContext.All);
        core.WebMessageReceived += OnWebMessageReceived;
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(DataHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 跨域 fetch / XHR 预检
        if (string.Equals(e.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                null, 204, "No Content", CorsHeaders());
            return;
        }

        if (!uri.AbsolutePath.Equals("/data", StringComparison.OrdinalIgnoreCase))
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", CorsHeaders());
            return;
        }

        if (_dataPath is null)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", CorsHeaders());
            return;
        }

        try
        {
            var stream = new FileStream(_dataPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                stream, 200, "OK",
                CorsHeaders() +
                $"\nContent-Type: application/octet-stream\nContent-Length: {length}\nAccept-Ranges: bytes");
        }
        catch (FileNotFoundException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "File Not Found", CorsHeaders());
            ReportErrorToVM($"文件不存在: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Directory Not Found", CorsHeaders());
            ReportErrorToVM($"目录不存在: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 403, "Forbidden", CorsHeaders());
            ReportErrorToVM($"访问权限不足: {ex.Message}");
        }
        catch (IOException ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 500, "IO Error", CorsHeaders());
            ReportErrorToVM($"文件读取错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 500, "Internal Server Error", CorsHeaders());
            ReportErrorToVM($"服务器错误: {ex.Message}");
        }
    }

    /// <summary>页面在 AssetsHost，数据在 DataHost，需显式放开 CORS。</summary>
    private static string CorsHeaders() =>
        $"Access-Control-Allow-Origin: https://{VirtualHost}\n" +
        "Access-Control-Allow-Methods: GET, HEAD, OPTIONS\n" +
        "Access-Control-Allow-Headers: *";

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

    private void ReportErrorToVM(string errorMessage)
    {
        var vm = DataContext as OfficeContentViewModel;
        if (vm is not null)
            Dispatcher.Invoke(() => vm.WebError = errorMessage);
    }

    private static string ExtractMessage(string json)
    {
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
