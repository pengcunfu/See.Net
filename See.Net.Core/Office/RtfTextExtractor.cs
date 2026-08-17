using System.Text;

namespace See.Net.Core.Office;

/// <summary>RTF 纯文本提取：字节级 tokenizer，处理 \par / \tab / \'hh 代码页字节 / \uN Unicode。</summary>
public static class RtfTextExtractor
{
    /// <summary>RTF 解析的输入上限，超过按截断处理。</summary>
    public const int MaxInputBytes = 20 * 1024 * 1024;

    static RtfTextExtractor() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static WordBlocksModel Read(string path)
    {
        using var fs = OfficeDocumentReader.OpenShared(path);
        return Read(fs);
    }

    public static WordBlocksModel Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms, 81920);
        byte[] bytes = ms.ToArray();
        if (ms.Length > MaxInputBytes)
        {
            return new WordBlocksModel
            {
                Blocks = [new WordBlock { Kind = WordBlockKind.Paragraph, Text = "文件过大，已跳过 RTF 文本提取。" }],
                TotalParagraphs = 1,
            };
        }

        Encoding cp = DetectEncoding(bytes);
        string text = StripRtf(bytes, cp);

        var blocks = text.Split('\n')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select(s => new WordBlock { Kind = WordBlockKind.Paragraph, Text = s })
            .ToList();
        return new WordBlocksModel { Blocks = blocks, TotalParagraphs = blocks.Count };
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        int cp = 1252; // RTF 缺省 ANSI 代码页
        // \ansicpg 位于文件头 preamble（\rtf1\ansi\ansicpgN...），只扫头部即可
        int limit = Math.Min(bytes.Length - 8, 1024);
        for (int i = 0; i < limit; i++)
        {
            if (bytes[i] == '\\' && bytes[i + 1] == 'a' && bytes[i + 2] == 'n' && bytes[i + 3] == 's'
                && bytes[i + 4] == 'i' && bytes[i + 5] == 'c' && bytes[i + 6] == 'p' && bytes[i + 7] == 'g')
            {
                int j = i + 8;
                int value = 0;
                while (j < bytes.Length && bytes[j] >= '0' && bytes[j] <= '9')
                {
                    value = value * 10 + (bytes[j] - '0');
                    j++;
                }
                if (value > 0) cp = value;
                break;
            }
        }

        try { return Encoding.GetEncoding(cp, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback); }
        catch { return Encoding.GetEncoding(1252); }
    }

    private static string StripRtf(byte[] bytes, Encoding cp)
    {
        var sb = new StringBuilder(bytes.Length / 2);
        var raw = new List<byte>();
        int i = 0, n = bytes.Length;
        int ucSkip = 1; // \ucN 指定 \u 后回退字符字节数

        void FlushRaw()
        {
            if (raw.Count == 0) return;
            sb.Append(cp.GetString(raw.ToArray()));
            raw.Clear();
        }

        while (i < n)
        {
            byte b = bytes[i];
            if (b == '{')
            {
                FlushRaw();
                if (PeekIs(bytes, i + 1, '\\') && PeekIs(bytes, i + 2, '*'))
                {
                    // 跳过 {\*...} 隐藏组
                    int end = FindMatchingBrace(bytes, i, n);
                    i = end + 1;
                }
                else if (PeekIs(bytes, i + 1, '\\') && IsNonTextGroup(bytes, i + 2, n, out int wordEnd))
                {
                    // {\fonttbl ...}、{\colortbl ...} 等纯格式组，整组跳过
                    int end = FindMatchingBrace(bytes, i, n);
                    i = end + 1;
                }
                else
                {
                    i++;
                }
                continue;
            }
            if (b == '}')
            {
                FlushRaw();
                i++;
                continue;
            }
            if (b == '\\')
            {
                // \'hh 十六进制转义：连续的 \'hh 可能组成一个多字节字符（如 GBK 双字节），
                // 必须攒够再解码，不能在转义之间提前 FlushRaw。
                if (i + 3 < n && bytes[i + 1] == '\'' && IsHex(bytes[i + 2]) && IsHex(bytes[i + 3]))
                {
                    raw.Add(HexByte(bytes[i + 2], bytes[i + 3]));
                    i += 4;
                    continue;
                }
                FlushRaw();
                i++;
                int nameStart = i;
                while (i < n && IsLetter(bytes[i])) i++;
                string name = Encoding.ASCII.GetString(bytes, nameStart, i - nameStart);

                long param = 0;
                bool hasParam = false;
                if (i < n && (bytes[i] == '-' || IsDigit(bytes[i])))
                {
                    hasParam = true;
                    bool neg = bytes[i] == '-';
                    if (neg) i++;
                    while (i < n && IsDigit(bytes[i]))
                    {
                        param = param * 10 + (bytes[i] - '0');
                        i++;
                    }
                    if (neg) param = -param;
                }
                if (i < n && bytes[i] == ' ')
                {
                    i++; // 控制字/参数后的分隔空格
                }

                if (name.Length == 0 && i < n)
                {
                    // 控制符号：\\ \{ \} \'hh \~ \_ \-
                    byte sym = bytes[i];
                    i++;
                    switch (sym)
                    {
                        case (byte)'\\': sb.Append('\\'); break;
                        case (byte)'{': sb.Append('{'); break;
                        case (byte)'}': sb.Append('}'); break;
                        case (byte)'~':
                        case (byte)'_': sb.Append(' '); break;
                        default: break; // 其余控制符号（\'hh 已在循环头部处理）
                    }
                    continue;
                }

                switch (name.ToLowerInvariant())
                {
                    case "par" or "line" or "page" or "sect": sb.Append('\n'); break;
                    case "tab": sb.Append('\t'); break;
                    case "uc": ucSkip = (int)param; break;
                    case "u" when hasParam:
                        sb.Append((char)(ushort)param);
                        for (int k = 0; k < ucSkip && i < n; k++) i++;
                        break;
                    default: break; // 其余控制字忽略
                }
                continue;
            }
            raw.Add(b);
            i++;
        }

        FlushRaw();
        return sb.ToString();
    }

    private static bool PeekIs(byte[] bytes, int i, char c)
        => i < bytes.Length && bytes[i] == (byte)c;

    /// <summary>这些组只承载格式/元信息，不产生正文文本，整组跳过。</summary>
    private static readonly HashSet<string> NonTextGroups = new(StringComparer.Ordinal)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "field",
        "generator", "themedata", "colorschememapping", "listtable", "listoverridetable",
        "latentstyles", "rsidtbl", "xmlnstbl", "filetbl", "revtbl", "mmath", "wgrffmtfilter",
        "pnseclvl", "ptr", "brdrtbl", "shpinst", "nonshppict", "datastore",
    };

    /// <summary>组开始于 bytes[pos]（'{'），返回组结束花括号的下标；未找到返回 n-1。</summary>
    private static int FindMatchingBrace(byte[] bytes, int openPos, int n)
    {
        int depth = 0;
        for (int i = openPos; i < n; i++)
        {
            switch (bytes[i])
            {
                case (byte)'\\':
                    i++;
                    break;
                case (byte)'{':
                    depth++;
                    break;
                case (byte)'}' when --depth == 0:
                    return i;
            }
        }
        return n - 1;
    }

    private static bool IsLetter(byte b) => (b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z');

    private static bool IsDigit(byte b) => b >= '0' && b <= '9';

    private static bool IsHex(byte b)
        => IsDigit(b) || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F');

    /// <summary>bytes[pos] 起是否是“控制字 + 空格/花括号”形式且该控制字属于非文本组。</summary>
    private static bool IsNonTextGroup(byte[] bytes, int pos, int n, out int wordEnd)
    {
        wordEnd = pos;
        int i = pos;
        while (i < n && IsLetter(bytes[i])) i++;
        if (i == pos) return false;
        string word = Encoding.ASCII.GetString(bytes, pos, i - pos);
        if (!NonTextGroups.Contains(word)) return false;
        // 组名后必须是分隔空格（{\fonttbl ...}）或直接嵌套花括号（{\colortbl;...} 少见）
        if (i < n && (bytes[i] == ' ' || bytes[i] == '{'))
        {
            wordEnd = i;
            return true;
        }
        return false;
    }

    private static byte HexByte(byte hi, byte lo)
        => (byte)((HexValue(hi) << 4) | HexValue(lo));

    private static byte HexValue(byte c) => c switch
    {
        >= (byte)'0' and <= (byte)'9' => (byte)(c - '0'),
        >= (byte)'a' and <= (byte)'f' => (byte)(c - 'a' + 10),
        >= (byte)'A' and <= (byte)'F' => (byte)(c - 'A' + 10),
        _ => 0,
    };
}