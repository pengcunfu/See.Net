using System.Text;

namespace See.Net.Core;

/// <summary>基于扩展名与文件头的文件类型识别。</summary>
public static class FileTypeDetector
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csx", ".ts", ".js", ".tsx", ".jsx", ".json", ".xml", ".css", ".scss",
        ".less", ".py", ".java", ".kt", ".c", ".cpp", ".cc", ".h", ".hpp", ".go", ".rs", ".rb", ".php",
        ".sql", ".yml", ".yaml", ".toml", ".ini", ".sh", ".bat", ".cmd", ".ps1", ".psm1", ".vb", ".fs",
        ".fsx", ".proto", ".svg", ".xaml", ".axaml", ".razor", ".cshtml", ".vue", ".svelte", ".lua",
        ".swift", ".m", ".mm", ".pl", ".dart", ".r", ".scala", ".groovy", ".kt", ".kts", ".sol",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".csv", ".tsv", ".text", ".license", ".gitignore",
        ".gitattributes", ".editorconfig", ".dockerignore", ".env", ".conf", ".cfg", ".properties",
        ".gitmodules", ".nuspec", ".targets", ".props", ".sln", ".csproj", ".vcxproj", ".pubxml",
    };

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd",
    };

    private static readonly HashSet<string> WebPageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".xhtml",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".ogg", ".oga", ".m4a", ".aac", ".wma", ".opus", ".aif", ".aiff",
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".jfif",
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx", ".pptm",
        ".rtf", ".odt", ".ods", ".odp",
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".bin", ".dat", ".iso", ".img", ".zip", ".7z", ".rar", ".gz", ".tar",
        ".xz", ".bz2", ".pdf", ".mp4", ".mkv", ".avi", ".wmv",
        ".mov", ".flv", ".pak", ".so", ".a", ".lib", ".obj", ".pdb", ".db", ".sqlite",
        ".mdb", ".dmp", ".msi", ".cab", ".jar", ".apk", ".ipa", ".nupkg", ".whl", ".class", ".pyc",
        ".o", ".ko", ".efi",
    };

    public static ContentKind Detect(string path, bool readContent = true)
    {
        if (Directory.Exists(path)) return ContentKind.Unknown;

        string ext = Path.GetExtension(path);
        var byExt = ByExtension(ext);
        if (byExt != ContentKind.Unknown) return byExt;

        if (!readContent) return ContentKind.Unknown;

        var head = ReadHead(path, 4096);
        var byMagic = ByMagic(head);
        if (byMagic != ContentKind.Unknown) return byMagic;

        return LooksLikeText(head) ? ContentKind.Text : ContentKind.Binary;
    }

    public static ContentKind ByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return ContentKind.Unknown;
        if (CodeExtensions.Contains(extension)) return ContentKind.Code;
        if (TextExtensions.Contains(extension)) return ContentKind.Text;
        if (MarkdownExtensions.Contains(extension)) return ContentKind.Markdown;
        if (WebPageExtensions.Contains(extension)) return ContentKind.WebPage;
        if (ImageExtensions.Contains(extension)) return ContentKind.Image;
        if (DocumentExtensions.Contains(extension)) return ContentKind.Document;
        if (AudioExtensions.Contains(extension)) return ContentKind.Audio;
        if (BinaryExtensions.Contains(extension)) return ContentKind.Binary;
        return ContentKind.Unknown;
    }

    public static ContentKind ByMagic(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
            return ContentKind.Image;
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return ContentKind.Image;
        if (head.Length >= 4 && head[0] == (byte)'G' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'8')
            return ContentKind.Image;
        if (head.Length >= 2 && head[0] == (byte)'B' && head[1] == (byte)'M')
            return ContentKind.Image;
        if (head.Length >= 12 && head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
            && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P')
            return ContentKind.Image;
        if (head.Length >= 4 && ((head[0] == 0x49 && head[1] == 0x49 && head[2] == 0x2A && head[3] == 0x00)
            || (head[0] == 0x4D && head[1] == 0x4D && head[2] == 0x00 && head[3] == 0x2A)))
            return ContentKind.Image;
        if (head.Length >= 4 && head[0] == 0x00 && head[1] == 0x00 && head[2] == 0x01 && head[3] == 0x00)
            return ContentKind.Image;

        if (head.Length >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46)
            return ContentKind.Binary;
        if (head.Length >= 2 && head[0] == (byte)'M' && head[1] == (byte)'Z')
            return ContentKind.Binary;
        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && (head[2] == 0x03 || head[2] == 0x05 || head[2] == 0x07))
            return ContentKind.Binary;
        // OLE 复合文档（旧版 Word / Excel / PowerPoint，及 ODF）
        if (head.Length >= 8 && head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0
            && head[4] == 0xA1 && head[5] == 0xB1 && head[6] == 0x1A && head[7] == 0xE1)
            return ContentKind.Document;
        if (head.Length >= 5 && head[0] == (byte)'{' && head[1] == (byte)'\\' && head[2] == (byte)'r'
            && head[3] == (byte)'t' && head[4] == (byte)'f')
            return ContentKind.Document;
        if (head.Length >= 4 && head[0] == 0x7F && head[1] == 0x45 && head[2] == 0x4C && head[3] == 0x46)
            return ContentKind.Binary;
        if (head.Length >= 4 && head[0] == 0x37 && head[1] == 0x7A && head[2] == 0xBC && head[3] == 0xAF)
            return ContentKind.Binary;
        if (head.Length >= 4 && head[0] == 0xCA && head[1] == 0xFE && head[2] == 0xBA && head[3] == 0xBE)
            return ContentKind.Binary;

        return ContentKind.Unknown;
    }

    public static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        if (head.Length == 0) return true;
        foreach (byte b in head)
        {
            if (b == 0) return false;
        }
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            utf8.GetCharCount(head);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static byte[] ReadHead(string path, int maxBytes)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[Math.Min(maxBytes, fs.Length > 0 ? fs.Length : maxBytes)];
            int read = fs.Read(buffer, 0, buffer.Length);
            return read == buffer.Length ? buffer : buffer[..read];
        }
        catch
        {
            return [];
        }
    }
}
