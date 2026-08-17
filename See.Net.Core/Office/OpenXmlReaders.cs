using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using P = DocumentFormat.OpenXml.Presentation;

namespace See.Net.Core.Office;

/// <summary>基于 DocumentFormat.OpenXml 读取 docx / xlsx / pptx 的结构化内容。</summary>
internal static class OpenXmlReaders
{
    public static WordBlocksModel ReadWord(string path)
    {
        // 验证文档格式
        try
        {
            using var validationFs = OfficeDocumentReader.OpenShared(path);
            using var validationDoc = WordprocessingDocument.Open(validationFs, false);
            var validationBody = validationDoc.MainDocumentPart?.Document?.Body;
            if (validationBody is null) return new WordBlocksModel();
        }
        catch (FileFormatException ex)
        {
            throw new InvalidDataException($"Word文档格式损坏: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException)
        {
            throw new InvalidDataException($"读取Word文档失败: {ex.Message}", ex);
        }
        
        // 重新打开文档进行处理
        using var fs = OfficeDocumentReader.OpenShared(path);
        using var doc = WordprocessingDocument.Open(fs, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return new WordBlocksModel();

        var blocks = new List<WordBlock>();
        int totalParagraphs = 0;
        bool truncated = false;

        foreach (var element in body.ChildElements)
        {
            if (blocks.Count >= OfficeDocumentReader.MaxBlocks)
            {
                truncated = true;
                break;
            }
            switch (element)
            {
                case Word.Paragraph p:
                    totalParagraphs++;
                    AddParagraphBlock(blocks, p);
                    break;
                case Word.Table table:
                    int rows = 0;
                    foreach (var row in table.ChildElements.OfType<Word.TableRow>())
                    {
                        if (blocks.Count >= OfficeDocumentReader.MaxBlocks || rows >= OfficeDocumentReader.MaxTableRows)
                        {
                            truncated = true;
                            break;
                        }
                        var cells = row.ChildElements.OfType<Word.TableCell>()
                            .Select(c => c.InnerText.Trim()).ToArray();
                        blocks.Add(new WordBlock { Kind = WordBlockKind.TableRow, Cells = cells });
                        rows++;
                    }
                    break;
            }
        }

        return new WordBlocksModel
        {
            Blocks = blocks,
            TotalParagraphs = totalParagraphs,
            Truncated = truncated || totalParagraphs > OfficeDocumentReader.MaxBlocks,
        };
    }

    private static void AddParagraphBlock(List<WordBlock> blocks, Word.Paragraph p)
    {
        string text = p.InnerText.Trim();
        if (text.Length == 0) return;

        var props = p.ParagraphProperties;
        if (props?.NumberingProperties is not null)
        {
            blocks.Add(new WordBlock { Kind = WordBlockKind.Bullet, Text = text });
            return;
        }

        string? styleId = props?.ParagraphStyleId?.Val?.Value;
        var heading = HeadingKind(styleId);
        if (heading != WordBlockKind.Paragraph)
        {
            blocks.Add(new WordBlock { Kind = heading, Text = text });
            return;
        }

        blocks.Add(new WordBlock { Kind = WordBlockKind.Paragraph, Text = text });
    }

    private static WordBlockKind HeadingKind(string? styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId)) return WordBlockKind.Paragraph;
        var m = Regex.Match(styleId, @"(?i)heading\s*([1-9])");
        if (!m.Success) return WordBlockKind.Paragraph;
        return m.Groups[1].Value[0] switch
        {
            '1' => WordBlockKind.Heading1,
            '2' => WordBlockKind.Heading2,
            '3' => WordBlockKind.Heading3,
            '4' => WordBlockKind.Heading4,
            '5' => WordBlockKind.Heading5,
            '6' => WordBlockKind.Heading6,
            _ => WordBlockKind.Paragraph,
        };
    }

    public static SheetSetModel ReadSheet(string path)
    {
        try
        {
            using var fs = OfficeDocumentReader.OpenShared(path);
            using var doc = SpreadsheetDocument.Open(fs, false);
            var workbook = doc.WorkbookPart;
            if (workbook is null) return new SheetSetModel();

            // 先读取共享字符串，有大小限制
            var sharedStrings = new List<string>();
            bool sharedTruncated = false;
            var sharedPart = workbook.SharedStringTablePart;
            if (sharedPart is not null)
            {
                foreach (var item in sharedPart.SharedStringTable.Elements<SharedStringItem>())
                {
                    if (sharedStrings.Count >= OfficeDocumentReader.MaxSharedStrings)
                    {
                        sharedTruncated = true;
                        break;
                    }
                    sharedStrings.Add(item.Text?.Text ?? "");
                }
            }

            var sheets = new List<SheetData>();
            long totalRows = 0;
            bool anyTruncated = false;

            foreach (var sheet in workbook.Workbook.Sheets.Elements<Sheet>())
            {
                var part = workbook.GetPartById(sheet.Id) as WorksheetPart;
                if (part is null) continue;

                // 预估行数，如果过大则跳过详细读取
                long? sheetRows = TryGetDimensionRows(part);
                if (sheetRows > OfficeDocumentReader.MaxSheetRows)
                {
                    sheets.Add(new SheetData
                    {
                        Name = sheet.Name,
                        Rows = Array.Empty<string[]>(),
                        Truncated = true,
                    });
                    totalRows += sheetRows ?? 0;
                    anyTruncated = true;
                    continue;
                }

                var rows = new List<string[]>();
                int maxColumns = 0;
                foreach (var row in part.Worksheet.Elements<Row>())
                {
                    if (rows.Count >= OfficeDocumentReader.MaxSheetRows)
                    {
                        anyTruncated = true;
                        break;
                    }

                    var cells = new List<string>();
                    foreach (var cell in row.Elements<Cell>())
                    {
                        string value = GetCellValue(cell, sharedStrings, sharedTruncated);
                        cells.Add(value);
                    }
                    if (cells.Count > maxColumns) maxColumns = cells.Count;
                    rows.Add(cells.ToArray());
                }

                sheets.Add(new SheetData
                {
                    Name = sheet.Name,
                    Rows = rows.ToArray(),
                    Truncated = anyTruncated,
                    MaxColumns = maxColumns,
                });
                totalRows += rows.Count;
            }

            return new SheetSetModel 
            { 
                Sheets = sheets.ToArray(),
                Truncated = anyTruncated,
                TotalRows = totalRows
            };
        }
        catch (FileFormatException ex)
        {
            throw new InvalidDataException($"Excel文档格式损坏: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException)
        {
            throw new InvalidDataException($"读取Excel文档失败: {ex.Message}", ex);
        }
    }

    private static string GetCellValue(Cell cell, List<string> shared, bool sharedTruncated)
    {
        string? raw = cell.CellValue?.Text;
        var type = cell.DataType?.Value;
        if (type == CellValues.SharedString)
        {
            if (int.TryParse(raw, out int idx) && idx >= 0 && idx < shared.Count)
                return shared[idx];
            return sharedTruncated ? "…" : "";
        }
        if (type == CellValues.InlineString)
            return cell.InlineString?.Text?.Text ?? "";
        if (type == CellValues.Boolean)
            return raw == "1" ? "TRUE" : "FALSE";
        return raw ?? "";
    }

    /// <summary>仅读 sheet XML 头部的 dimension ref（如 "A1:E100000"）获得真实总行数，避免整表 DOM 解析。</summary>
    private static long? TryGetDimensionRows(WorksheetPart part)
    {
        try
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            var buffer = new byte[64 * 1024];
            int read = stream.Read(buffer, 0, buffer.Length);
            string head = Encoding.UTF8.GetString(buffer, 0, read);
            var m = Regex.Match(head, @"dimension[^>]*ref=""([^""]+)""");
            if (!m.Success) return null;

            string last = m.Groups[1].Value.Split(':').Last();
            int i = last.Length - 1;
            while (i >= 0 && char.IsAsciiDigit(last[i])) i--;
            return i + 1 < last.Length && long.TryParse(last[(i + 1)..], out long n) ? n : null;
        }
        catch
        {
            return null;
        }
    }

    public static SlidesModel ReadSlides(string path)
    {
        // 验证文档格式
        try
        {
            using var validationFs = OfficeDocumentReader.OpenShared(path);
            using var validationDoc = PresentationDocument.Open(validationFs, false);
            var validationPres = validationDoc.PresentationPart;
            if (validationPres is null) throw new InvalidDataException("演示文稿结构无效");
        }
        catch (FileFormatException ex)
        {
            throw new InvalidDataException($"PowerPoint文档格式损坏: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException)
        {
            throw new InvalidDataException($"读取PowerPoint文档失败: {ex.Message}", ex);
        }
        
        // 重新打开文档进行处理
        using var fs = OfficeDocumentReader.OpenShared(path);
        using var doc = PresentationDocument.Open(fs, false);
        var pres = doc.PresentationPart ?? throw new InvalidDataException("演示文稿结构无效");

        var slides = new List<SlideData>();
        int index = 1;
        foreach (var slidePart in pres.SlideParts)
        {
            var tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
            var title = "";
            var lines = new List<string>();
            if (tree is not null)
            {
                foreach (var shape in tree.Descendants<P.Shape>())
                {
                    var paragraphs = shape.TextBody?
                        .Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>()
                        .Select(ParagraphText)
                        .Where(s => s.Length > 0)
                        .ToArray();
                    if (paragraphs is null || paragraphs.Length == 0) continue;

                    var nv = shape.NonVisualShapeProperties?
                        .ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    var phType = nv?.Type?.Value;
                    bool isTitle = phType == PlaceholderValues.Title
                        || phType == PlaceholderValues.CenteredTitle;

                    if (isTitle && title.Length == 0)
                    {
                        title = paragraphs[0];
                        lines.AddRange(paragraphs.Skip(1));
                    }
                    else
                    {
                        lines.AddRange(paragraphs);
                    }
                }
            }
            slides.Add(new SlideData { Index = index, Title = title, Lines = lines });
            index++;
        }
        return new SlidesModel { Slides = slides };
    }

    /// <summary>段落内所有 a:t 文本拼接。</summary>
    private static string ParagraphText(DocumentFormat.OpenXml.Drawing.Paragraph p)
        => string.Concat(p.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text ?? "")).Trim();

    /// <summary>0 起始列索引 → 列字母（0→A、25→Z、26→AA）。</summary>
    public static string ColumnLetter(int index)
    {
        var sb = new StringBuilder();
        int n = index + 1;
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + n % 26));
            n /= 26;
        }
        return sb.ToString();
    }
}
