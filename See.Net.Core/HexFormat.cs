using System.Text;

namespace See.Net.Core;

/// <summary>十六进制格式化与解析工具。</summary>
public static class HexFormat
{
    public static string FormatOffset(long offset, int minDigits = 8)
    {
        string hex = offset.ToString("X");
        return hex.Length < minDigits ? hex.PadLeft(minDigits, '0') : hex;
    }

    /// <summary>形如 "48 65 6C 6C 6F" 的十六进制串。</summary>
    public static string ToHexSpaced(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length * 3);
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(data[i].ToString("X2"));
        }
        return sb.ToString();
    }

    /// <summary>紧凑十六进制串 "48656C6C6F"。</summary>
    public static string ToHexCompact(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length * 2);
        foreach (byte b in data) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    /// <summary>C 数组风格 "0x48, 0x65, ..."。</summary>
    public static string ToCArray(ReadOnlySpan<byte> data, int bytesPerLine = 12)
    {
        var sb = new StringBuilder(data.Length * 6);
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
                if (bytesPerLine > 0 && i % bytesPerLine == 0) sb.Append('\n');
            }
            sb.Append("0x").Append(data[i].ToString("X2"));
        }
        return sb.ToString();
    }

    /// <summary>ASCII 区文本，不可打印字符以 '.' 代替。</summary>
    public static string ToAscii(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        foreach (byte b in data)
        {
            sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
        }
        return sb.ToString();
    }

    /// <summary>解析十六进制输入，容忍空格、0x 前缀、逗号、换行。</summary>
    public static bool TryParseHex(string? input, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(input)) return false;
        string cleaned = input
            .Replace("0x", "", StringComparison.OrdinalIgnoreCase)
            .Replace(",", "")
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("\r", "")
            .Replace("\n", "");
        if (cleaned.Length == 0 || cleaned.Length % 2 != 0) return false;
        foreach (char c in cleaned)
        {
            if (c is not (>= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')) return false;
        }
        var result = new byte[cleaned.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
        }
        bytes = result;
        return true;
    }

    /// <summary>解析偏移量：0x 前缀按十六进制，否则按十进制。</summary>
    public static bool TryParseOffset(string? input, out long offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(input[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
        return long.TryParse(input, out offset);
    }
}
