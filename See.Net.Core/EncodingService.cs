using System.Text;

namespace See.Net.Core;

/// <summary>文本编码检测与常用编码集合。</summary>
public static class EncodingService
{
    static EncodingService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding Utf8 { get; } = new UTF8Encoding(false);
    public static Encoding Utf8Bom { get; } = new UTF8Encoding(true);
    public static Encoding Utf16Le { get; } = new UnicodeEncoding(false, true);
    public static Encoding Utf16Be { get; } = new UnicodeEncoding(true, true);
    public static Encoding Gb18030 => _gb18030.Value;
    public static Encoding SystemDefault => _systemDefault.Value;

    private static readonly Lazy<Encoding> _gb18030 = new(
        () => GetStrict("GB18030"), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<Encoding> _systemDefault = new(
        () => GetStrict("GBK"), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<(string Name, Encoding Encoding, bool WriteBom)[]> _options = new(() =>
    [
        ("UTF-8", new UTF8Encoding(false), false),
        ("UTF-8 (BOM)", new UTF8Encoding(true), true),
        ("UTF-16 LE", new UnicodeEncoding(false, true), true),
        ("UTF-16 BE", new UnicodeEncoding(true, true), true),
        ("GB18030", GetStrict("GB18030"), false),
        ("ASCII", Encoding.ASCII, false),
        ("系统默认 (GBK)", GetStrict("GBK"), false),
    ], LazyThreadSafetyMode.ExecutionAndPublication);

    public static (string Name, Encoding Encoding, bool WriteBom)[] Options => _options.Value;

    public static Encoding Detect(byte[] data)
    {
        if (data is null || data.Length == 0) return Utf8;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return Utf8Bom;
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xFE && data[2] == 0x00 && data[3] == 0x00)
            return Encoding.GetEncoding("UTF-32");
        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0xFE && data[3] == 0xFF)
            return Encoding.GetEncoding("UTF-32BE");
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Utf16Le;
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return Utf16Be;

        if (IsValidUtf8(data)) return Utf8;
        if (CanDecodeStrict(data, Gb18030)) return Gb18030;
        return SystemDefault;
    }

    public static bool IsValidUtf8(ReadOnlySpan<byte> data)
    {
        try
        {
            var strict = new UTF8Encoding(false, true);
            strict.GetCharCount(data);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    public static bool CanDecodeStrict(byte[] data, Encoding encoding)
    {
        try
        {
            var strict = GetStrict(encoding.WebName);
            strict.GetCharCount(data);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    public static Encoding GetStrict(string name) =>
        Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    public static byte[] EncodeWithBom(string text, Encoding encoding, bool writeBom)
    {
        var body = encoding.GetBytes(text);
        if (!writeBom) return body;
        var bom = encoding.GetPreamble();
        if (bom.Length == 0) return body;
        var result = new byte[bom.Length + body.Length];
        Array.Copy(bom, result, bom.Length);
        Array.Copy(body, 0, result, bom.Length, body.Length);
        return result;
    }
}
