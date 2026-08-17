using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using See.Net.Core.Markdown;

namespace See.Net.ViewModels;

/// <summary>
/// Markdown 双视图状态机：默认渲染（WebView2），可切换源码（复用文本编辑栈）。
/// 源码经 TextContentViewModel 承载，编辑 / 保存 / 编码切换全套复用；
/// 切回渲染时重新执行 RenderAsync，编辑内容即时可见。
/// </summary>
public sealed partial class MarkdownContentViewModel : ObservableObject, IDisposable
{
    private readonly string _path;

    public MarkdownContentViewModel(string path, TextContentViewModel source, bool canRender)
    {
        _path = path;
        Source = source;
        CanRender = canRender;
        if (!canRender) _useRendered = false; // WebView2 运行时缺失：初始即源码模式
    }

    /// <summary>源码视图模型（复用文本编辑 / 保存 / 编码栈）。</summary>
    public TextContentViewModel Source { get; }

    public string FilePath => _path;

    /// <summary>本机是否存在 WebView2 运行时（缺失时渲染视图整体降级）。</summary>
    public bool CanRender { get; }

    /// <summary>Markdig 渲染产物，宿主以 UTF-8 字节从 /data 回吐。</summary>
    [ObservableProperty]
    private string? _html;

    /// <summary>当前是否处于渲染视图。</summary>
    [ObservableProperty]
    private bool _useRendered = true;

    /// <summary>渲染失败信息（超限 / 解析异常），非空时提示并可切源码。</summary>
    [ObservableProperty]
    private string? _renderError;

    /// <summary>后台渲染当前源码；切回渲染视图前调用，保证编辑即时可见。</summary>
    public async Task RenderAsync()
    {
        try
        {
            Html = await Task.Run(() => MarkdownRenderer.ToHtml(Source.Text));
            RenderError = null;
        }
        catch (Exception ex)
        {
            Html = null;
            RenderError = ex.Message;
        }
    }

    [RelayCommand]
    private void UseRenderedView() => UseRendered = true;

    [RelayCommand]
    private void UseSourceView() => UseRendered = false;

    partial void OnUseRenderedChanged(bool value)
    {
        if (!value) return;
        // 切回渲染时刷新（异步执行，宿主在 Html 变更后重新导航）
        _ = RenderAsync();
    }

    public void Dispose()
    {
        Html = null;
    }
}
