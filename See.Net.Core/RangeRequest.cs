using System.Globalization;

namespace See.Net.Core;

/// <summary>HTTP Range 请求解析结果（闭区间 [Start, End]，字节偏移）。</summary>
public readonly record struct RangeSpec(long Start, long End)
{
    /// <summary>
    /// 解析 Range 头（仅支持单区间 bytes=start-end / start- / -suffix）。
    /// 返回 null 表示：无头、多区间、畸形或不可满足（调用方回退 200 全量或 416）。
    /// </summary>
    public static RangeSpec? Parse(string? header, long length)
    {
        if (string.IsNullOrWhiteSpace(header) || length <= 0) return null;

        const string prefix = "bytes=";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        string spec = header[prefix.Length..].Trim();

        // 多区间（逗号分隔）不支持
        if (spec.Contains(',')) return null;

        int dash = spec.IndexOf('-');
        if (dash < 0) return null;

        string startText = spec[..dash].Trim();
        string endText = spec[(dash + 1)..].Trim();

        long start, end;
        if (startText.Length == 0)
        {
            // 后缀区间 bytes=-N：最后 N 字节
            if (!long.TryParse(endText, NumberStyles.None, CultureInfo.InvariantCulture, out long suffix)
                || suffix <= 0) return null;
            start = Math.Max(0, length - suffix);
            end = length - 1;
        }
        else
        {
            if (!long.TryParse(startText, NumberStyles.None, CultureInfo.InvariantCulture, out start)
                || start < 0 || start >= length) return null;
            if (endText.Length == 0)
            {
                end = length - 1;
            }
            else if (!long.TryParse(endText, NumberStyles.None, CultureInfo.InvariantCulture, out end)
                || end < start)
            {
                return null;
            }
            end = Math.Min(end, length - 1);
        }

        return new RangeSpec(start, end);
    }
}
