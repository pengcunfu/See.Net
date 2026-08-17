using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using See.Net.Core;
using See.Net.Core.Office;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace See.Net.Tests;

public sealed class OfficeDocumentReaderTests : IDisposable
{
    private readonly string _dir;

    public OfficeDocumentReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SeeNetOffice_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略 */ }
    }

    private string Temp(string ext) => Path.Combine(_dir, "t_" + Guid.NewGuid().ToString("N") + ext);

    // ---------- docx ----------

    [Fact]
    public void ReadWord_Headings_Paragraphs_Table()
    {
        string path = Temp(".docx");
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Heading1" }), new W.Run(new W.Text("主标题"))),
                new W.Paragraph(new W.Run(new W.Text("第一段正文"))),
                new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Heading2" }), new W.Run(new W.Text("二级标题"))),
                new W.Paragraph(new W.ParagraphProperties(new W.NumberingProperties(new W.NumberingId { Val = 1 })), new W.Run(new W.Text("列表项"))),
                new W.Table(
                    new W.TableRow(
                        new W.TableCell(new W.Paragraph(new W.Run(new W.Text("A1")))),
                        new W.TableCell(new W.Paragraph(new W.Run(new W.Text("B1"))))),
                    new W.TableRow(
                        new W.TableCell(new W.Paragraph(new W.Run(new W.Text("A2")))),
                        new W.TableCell(new W.Paragraph(new W.Run(new W.Text("B2"))))))));
        }

        var model = (WordBlocksModel)OfficeDocumentReader.Read(path);
        Assert.Equal(WordBlockKind.Heading1, model.Blocks[0].Kind);
        Assert.Equal("主标题", model.Blocks[0].Text);
        Assert.Equal(WordBlockKind.Paragraph, model.Blocks[1].Kind);
        Assert.Equal("第一段正文", model.Blocks[1].Text);
        Assert.Equal(WordBlockKind.Heading2, model.Blocks[2].Kind);
        Assert.Equal(WordBlockKind.Bullet, model.Blocks[3].Kind);
        Assert.Equal(2, model.Blocks.Count(b => b.Kind == WordBlockKind.TableRow));
        Assert.False(model.Truncated);
    }

    // ---------- xlsx ----------

    [Fact]
    public void ReadSheet_MultipleSheets_And_CellTypes()
    {
        string path = Temp(".xlsx");
        using (var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
        {
            var wb = doc.AddWorkbookPart();
            var ws1 = wb.AddNewPart<WorksheetPart>();
            ws1.Worksheet = new S.Worksheet(new S.SheetData(
                new S.Row(
                    new S.Cell { CellReference = "A1", DataType = S.CellValues.Number, CellValue = new S.CellValue("1.5") },
                    new S.Cell { CellReference = "B1", DataType = S.CellValues.Boolean, CellValue = new S.CellValue("1") })));
            var ws2 = wb.AddNewPart<WorksheetPart>();
            ws2.Worksheet = new S.Worksheet(new S.SheetData(
                new S.Row(new S.Cell { CellReference = "A1", DataType = S.CellValues.String, CellValue = new S.CellValue("Sheet2 数据") })));
            wb.Workbook = new S.Workbook(
                new S.Sheets(
                    new S.Sheet { Name = "数据表", SheetId = 1, Id = wb.GetIdOfPart(ws1) },
                    new S.Sheet { Name = "说明", SheetId = 2, Id = wb.GetIdOfPart(ws2) }));
            wb.Workbook.Save();
        }

        var model = (SheetSetModel)OfficeDocumentReader.Read(path);
        Assert.Equal(2, model.Sheets.Count);
        Assert.Equal("数据表", model.Sheets[0].Name);
        Assert.Equal("说明", model.Sheets[1].Name);
        Assert.Equal("1.5", model.Sheets[0].Rows[0][0]);
        Assert.Equal("TRUE", model.Sheets[0].Rows[0][1]);
        Assert.Equal("Sheet2 数据", model.Sheets[1].Rows[0][0]);
        Assert.Equal("A", model.Sheets[0].Columns[0]);
    }

    [Fact]
    public void ReadSheet_SharedStrings_And_LargeSheet_Truncation()
    {
        string path = Temp(".xlsx");
        const int total = OfficeDocumentReader.MaxSheetRows + 500;
        using (var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
        {
            var wb = doc.AddWorkbookPart();
            var ws = wb.AddNewPart<WorksheetPart>();

            var sst = new S.SharedStringTable();
            for (int i = 0; i < 10; i++)
                sst.AppendChild(new S.SharedStringItem(new S.Text($"共享串{i}")));
            var sstPart = wb.AddNewPart<SharedStringTablePart>();
            sstPart.SharedStringTable = sst;

            var sheetData = new S.SheetData();
            for (int r = 0; r < total; r++)
            {
                var row = new S.Row { RowIndex = (uint)(r + 1) };
                row.AppendChild(new S.Cell { CellReference = $"A{r + 1}", DataType = S.CellValues.SharedString, CellValue = new S.CellValue((r % 10).ToString()) });
                sheetData.AppendChild(row);
            }
            ws.Worksheet = new S.Worksheet(
                new S.SheetDimension { Reference = new StringValue($"A1:A{total}") },
                sheetData);
            wb.Workbook = new S.Workbook(
                new S.Sheets(new S.Sheet { Name = "大表", SheetId = 1, Id = wb.GetIdOfPart(ws) }));
            wb.Workbook.Save();
        }

        var model = (SheetSetModel)OfficeDocumentReader.Read(path);
        Assert.True(model.Truncated);
        Assert.True(model.Sheets[0].Truncated);
        Assert.Equal(OfficeDocumentReader.MaxSheetRows, model.Sheets[0].Rows.Count);
        Assert.StartsWith("共享串", model.Sheets[0].Rows[0][0]);
        Assert.Equal(total, model.TotalRows);
    }

    // ---------- pptx ----------

    [Fact]
    public void ReadSlides_Titles_And_Lines()
    {
        string path = Temp(".pptx");
        using (var doc = PresentationDocument.Create(path, PresentationDocumentType.Presentation))
        {
            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new P.Presentation(
                new P.SlideIdList(new P.SlideId { Id = 256 }), new P.SlideSize { Cx = 9144000, Cy = 5143500 });
            var slide1 = presPart.AddNewPart<SlidePart>("rId1");
            slide1.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree(
                new P.Shape(
                    new P.NonVisualShapeProperties(new D.NonVisualDrawingProperties { Id = 2U, Name = "Title" },
                        new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Title })),
                    new P.ShapeProperties(new D.Transform2D(
                        new D.Offset { X = 0, Y = 0 },
                        new D.Extents { Cx = 100, Cy = 100 })),
                    new P.TextBody(
                        new D.BodyProperties(),
                        new D.ListStyle(),
                        new D.Paragraph(new D.Run(
                            new D.Text("第一页标题"))))))));
            presPart.Presentation.Save();
        }

        var model = (SlidesModel)OfficeDocumentReader.Read(path);
        var slide = Assert.Single(model.Slides);
        Assert.Equal(1, slide.Index);
        Assert.Equal("第一页标题", slide.Title);
    }

    // ---------- RTF ----------

    [Fact]
    public void Rtf_Strips_ControlWords_And_Decodes()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        string rtf = "{\\rtf1\\ansi\\ansicpg936\\deff0{\\fonttbl{\\f0 SimSun;}}\n"
            + "{\\*\\generator WPS Office}\n"
            + "\\pard\\qc 第一行标题\\par\n"
            + "中文 Body\\~text\\tab end\\par\n"
            + "\\'c4\\'e3\\'ba\\'c3\n"
            + "}\n";
        string path = Temp(".rtf");
        File.WriteAllText(path, rtf, Encoding.GetEncoding(936));

        var model = (WordBlocksModel)OfficeDocumentReader.Read(path);
        Assert.Contains(model.Blocks, b => b.Text.Contains("第一行标题"));
        Assert.Contains(model.Blocks, b => b.Text.Contains("中文 Body"));
        Assert.Contains(model.Blocks, b => b.Text.Contains("end"));
        // \'c4\'e3\'ba\'c3 是 GBK 的“你好”
        Assert.Contains(model.Blocks, b => b.Text.Contains("你好"));
    }

    // ---------- ODF ----------

    [Fact]
    public void Odf_Reads_Odt_Paragraphs_From_Zip()
    {
        string path = Temp(".odt");
        string content = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:body><office:text>
                <text:h text:outline-level="1">ODF 标题</text:h>
                <text:p>ODF 段落一</text:p>
                <text:list><text:list-item><text:p>列表项 A</text:p></text:list-item></text:list>
              </office:text></office:body>
            </office:document-content>
            """;
        using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("content.xml");
            using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
            w.Write(content);
        }

        var model = (WordBlocksModel)OfficeDocumentReader.Read(path);
        Assert.Equal(WordBlockKind.Heading1, model.Blocks[0].Kind);
        Assert.Equal("ODF 标题", model.Blocks[0].Text);
        Assert.Equal(WordBlockKind.Paragraph, model.Blocks[1].Kind);
        Assert.Equal("ODF 段落一", model.Blocks[1].Text);
        Assert.Equal(WordBlockKind.Bullet, model.Blocks[2].Kind);
    }

    // ---------- 不支持的旧格式 ----------

    [Fact]
    public void Legacy_Binary_Formats_Throw_NotSupported()
    {
        foreach (string ext in new[] { ".doc", ".xls", ".ppt" })
        {
            string path = Temp(ext);
            File.WriteAllBytes(path, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00]);
            Assert.Throws<NotSupportedException>(() => OfficeDocumentReader.Read(path));
            Assert.False(OfficeDocumentReader.CanReadStructured(ext));
        }
    }

    // ---------- Detector ----------

    [Fact]
    public void Detector_Office_Extensions_Are_Document()
    {
        foreach (string ext in new[] { ".docx", ".docm", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx", ".pptm", ".rtf", ".odt", ".ods", ".odp", ".doc" })
            Assert.Equal(ContentKind.Document, FileTypeDetector.ByExtension(ext));
        Assert.Equal(ContentKind.Document, FileTypeDetector.ByExtension(".DOCX"));
    }

    [Fact]
    public void Detector_Magic_Ole2_And_Rtf_Are_Document()
    {
        Assert.Equal(ContentKind.Document, FileTypeDetector.ByMagic(
            [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00]));
        Assert.Equal(ContentKind.Document, FileTypeDetector.ByMagic(
            Encoding.ASCII.GetBytes("{\\rtf1")));
    }
}
