using See.Net.Core;

namespace See.Net.Tests;

public sealed class HexDocumentTests : IDisposable
{
    private readonly string _dir;

    public HexDocumentTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SeeNetTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略 */ }
    }

    private string CreateFile(params byte[] content)
    {
        string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void Open_And_Read_Works()
    {
        byte[] content = [0x00, 0x01, 0x02, 0xFF, 0x10, 0x20];
        using var doc = HexDocument.Open(CreateFile(content));
        Assert.Equal(6, doc.Length);
        Assert.False(doc.HasChanges);
        Assert.Equal(content, doc.ReadBytes(0, 6));
        Assert.Equal(new byte[] { 0x02, 0xFF }, doc.ReadBytes(2, 2));
        Assert.Equal(0x10, doc.ReadByte(4));
        Assert.Empty(doc.ReadBytes(6, 10));
    }

    [Fact]
    public void WriteBytes_Overwrites_In_Place()
    {
        using var doc = HexDocument.Open(CreateFile(0x11, 0x22, 0x33, 0x44));
        doc.WriteByte(1, 0xAA);
        Assert.Equal(new byte[] { 0x11, 0xAA, 0x33, 0x44 }, doc.ReadBytes(0, 4));
        Assert.Equal(4, doc.Length);
        Assert.True(doc.HasChanges);
        Assert.True(doc.IsEdited(1));
        Assert.False(doc.IsEdited(0));
    }

    [Fact]
    public void Insert_At_Start_Middle_And_End()
    {
        using var doc = HexDocument.Open(CreateFile(0x01, 0x02, 0x03));
        doc.InsertBytes(0, [0xAA, 0xBB]);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0x01, 0x02, 0x03 }, doc.ReadBytes(0, 5));

        doc.InsertBytes(2, [0xCC]);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0x01, 0x02, 0x03 }, doc.ReadBytes(0, 6));

        doc.InsertBytes(6, [0xDD]);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0x01, 0x02, 0x03, 0xDD }, doc.ReadBytes(0, 7));
        Assert.Equal(7, doc.Length);
    }

    [Fact]
    public void Delete_Across_Segments()
    {
        using var doc = HexDocument.Open(CreateFile(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08));
        doc.WriteByte(3, 0xAA);           // 中间改成插入段
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0xAA, 0x05, 0x06, 0x07, 0x08 }, doc.ReadBytes(0, 8));
        doc.InsertBytes(5, [0x99]);       // 再插入一个字节
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0xAA, 0x05, 0x99, 0x06, 0x07, 0x08 }, doc.ReadBytes(0, 9));
        doc.DeleteRange(2, 5);            // 跨原始段与插入段删除
        Assert.Equal(new byte[] { 0x01, 0x02, 0x07, 0x08 }, doc.ReadBytes(0, 4));
        Assert.Equal(4, doc.Length);
    }


    [Fact]
    public void Save_To_Same_File_Persists_And_Reopens()
    {
        string path = CreateFile(0x01, 0x02, 0x03, 0x04);
        using (var doc = HexDocument.Open(path))
        {
            doc.WriteByte(1, 0xFF);
            doc.InsertBytes(4, [0xEE]);
            doc.Save(path);
            Assert.False(doc.HasChanges);
            Assert.Equal(new byte[] { 0x01, 0xFF, 0x03, 0x04, 0xEE }, doc.ReadBytes(0, 5));
        }
        Assert.Equal(new byte[] { 0x01, 0xFF, 0x03, 0x04, 0xEE }, File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_To_New_Path_Writes_Copy()
    {
        string source = CreateFile(0x01, 0x02, 0x03);
        string target = Path.Combine(_dir, "target.bin");
        using (var doc = HexDocument.Open(source))
        {
            doc.WriteByte(0, 0x99);
            doc.Save(target);
        }
        Assert.Equal(new byte[] { 0x99, 0x02, 0x03 }, File.ReadAllBytes(target));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, File.ReadAllBytes(source));
    }

    [Fact]
    public void Find_Works_Across_Chunk_Boundaries()
    {
        var content = new byte[3000];
        for (int i = 0; i < content.Length; i++) content[i] = (byte)(i % 251);
        content[2046] = 0xDE;
        content[2047] = 0xAD;
        content[2048] = 0xBE;
        content[2049] = 0xEF;

        using var doc = HexDocument.Open(CreateFile(content));
        Assert.Equal(2046, doc.Find([0xDE, 0xAD, 0xBE, 0xEF]));
        Assert.Equal(-1, doc.Find([0xFE, 0xFE, 0xFE, 0xFE, 0xFE]));
    }

    [Fact]
    public void Find_Respects_Edits()
    {
        using var doc = HexDocument.Open(CreateFile(0x00, 0x11, 0x22, 0x33, 0x44));
        doc.WriteByte(1, 0xAA);
        doc.InsertBytes(3, [0xBB]);
        Assert.Equal(0, doc.Find([0x00, 0xAA, 0x22, 0xBB, 0x33]));
    }
}
