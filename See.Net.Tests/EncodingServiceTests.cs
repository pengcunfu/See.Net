using System.Text;
using See.Net.Core;

namespace See.Net.Tests;

public sealed class EncodingServiceTests
{
    [Fact]
    public void Detects_Utf8_Bom()
    {
        var bytes = new UTF8Encoding(true).GetPreamble().Concat("hello"u8.ToArray()).ToArray();
        Assert.Equal(EncodingService.Utf8Bom.CodePage, EncodingService.Detect(bytes).CodePage);
    }

    [Fact]
    public void Detects_Utf16Le()
    {
        byte[] bytes = [0xFF, 0xFE, 0x48, 0x00, 0x69, 0x00];
        Assert.Equal(EncodingService.Utf16Le.CodePage, EncodingService.Detect(bytes).CodePage);
    }

    [Fact]
    public void Detects_Utf8_Without_Bom()
    {
        byte[] bytes = "hello world"u8.ToArray();
        Assert.Equal(EncodingService.Utf8.CodePage, EncodingService.Detect(bytes).CodePage);
    }

    [Fact]
    public void Detects_Gb18030_For_Chinese_Text()
    {
        byte[] bytes = EncodingService.Gb18030.GetBytes("中文测试");
        Assert.Equal(EncodingService.Gb18030.CodePage, EncodingService.Detect(bytes).CodePage);
    }

    [Fact]
    public void EncodeWithBom_Prepends_Preamble()
    {
        byte[] bytes = EncodingService.EncodeWithBom("hi", EncodingService.Utf8Bom, writeBom: true);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("hi"u8.ToArray(), bytes[3..]);
    }
}
