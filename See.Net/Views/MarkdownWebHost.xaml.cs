using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// Markdown 渲染宿主：webassets 提供渲染容器页与样式，
/// md 所在目录映射为虚拟主机（相对图片解析），HTML 片段经 /data 拦截回吐。
/// </summary>
public partial class MarkdownWebHost : WebViewHostBase
{
    private const string ContentHost = "mdcontent.local";

    private string? _basePath;

    public MarkdownWebHost()
    {
        InitializeComponent();
    }

    /// <summary>加载渲染结果：basePath 是 md 文件路径（取目录映射 + base href）。</summary>
    public Task LoadAsync(string basePath)
    {
        _basePath = basePath;
        var dir = Path.GetDirectoryName(basePath) ?? ".";
        // base 参数经 URL 编码传给页面，页面据此插入 <base>（目录映射 + 相对图片）
        var encoded = Uri.EscapeDataString(dir.Replace('\\', '/'));
        NavigateOrPending($"https://{AssetsHost}/markdown-preview.html?base={encoded}");
        return Task.CompletedTask;
    }

    protected override void Configure(CoreWebView2 core)
    {
        MapAssets(core);

        if (_basePath is not null)
        {
            var dir = Path.GetDirectoryName(_basePath);
            if (!string.IsNullOrEmpty(dir))
                core.SetVirtualHostNameToFolderMapping(
                    ContentHost, dir, CoreWebView2HostResourceAccessKind.Allow);
        }

        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{AssetsHost}/data", CoreWebView2WebResourceContext.Other);
        core.WebMessageReceived += OnWebMessageReceived;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
    }

    /// <summary>拦截 data 请求，回吐视图模型 Html 的 UTF-8 字节。</summary>
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var vm = DataContext as MarkdownContentViewModel;
        if (vm?.Html is null || !e.Request.Uri.EndsWith("/data", StringComparison.Ordinal))
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(vm.Html);
        var stream = new MemoryStream(bytes);
        e.Response = SharedEnvironment.CreateWebResourceResponse(
            stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
    }

    /// <summary>接收渲染页 postMessage 的错误上报。</summary>
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

    /// <summary>虚拟域之外的顶级导航取消并交给系统浏览器（外链不内嵌跳转）。</summary>
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var host = new Uri(e.Uri).Host;
        if (host == AssetsHost || host == ContentHost) return;
        e.Cancel = true;
        OpenExternally(e.Uri);
    }

    /// <summary>新窗口一律外部打开，不在预览内弹出。</summary>
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
            // 打开失败忽略（无默认浏览器等场景）
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
