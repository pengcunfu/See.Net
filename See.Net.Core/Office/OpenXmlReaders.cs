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
            var sharedTable = workbook.SharedStringTablePart?.SharedStringTable;
            if (sharedTable is not null)
            {
                foreach (var item in sharedTable.Elements<SharedStringItem>())
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

            var workbookSheets = workbook.Workbook?.Sheets;
            if (workbookSheets is null) return new SheetSetModel();

            foreach (var sheet in workbookSheets.Elements<Sheet>())
            {
                string? sheetId = sheet.Id?.Value;
                if (string.IsNullOrEmpty(sheetId)) continue;
                var part = workbook.GetPartById(sheetId) as WorksheetPart;
                var worksheet = part?.Worksheet;
                if (part is null || worksheet is null) continue;

                string sheetName = sheet.Name?.Value ?? "Sheet";

                // 预估行数，如果过大则跳过详细读取
                long? sheetRows = TryGetDimensionRows(part);
                if (sheetRows > OfficeDocumentReader.MaxSheetRows)
                {
                    sheets.Add(new SheetData
                    {
                        Name = sheetName,
                        Rows = Array.Empty<string[]>(),
                        Truncated = true,
                    });
                    totalRows += sheetRows ?? 0;
                    anyTruncated = true;
                    continue;
                }

                var rows = new List<string[]>();
                int maxColumns = 0;
                foreach (var row in worksheet.Elements<Row>())
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
                    Name = sheetName,
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

        long widthEmu = 0, heightEmu = 0;
        if (pres.Presentation?.SlideSize is { } sldSz)
        {
            widthEmu = sldSz.Cx?.Value ?? 0;
            heightEmu = sldSz.Cy?.Value ?? 0;
        }

        var slideParts = EnumerateSlidePartsInOrder(pres).ToList();
        var slides = new List<SlideData>(slideParts.Count);
        long totalImageBytes = 0;
        bool imagesTruncated = false;
        int index = 1;

        foreach (var slidePart in slideParts)
        {
            var tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
            var title = "";
            var lines = new List<string>();
            var images = new List<SlideImageData>();

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

                if (totalImageBytes < OfficeDocumentReader.MaxTotalSlideImageBytes)
                {
                    foreach (var picture in tree.Descendants<P.Picture>())
                    {
                        if (images.Count >= OfficeDocumentReader.MaxImagesPerSlide)
                        {
                            imagesTruncated = true;
                            break;
                        }

                        if (TryReadPicture(slidePart, picture, out var image, out bool skippedByBudget))
                        {
                            long next = totalImageBytes + image.Bytes.Length;
                            if (next > OfficeDocumentReader.MaxTotalSlideImageBytes)
                            {
                                imagesTruncated = true;
                                break;
                            }
                            images.Add(image);
                            totalImageBytes = next;
                        }
                        else if (skippedByBudget)
                        {
                            imagesTruncated = true;
                        }
                    }
                }
                else
                {
                    // 总预算已满：若本页仍有图片则标记截断
                    if (tree.Descendants<P.Picture>().Any())
                        imagesTruncated = true;
                }
            }

            slides.Add(new SlideData
            {
                Index = index,
                Title = title,
                Lines = lines,
                Images = images,
            });
            index++;
        }

        return new SlidesModel
        {
            Slides = slides,
            SlideWidthEmu = widthEmu,
            SlideHeightEmu = heightEmu,
            ImagesTruncated = imagesTruncated,
        };
    }

    /// <summary>按 SlideIdList 顺序枚举幻灯片；列表缺失或关系无效时回退 SlideParts。</summary>
    private static IEnumerable<SlidePart> EnumerateSlidePartsInOrder(PresentationPart pres)
    {
        var idList = pres.Presentation?.SlideIdList;
        if (idList is not null)
        {
            var ordered = new List<SlidePart>();
            foreach (var slideId in idList.Elements<SlideId>())
            {
                var relId = slideId.RelationshipId?.Value;
                if (string.IsNullOrEmpty(relId)) continue;
                try
                {
                    if (pres.GetPartById(relId) is SlidePart part)
                        ordered.Add(part);
                }
                catch
                {
                    // 损坏关系跳过
                }
            }

            if (ordered.Count > 0)
            {
                foreach (var part in ordered)
                    yield return part;
                yield break;
            }
        }

        foreach (var part in pres.SlideParts)
            yield return part;
    }

    /// <summary>
    /// 从 p:pic 读取 ImagePart 字节。skippedByBudget=true 表示因单图过大跳过。
    /// </summary>
    private static bool TryReadPicture(
        SlidePart slidePart,
        P.Picture picture,
        out SlideImageData image,
        out bool skippedByBudget)
    {
        image = null!;
        skippedByBudget = false;

        var embed = picture.BlipFill?.Blip?.Embed?.Value;
        if (string.IsNullOrEmpty(embed)) return false;

        OpenXmlPart? part;
        try { part = slidePart.GetPartById(embed); }
        catch { return false; }

        if (part is not ImagePart imagePart) return false;

        string contentType = imagePart.ContentType ?? "";
        if (IsSkippedImageContentType(contentType)) return false;

        try
        {
            using var stream = imagePart.GetStream();
            long length = stream.CanSeek ? stream.Length : -1;
            if (length > OfficeDocumentReader.MaxImageBytes)
            {
                skippedByBudget = true;
                return false;
            }

            using var ms = new MemoryStream(length > 0 && length <= int.MaxValue ? (int)length : 0);
            stream.CopyTo(ms);
            if (ms.Length == 0 || ms.Length > OfficeDocumentReader.MaxImageBytes)
            {
                if (ms.Length > OfficeDocumentReader.MaxImageBytes)
                    skippedByBudget = true;
                return false;
            }

            image = new SlideImageData
            {
                ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType,
                Bytes = ms.ToArray(),
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSkippedImageContentType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;
        // WMF/EMF 在 WPF BitmapImage 中通常不可解码
        return contentType.Contains("x-emf", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("x-wmf", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("image/x-emf", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("image/x-wmf", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/x-emf", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/emf", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/wmf", StringComparison.OrdinalIgnoreCase);
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
