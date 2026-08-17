using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WebView2 = Microsoft.Web.WebView2.Wpf.WebView2;

namespace See.Views;

/// <summary>
/// WebView2 宿主基类：共享运行时探测、环境创建与释放生命周期。
/// 子类在 <see cref="Configure"/> 中完成虚拟主机映射、请求拦截与导航策略。
/// </summary>
public abstract class WebViewHostBase : System.Windows.Controls.UserControl, IDisposable
{
    /// <summary>webassets 虚拟主机域名（映射到打包内 webassets 目录）。</summary>
    public const string AssetsHost = "officeline.local";

    private static CoreWebView2Environment? _environment;
    private static readonly string UserDataFolder =
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "See.Net", "WebView2");

    private WebView2? _webView;
    protected string? _pendingNavigate;

    protected WebViewHostBase()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>本机是否安装 WebView2 运行时（Evergreen / Fixed）。</summary>
    public static bool IsRuntimeAvailable()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString() is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>子类钩子：核心初始化后配置映射 / 拦截 / 导航策略。</summary>
    protected abstract void Configure(CoreWebView2 core);

    /// <summary>子类钩子：释放前解绑事件（与 Configure 对称）。</summary>
    protected virtual void OnDetach(CoreWebView2 core)
    {
    }

    /// <summary>子类钩子：WebView2 进程异常退出后的业务侧处理（切回结构化等）。</summary>
    protected virtual void OnEngineCrashed(string message)
    {
    }

    /// <summary>把 webassets 目录映射到虚拟主机 AssetsHost，供页面引用离线资源。</summary>
    protected static void MapAssets(CoreWebView2 core)
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "webassets");
        core.SetVirtualHostNameToFolderMapping(
            AssetsHost, assetsDir, CoreWebView2HostResourceAccessKind.Allow);
    }

    /// <summary>共享的 WebView2 环境（同进程同 UserDataFolder，仅创建一次）。</summary>
    protected static async Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        _environment ??= await CoreWebView2Environment.CreateAsync(null, UserDataFolder);
        return _environment;
    }

    /// <summary>响应 /data 拦截时构造响应所需的共享环境（已由加载流程初始化）。</summary>
    protected static CoreWebView2Environment SharedEnvironment => _environment ?? throw new InvalidOperationException("WebView2 环境尚未初始化");

    /// <summary>请求导航；若核心未就绪则挂起，待加载完成后执行。</summary>
    protected void NavigateOrPending(string url)
    {
        if (_webView is not null && _webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.Navigate(url);
        }
        else
        {
            _pendingNavigate = url;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webView is not null) return;

        try
        {
            _webView = new WebView2();
            Content = _webView;

            var environment = await GetEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);

            var core = _webView.CoreWebView2;
            core.ProcessFailed += OnProcessFailed;
            Configure(core);

            if (_pendingNavigate is not null)
            {
                core.Navigate(_pendingNavigate);
                _pendingNavigate = null;
            }
        }
        catch (Exception ex)
        {
            ShowFailure($"WebView2 初始化失败：{ex.Message}");
        }
    }

    /// <summary>渲染 / 浏览器进程崩溃时尽量保住宿主，并允许重建环境。</summary>
    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                // 浏览器进程退出后共享 Environment 失效，必须丢弃以便下次重建
                if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.BrowserProcessExited)
                {
                    _environment = null;
                }

                string message =
                    $"预览引擎异常退出（{e.ProcessFailedKind}）。已切回安全模式，请使用结构化预览或关闭后重试。";

                SafeTearDownWebView();
                ShowFailure(message);
                OnEngineCrashed(message);
            }
            catch
            {
                // 崩溃收尾绝不能再抛出，否则会拖垮整个 WPF 进程
            }
        });
    }

    private void SafeTearDownWebView()
    {
        var view = _webView;
        _webView = null;
        if (view is null) return;
        try
        {
            if (view.CoreWebView2 is not null)
            {
                view.CoreWebView2.ProcessFailed -= OnProcessFailed;
                OnDetach(view.CoreWebView2);
            }
        }
        catch { /* 进程已死后解绑可能失败 */ }

        try { view.Dispose(); }
        catch { /* ignore */ }
    }

    private void ShowFailure(string message)
    {
        Content = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24),
            Foreground = System.Windows.Media.Brushes.DarkRed,
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    public virtual void Dispose()
    {
        SafeTearDownWebView();
    }
}
