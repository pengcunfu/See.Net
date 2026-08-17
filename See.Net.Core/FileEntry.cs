namespace See.Net.Core;

/// <summary>内容类型分类。</summary>
public enum ContentKind
{
    Text,
    Code,
    Binary,
    Image,
    Unknown,
}

/// <summary>文件列表条目（纯逻辑，无 UI 依赖）。</summary>
public sealed class FileEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Length { get; init; }
    public DateTime LastWriteTime { get; init; }
    public ContentKind Kind { get; init; }

    public string SizeText => IsDirectory ? string.Empty : FormatSize(Length);

    public string KindText => Kind switch
    {
        ContentKind.Text => "文本",
        ContentKind.Code => "代码",
        ContentKind.Binary => "二进制",
        ContentKind.Image => "图片",
        _ => IsDirectory ? "文件夹" : "未知",
    };

    /// <summary>Segoe MDL2 Assets 字形，用于列表图标。</summary>
    public string Glyph => IsDirectory ? "\uE8B7" : Kind switch
    {
        ContentKind.Image => "\uEB9F",
        ContentKind.Code => "\uE943",
        _ => "\uE8A5",
    };

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return string.Empty;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
