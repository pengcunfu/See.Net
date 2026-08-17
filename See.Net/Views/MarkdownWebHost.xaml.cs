using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// Markdown 渲染宿主：
/// - see-md.local（未映射）：播放容器页 / CSS / 渲染 HTML 片段（可 WebResourceRequested）
/// - mdcontent.local（目录映射）：md 所在目录，供相对图片解析
/// 注意：映射域上 WebResourceRequested 不触发，故 /data 绝不能挂在 AssetsHost 上。
/// </summary>
public partial class MarkdownWebHost : WebViewHostBase
{
    public const string MarkdownDataHost = "see-md.local";
    private const string ContentHost = "mdcontent.local";

    private string? _basePath;

    public MarkdownWebHost()
    {
        InitializeComponent();
    }

    /// <summary>加载渲染结果：basePath 是 md 文件路径（取目录映射）。</summary>
    public Task LoadAsync(string basePath)
    {
        _basePath = basePath;
        NavigateOrPending($"https://{MarkdownDataHost}/markdown-preview.html");
        return Task.CompletedTask;
    }

    protected override void Configure(CoreWebView2 core)
    {
        // 不把 MarkdownDataHost 做文件夹映射，否则 /data 拦截失效。
        if (_basePath is not null)
        {
            var dir = Path.GetDirectoryName(_basePath);
            if (!string.IsNullOrEmpty(dir))
                core.SetVirtualHostNameToFolderMapping(
                    ContentHost, dir, CoreWebView2HostResourceAccessKind.Allow);
        }

        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{MarkdownDataHost}/*", CoreWebView2WebResourceContext.All);
        core.WebMessageReceived += OnWebMessageReceived;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;

        if (_basePath is not null)
            NavigateOrPending($"https://{MarkdownDataHost}/markdown-preview.html");
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(MarkdownDataHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string path = uri.AbsolutePath;
        if (path.Equals("/markdown-preview.html", StringComparison.OrdinalIgnoreCase))
        {
            ServeAsset(e, "markdown-preview.html", "text/html; charset=utf-8");
            return;
        }

        if (path.Equals("/markdown.css", StringComparison.OrdinalIgnoreCase))
        {
            ServeAsset(e, "markdown.css", "text/css; charset=utf-8");
            return;
        }

        if (path.Equals("/data", StringComparison.OrdinalIgnoreCase))
        {
            ServeRenderedHtml(e);
            return;
        }

        e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
    }

    private static void ServeAsset(CoreWebView2WebResourceRequestedEventArgs e, string fileName, string contentType)
    {
        try
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, "webassets", fileName);
            var stream = File.OpenRead(fullPath);
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                stream, 200, "OK", $"Content-Type: {contentType}\nCache-Control: no-cache");
        }
        catch (Exception ex)
        {
            var bytes = Encoding.UTF8.GetBytes(ex.Message);
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                new MemoryStream(bytes), 500, "Error", "Content-Type: text/plain; charset=utf-8");
        }
    }

    private void ServeRenderedHtml(CoreWebView2WebResourceRequestedEventArgs e)
    {
        var vm = DataContext as MarkdownContentViewModel;
        if (vm?.Html is null)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(vm.Html);
        e.Response = SharedEnvironment.CreateWebResourceResponse(
            new MemoryStream(bytes), 200, "OK", "Content-Type: text/html; charset=utf-8");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var vm = DataContext as MarkdownContentViewModel;
        if (vm is null) return;
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (json?.Contains("\"type\":\"error\"") == true)
                Dispatcher.Invoke(() => vm.RenderError = ExtractMessage(json));
        }
        catch
        {
            // 非字符串消息忽略
        }
    }

    private static string ExtractMessage(string json)
    {
        int idx = json.IndexOf("\"message\":\"", StringComparison.Ordinal);
        if (idx < 0) return json;
        int start = idx + 11;
        int end = json.IndexOf('"', start);
        return end > start ? json[start..end] : json;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var host = new Uri(e.Uri).Host;
        if (host.Equals(MarkdownDataHost, StringComparison.OrdinalIgnoreCase)
            || host.Equals(ContentHost, StringComparison.OrdinalIgnoreCase))
            return;
        e.Cancel = true;
        OpenExternally(e.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Uri is not null) OpenExternally(e.Uri);
    }

    private static void OpenExternally(string url)
    {
        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打开失败忽略
        }
    }

    protected override void OnDetach(CoreWebView2 core)
    {
        core.WebResourceRequested -= OnWebResourceRequested;
        core.WebMessageReceived -= OnWebMessageReceived;
        core.NavigationStarting -= OnNavigationStarting;
        core.NewWindowRequested -= OnNewWindowRequested;
    }
}
