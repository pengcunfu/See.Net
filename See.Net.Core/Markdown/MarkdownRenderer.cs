using Markdig;

namespace See.Net.Core.Markdown;

/// <summary>
/// Markdown → HTML 片段渲染。
/// DisableHtml 转义原始 HTML，预览任意来源文件时不执行内嵌脚本。
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>渲染输入上限（字符），超出抛出以便上层提示改用源码模式。</summary>
    public const int MaxRenderChars = 2_000_000;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions() // 表格、任务列表、删除线、自动标题锚点
        .DisableHtml()
        .Build();

    /// <summary>把 Markdown 源文本渲染为 HTML 片段（不含 html/body 包裹）。</summary>
    /// <exception cref="InvalidOperationException">输入超过 <see cref="MaxRenderChars"/>。</exception>
    public static string ToHtml(string markdown)
    {
        if (markdown.Length > MaxRenderChars)
            throw new InvalidOperationException($"Markdown 超过渲染上限 {MaxRenderChars:N0} 字符，请使用源码模式查看。");
        return Markdig.Markdown.ToHtml(markdown, Pipeline);
    }
}
