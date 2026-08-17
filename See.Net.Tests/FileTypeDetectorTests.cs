using See.Net.Core;

namespace See.Net.Tests;

public sealed class FileTypeDetectorTests : IDisposable
{
    private readonly string _dir;

    public FileTypeDetectorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SeeNetType_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略 */ }
    }

    [Fact]
    public void Detect_By_Extension()
    {
        Assert.Equal(ContentKind.Text, FileTypeDetector.ByExtension(".txt"));
        Assert.Equal(ContentKind.Code, FileTypeDetector.ByExtension(".cs"));
        Assert.Equal(ContentKind.Code, FileTypeDetector.ByExtension(".JSON"));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByExtension(".png"));
        Assert.Equal(ContentKind.Binary, FileTypeDetector.ByExtension(".exe"));
        Assert.Equal(ContentKind.Unknown, FileTypeDetector.ByExtension(".xyz123"));
    }

    [Fact]
    public void Detect_By_Magic_Bytes()
    {
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByMagic([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByMagic([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(ContentKind.Binary, FileTypeDetector.ByMagic([0x4D, 0x5A, 0x90, 0x00]));
        Assert.Equal(ContentKind.Binary, FileTypeDetector.ByMagic([0x50, 0x4B, 0x03, 0x04]));
        Assert.Equal(ContentKind.Binary, FileTypeDetector.ByMagic([0x25, 0x50, 0x44, 0x46]));
        Assert.Equal(ContentKind.Unknown, FileTypeDetector.ByMagic([0x41, 0x42, 0x43, 0x44]));
    }

    [Fact]
    public void Detect_Unknown_Content_Text_Vs_Binary()
    {
        string textPath = Path.Combine(_dir, "sample.unknownext");
        File.WriteAllText(textPath, "hello world, 中文内容");
        Assert.Equal(ContentKind.Text, FileTypeDetector.Detect(textPath));

        string binPath = Path.Combine(_dir, "sample2.unknownext");
        File.WriteAllBytes(binPath, [0x00, 0x01, 0x02, 0xFF, 0x00]);
        Assert.Equal(ContentKind.Binary, FileTypeDetector.Detect(binPath));
    }

    [Fact]
    public void LooksLikeText_Rejects_Nul()
    {
        Assert.False(FileTypeDetector.LooksLikeText([0x41, 0x00, 0x42]));
        Assert.True(FileTypeDetector.LooksLikeText(System.Text.Encoding.UTF8.GetBytes("中文abc")));
    }
}
