using System.IO;

namespace See.Net.Core.Office;

/// <summary>
/// Office 文档解析入口：按扩展名分发到各格式解析器。
/// 只做只读、轻量级的结构化内容提取，供快速预览使用。
/// </summary>
public static class OfficeDocumentReader
{
    /// <summary>结构化预览的最大派生块数（Word 标题/段落/表格行合计）。</summary>
    public const int MaxBlocks = 10_000;

    /// <summary>单个工作表最大预取行数，超出置截断标记。</summary>
    public const int MaxSheetRows = 10_000;

    /// <summary>Word 表格最大渲染行数。</summary>
    public const int MaxTableRows = 200;

    /// <summary>共享字符串表最大预读条数，防止超大工作簿吞内存。</summary>
    public const int MaxSharedStrings = 100_000;

    /// <summary>单页最多抽取的内嵌图片数。</summary>
    public const int MaxImagesPerSlide = 6;

    /// <summary>单张内嵌图片原始字节上限，超出跳过。</summary>
    public const int MaxImageBytes = 4 * 1024 * 1024;

    /// <summary>整份演示文稿图片字节总预算，超出后不再抽图（文字仍保留）。</summary>
    public const long MaxTotalSlideImageBytes = 48L * 1024 * 1024;

    /// <summary>能否用结构化（XML 解析）引擎读取该扩展名。二进制旧格式由网页引擎（SheetJS）兜底。</summary>
    public static bool CanReadStructured(string extension) => GetParserKind(extension) is not null;

    /// <summary>解析文档，返回对应模型（WordBlocksModel / SheetSetModel / SlidesModel）。</summary>
    public static object Read(string path)
    {
        // 验证文件
        OfficeExceptionHelper.ValidateFile(path);
        
        var kind = GetParserKind(Path.GetExtension(path));
        if (kind is null)
        {
            throw new NotSupportedException(
                $"暂不支持用结构化视图预览 {Path.GetExtension(path)} 格式" +
                $"（旧版二进制格式可切换「网页预览」尝试）。");
        }

        // 使用异常处理包装器
        return kind switch
        {
            ParserKind.OpenXmlWord => OfficeExceptionHelper.WrapOfficeOperation("Word", path, () => OpenXmlReaders.ReadWord(path)),
            ParserKind.OpenXmlSheet => OfficeExceptionHelper.WrapOfficeOperation("Excel", path, () => OpenXmlReaders.ReadSheet(path)),
            ParserKind.OpenXmlSlides => OfficeExceptionHelper.WrapOfficeOperation("PowerPoint", path, () => OpenXmlReaders.ReadSlides(path)),
            ParserKind.Rtf => OfficeExceptionHelper.WrapOfficeOperation("RTF", path, () => RtfTextExtractor.Read(path)),
            ParserKind.OdfWord => OfficeExceptionHelper.WrapOfficeOperation("ODT", path, () => OdfReaders.ReadWord(path)),
            ParserKind.OdfSheet => OfficeExceptionHelper.WrapOfficeOperation("ODS", path, () => OdfReaders.ReadSheet(path)),
            _ => OfficeExceptionHelper.WrapOfficeOperation("ODP", path, () => OdfReaders.ReadSlides(path)),
        };
    }

    /// <summary>以共享读方式打开文件，避免正在被 WPS / Word 等占用的文件打开失败。</summary>
    internal static FileStream OpenShared(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private enum ParserKind
    {
        OpenXmlWord,
        OpenXmlSheet,
        OpenXmlSlides,
        Rtf,
        OdfWord,
        OdfSheet,
        OdfSlides,
    }

    private static ParserKind? GetParserKind(string extension)
        => extension.ToLowerInvariant() switch
        {
            ".docx" or ".docm" => ParserKind.OpenXmlWord,
            ".xlsx" or ".xlsm" => ParserKind.OpenXmlSheet,
            ".pptx" or ".pptm" => ParserKind.OpenXmlSlides,
            ".rtf" => ParserKind.Rtf,
            ".odt" => ParserKind.OdfWord,
            ".ods" => ParserKind.OdfSheet,
            ".odp" => ParserKind.OdfSlides,
            _ => null,
        };
}
