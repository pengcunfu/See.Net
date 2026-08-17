namespace See.Net.Core.Office;

/// <summary>办公文档大类。</summary>
public enum OfficeKind
{
    Word,
    Spreadsheet,
    Presentation,
}

/// <summary>Word 文档结构化预览模型（docx / odt / rtf）。</summary>
public sealed class WordBlocksModel
{
    public IReadOnlyList<WordBlock> Blocks { get; init; } = [];
    public bool Truncated { get; init; }
    public int TotalParagraphs { get; init; }
}

/// <summary>单个 Word 块：标题 / 段落 / 列表项 / 表格行。</summary>
public sealed class WordBlock
{
    public required WordBlockKind Kind { get; init; }
    public string Text { get; init; } = "";
    /// <summary>表格行各单元格文本。</summary>
    public IReadOnlyList<string>? Cells { get; init; }
}

public enum WordBlockKind
{
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Paragraph,
    Bullet,
    TableRow,
}

/// <summary>Excel 工作簿结构化预览模型（xlsx / ods）。</summary>
public sealed class SheetSetModel
{
    public IReadOnlyList<SheetData> Sheets { get; init; } = [];
    /// <summary>任一个工作表因行数上限被截断。</summary>
    public bool Truncated { get; init; }
    /// <summary>全部工作表实际总行数。</summary>
    public long TotalRows { get; init; }
}

public sealed class SheetData
{
    public required string Name { get; init; }
    /// <summary>列字母表头（A、B…Z、AA…）。</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];
    /// <summary>预取的行数据（string 化单元格值）。</summary>
    public IReadOnlyList<string[]> Rows { get; init; } = [];
    public bool Truncated { get; init; }
    public int MaxColumns { get; init; }
}

/// <summary>PowerPoint 演示文稿结构化预览模型（pptx / odp）。</summary>
public sealed class SlidesModel
{
    public IReadOnlyList<SlideData> Slides { get; init; } = [];
    /// <summary>幻灯片宽度（EMU，914400 EMU = 1 英寸）；未知时为 0。</summary>
    public long SlideWidthEmu { get; init; }
    /// <summary>幻灯片高度（EMU）；未知时为 0。</summary>
    public long SlideHeightEmu { get; init; }
    /// <summary>因图片总预算耗尽等原因，部分图片被跳过。</summary>
    public bool ImagesTruncated { get; init; }
}

public sealed class SlideData
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = [];
    /// <summary>本页内嵌图片（按出现顺序，已应用每页/总量护栏）。</summary>
    public IReadOnlyList<SlideImageData> Images { get; init; } = [];
    /// <summary>PowerPoint 导出的整页 PNG 路径；优先用于视觉预览。</summary>
    public string? RenderedImagePath { get; init; }
}

/// <summary>幻灯片内嵌图片原始字节（解码由 UI 层完成）。</summary>
public sealed class SlideImageData
{
    public required string ContentType { get; init; }
    public required byte[] Bytes { get; init; }
}
