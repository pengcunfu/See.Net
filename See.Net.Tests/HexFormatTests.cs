using See.Net.Core;

namespace See.Net.Tests;

public sealed class HexFormatTests
{
    [Fact]
    public void FormatOffset_Pads_To_Minimum()
    {
        Assert.Equal("00000000", HexFormat.FormatOffset(0));
        Assert.Equal("000000FF", HexFormat.FormatOffset(255));
        Assert.Equal("00001000", HexFormat.FormatOffset(4096));
    }

    [Fact]
    public void ToHexSpaced_And_Compact()
    {
        byte[] data = [0x48, 0x65, 0x6C, 0x6C, 0x6F];
        Assert.Equal("48 65 6C 6C 6F", HexFormat.ToHexSpaced(data));
        Assert.Equal("48656C6C6F", HexFormat.ToHexCompact(data));
    }

    [Fact]
    public void ToCArray_And_Ascii()
    {
        byte[] data = [0x48, 0x00, 0x65];
        Assert.Equal("0x48, 0x00, 0x65", HexFormat.ToCArray(data, bytesPerLine: 0));
        Assert.Equal("H.e", HexFormat.ToAscii(data));
    }

    [Fact]
    public void TryParseHex_Accepts_Various_Formats()
    {
        Assert.True(HexFormat.TryParseHex("48 65 6C", out var a));
        Assert.Equal(new byte[] { 0x48, 0x65, 0x6C }, a);
        Assert.True(HexFormat.TryParseHex("0x48,0x65", out var b));
        Assert.Equal(new byte[] { 0x48, 0x65 }, b);
        Assert.True(HexFormat.TryParseHex("48656C", out var c));
        Assert.Equal(new byte[] { 0x48, 0x65, 0x6C }, c);
        Assert.False(HexFormat.TryParseHex("48 6", out _));
        Assert.False(HexFormat.TryParseHex("GG", out _));
    }

    [Fact]
    public void TryParseOffset_Supports_Hex_And_Decimal()
    {
        Assert.True(HexFormat.TryParseOffset("0x100", out long a));
        Assert.Equal(256, a);
        Assert.True(HexFormat.TryParseOffset("100", out long b));
        Assert.Equal(100, b);
        Assert.False(HexFormat.TryParseOffset("abc", out _));
    }
}
