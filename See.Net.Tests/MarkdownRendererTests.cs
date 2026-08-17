using See.Net.Core.Markdown;

namespace See.Net.Tests;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void ToHtml_Renders_Headings()
    {
        string html = MarkdownRenderer.ToHtml("# 标题\n\n正文段落");
        // 自动标题锚点扩展会附加 id 属性
        Assert.Contains("标题</h1>", html);
        Assert.Contains("<p>正文段落</p>", html);
    }

    [Fact]
    public void ToHtml_Renders_Table()
    {
        string md = "| A | B |\n| --- | --- |\n| 1 | 2 |";
        string html = MarkdownRenderer.ToHtml(md);
        Assert.Contains("<table>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void ToHtml_Renders_Fenced_Code_Block()
    {
        string html = MarkdownRenderer.ToHtml("```cs\nConsole.WriteLine();\n```");
        Assert.Contains("<pre><code", html);
        Assert.Contains("Console.WriteLine();", html);
    }

    [Fact]
    public void ToHtml_Renders_Links_And_Images()
    {
        string html = MarkdownRenderer.ToHtml("[文本](https://example.com)\n\n![图](img/a.png)");
        Assert.Contains("<a href=\"https://example.com\">文本</a>", html);
        Assert.Contains("<img src=\"img/a.png\"", html);
    }

    [Fact]
    public void ToHtml_Escapes_Raw_Html()
    {
        // DisableHtml：原始 HTML 输出为转义文本，脚本不会进入 DOM
        string html = MarkdownRenderer.ToHtml("<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void ToHtml_Throws_When_Too_Large()
    {
        string big = new string('a', MarkdownRenderer.MaxRenderChars + 1);
        Assert.Throws<InvalidOperationException>(() => MarkdownRenderer.ToHtml(big));
    }
}
