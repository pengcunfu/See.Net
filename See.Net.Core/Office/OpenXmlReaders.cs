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
            _ => WordBlockKind.Heading3,
        };
    }

    public static SheetSetModel ReadSheet(string path)
    {
        using var fs = OfficeDocumentReader.OpenShared(path);
        using var doc = SpreadsheetDocument.Open(fs, false);
        var workbook = doc.WorkbookPart ?? throw new InvalidDataException("工作簿结构无效");
        var (shared, sharedTruncated) = ReadSharedStrings(workbook);

        var nameById = new Dictionary<string, string>();
        var wb = workbook.Workbook;
        if (wb?.Sheets is not null)
        {
            foreach (var sheet in wb.Sheets.ChildElements.OfType<Sheet>())
            {
                if (sheet.Id?.Value is { } id && sheet.Name?.Value is { } name)
                    nameById[id] = name;
            }
        }

        var sheets = new List<SheetData>();
        bool anyTruncated = sharedTruncated;
        long totalRows = 0;
        int order = 1;
        foreach (var pair in workbook.Parts)
        {
            if (pair.OpenXmlPart is not WorksheetPart part) continue;
            string sheetName = nameById.TryGetValue(pair.RelationshipId, out var n) ? n : $"工作表 {order}";
            var (data, truncated, rows) = ReadOneSheet(sheetName, part, shared, sharedTruncated);
            sheets.Add(data);
            anyTruncated |= truncated;
            totalRows += rows;
            order++;
        }

        if (sheets.Count == 0) throw new InvalidDataException("工作簿中没有工作表");

        return new SheetSetModel { Sheets = sheets, Truncated = anyTruncated, TotalRows = totalRows };
    }

    private static (List<string>, bool) ReadSharedStrings(WorkbookPart workbook)
    {
        var list = new List<string>();
        var part = workbook.SharedStringTablePart;
        if (part is null) return (list, false);

        bool truncated = false;
        using var reader = OpenXmlReader.Create(part);
        while (reader.Read())
        {
            if (reader.ElementType != typeof(SharedStringItem)) continue;
            if (list.Count >= OfficeDocumentReader.MaxSharedStrings)
            {
                truncated = true;
                break;
            }
            var item = (SharedStringItem)reader.LoadCurrentElement()!;
            list.Add(item.InnerText.Trim());
        }
        return (list, truncated);
    }

    private static (SheetData, bool, long) ReadOneSheet(
        string name, WorksheetPart part, List<string> shared, bool sharedTruncated)
    {
        long realRows = TryGetDimensionRows(part) ?? 0;
        var rows = new List<string[]>();
        int maxCols = 0;
        bool truncated = false;

        using var reader = OpenXmlReader.Create(part);
        while (reader.Read())
        {
            if (reader.ElementType != typeof(Row)) continue;
            if (rows.Count >= OfficeDocumentReader.MaxSheetRows)
            {
                truncated = true;
                break;
            }

            var row = (Row)reader.LoadCurrentElement()!;
            var values = new List<string>();
            foreach (var cell in row.ChildElements.OfType<Cell>())
            {
                values.Add(CellText(cell, shared, sharedTruncated));
            }
            while (values.Count > 0 && values[^1].Length == 0) values.RemoveAt(values.Count - 1);
            if (values.Count == 0) continue;

            maxCols = Math.Max(maxCols, values.Count);
            rows.Add(values.ToArray());
        }

        for (int k = 0; k < rows.Count; k++)
        {
            var r = rows[k];
            if (r.Length < maxCols)
            {
                var padded = new string[maxCols];
                Array.Copy(r, padded, r.Length);
                rows[k] = padded;
            }
        }

        var data = new SheetData
        {
            Name = name,
            Columns = Enumerable.Range(0, maxCols).Select(ColumnLetter).ToArray(),
            Rows = rows,
            Truncated = truncated,
            MaxColumns = maxCols,
        };
        return (data, truncated || sharedTruncated, Math.Max(realRows, rows.Count));
    }

    private static string CellText(Cell cell, List<string> shared, bool sharedTruncated)
    {
        var type = cell.DataType?.Value;
        string? raw = cell.CellValue?.Text;
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