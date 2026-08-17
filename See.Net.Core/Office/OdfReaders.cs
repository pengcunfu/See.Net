using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace See.Net.Core.Office;

/// <summary>读取 ODF 办公格式（odt / ods / odp）文本内容：解压 content.xml 后按本地名提取。</summary>
public static class OdfReaders
{
    public static WordBlocksModel ReadWord(string path)
    {
        var text = LoadContentElement(path, "text") ?? throw new InvalidDataException("ODT 结构无效");
        var blocks = new List<WordBlock>();
        bool truncated = false;

        foreach (var child in text.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "h":
                    var kind = LocalAttrInt(child, "outline-level") switch
                    {
                        1 => WordBlockKind.Heading1,
                        2 => WordBlockKind.Heading2,
                        _ => WordBlockKind.Heading3,
                    };
                    Add(blocks, new WordBlock { Kind = kind, Text = OdfText(child) });
                    break;
                case "p":
                    Add(blocks, new WordBlock { Kind = WordBlockKind.Paragraph, Text = OdfText(child) });
                    break;
                case "list":
                    foreach (var item in child.Elements().Where(e => e.Name.LocalName == "list-item"))
                    {
                        var p = item.Elements().FirstOrDefault(e => e.Name.LocalName == "p");
                        Add(blocks, new WordBlock { Kind = WordBlockKind.Bullet, Text = OdfText(p ?? item) });
                    }
                    break;
                case "table":
                    int rows = 0;
                    foreach (var row in child.Elements().Where(e => e.Name.LocalName == "table-row"))
                    {
                        if (rows >= OfficeDocumentReader.MaxTableRows) { truncated = true; break; }
                        var cells = row.Elements()
                            .Where(e => e.Name.LocalName == "table-cell")
                            .Select(OdfText).ToArray();
                        if (cells.Length == 0) continue;
                        Add(blocks, new WordBlock { Kind = WordBlockKind.TableRow, Cells = cells });
                        rows++;
                    }
                    break;
            }
        }

        return new WordBlocksModel { Blocks = blocks, Truncated = truncated, TotalParagraphs = blocks.Count };

        void Add(List<WordBlock> list, WordBlock block)
        {
            if (block.Text.Length == 0 && block.Cells is null) return;
            if (list.Count >= OfficeDocumentReader.MaxBlocks) { truncated = true; return; }
            list.Add(block);
        }
    }

    public static SheetSetModel ReadSheet(string path)
    {
        var spreadsheet = LoadContentElement(path, "spreadsheet") ?? throw new InvalidDataException("ODS 结构无效");
        var sheets = new List<SheetData>();
        bool anyTruncated = false;
        long totalRows = 0;

        foreach (var table in spreadsheet.Elements().Where(e => e.Name.LocalName == "table"))
        {
            string name = LocalAttr(table, "name") ?? "工作表";
            var rows = new List<string[]>();
            int maxCols = 0;
            bool truncated = false;

            foreach (var row in table.Elements().Where(e => e.Name.LocalName == "table-row"))
            {
                int repeat = Math.Clamp(LocalAttrInt(row, "number-rows-repeated"), 1, 1000);
                var cells = row.Elements()
                    .Where(e => e.Name.LocalName == "table-cell")
                    .ToList();
                if (cells.Count == 0) continue;

                var values = new List<string>();
                foreach (var cell in cells)
                {
                    int colRepeat = Math.Clamp(LocalAttrInt(cell, "number-columns-repeated"), 1, 200);
                    string text = OdfText(cell);
                    for (int k = 0; k < colRepeat && values.Count < 2048; k++) values.Add(text);
                }
                while (values.Count > 0 && values[^1].Length == 0) values.RemoveAt(values.Count - 1);
                if (values.Count == 0) continue;

                for (int r = 0; r < repeat; r++)
                {
                    if (rows.Count >= OfficeDocumentReader.MaxSheetRows) { truncated = true; break; }
                    rows.Add(values.ToArray());
                }
                maxCols = Math.Max(maxCols, values.Count);
                if (truncated) break;
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

            sheets.Add(new SheetData
            {
                Name = name,
                Columns = Enumerable.Range(0, maxCols).Select(OpenXmlReaders.ColumnLetter).ToArray(),
                Rows = rows,
                Truncated = truncated,
                MaxColumns = maxCols,
            });
            anyTruncated |= truncated;
            totalRows += rows.Count;
        }

        return new SheetSetModel { Sheets = sheets, Truncated = anyTruncated, TotalRows = totalRows };
    }

    public static SlidesModel ReadSlides(string path)
    {
        var presentation = LoadContentElement(path, "presentation") ?? throw new InvalidDataException("ODP 结构无效");
        var slides = new List<SlideData>();
        int index = 1;

        foreach (var page in presentation.Elements().Where(e => e.Name.LocalName == "page"))
        {
            string title = LocalAttr(page, "name") ?? "";
            var lines = new List<string>();
            foreach (var p in page.Descendants().Where(e => e.Name.LocalName == "p"))
            {
                string text = OdfText(p);
                if (text.Length == 0) continue;
                if (title.Length == 0 && lines.Count == 0) title = text;
                else lines.Add(text);
            }
            slides.Add(new SlideData { Index = index, Title = title, Lines = lines });
            index++;
        }

        return new SlidesModel { Slides = slides };
    }

    private static XElement? LoadContentElement(string path, string bodyLocal)
    {
        using var outer = OfficeDocumentReader.OpenShared(path);
        using var zip = new ZipArchive(outer, ZipArchiveMode.Read);
        var entry = zip.GetEntry("content.xml") ?? throw new InvalidDataException("ODF 文件缺少 content.xml");
        using var stream = entry.Open();
        var doc = XDocument.Load(stream, LoadOptions.None);
        return doc.Root
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == "body")
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == bodyLocal);
    }

    private static string? LocalAttr(XElement e, string local)
        => e.Attributes().FirstOrDefault(a => a.Name.LocalName == local)?.Value;

    private static int LocalAttrInt(XElement e, string local)
        => int.TryParse(LocalAttr(e, local), out int v) ? v : 0;

    /// <summary>拼接元素内文本：text:p 正文、text:s 空格（text:c 次数）、text:tab、text:line-break。</summary>
    private static string OdfText(XElement e)
    {
        var sb = new StringBuilder();
        foreach (var node in e.DescendantNodes())
        {
            switch (node)
            {
                case XText t:
                    sb.Append(t.Value);
                    break;
                case XElement child when child.Name.LocalName == "s":
                    int count = LocalAttrInt(child, "c");
                    sb.Append(new string(' ', count > 0 ? count : 1));
                    break;
                case XElement child when child.Name.LocalName == "tab":
                    sb.Append('\t');
                    break;
                case XElement child when child.Name.LocalName is "line-break" or "text:line-break":
                    sb.Append('\n');
                    break;
            }
        }
        return sb.ToString().Trim();
    }
}