using System.IO;

namespace See;

/// <summary>临时文件 + 原子替换的写入工具。</summary>
public static class AtomicFile
{
    public static async Task WriteAsync(string path, byte[] content)
    {
        string fullPath = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(fullPath) ?? ".";
        string temp = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, content);
            File.Move(temp, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch { /* 忽略 */ }
            throw;
        }
    }
}
