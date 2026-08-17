namespace See.Net.Core;

/// <summary>目录枚举与导航辅助。</summary>
public static class FileSystemService
{
    public static List<FileEntry> Enumerate(string directory)
    {
        var list = new List<FileEntry>();

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(directory))
            {
                try
                {
                    var di = new DirectoryInfo(dir);
                    list.Add(new FileEntry
                    {
                        Name = di.Name,
                        FullPath = di.FullName,
                        IsDirectory = true,
                        LastWriteTime = di.LastWriteTime,
                    });
                }
                catch { /* 跳过无权限条目 */ }
            }
        }
        catch { /* 目录枚举失败时仅返回已有结果 */ }

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                try
                {
                    var fi = new FileInfo(file);
                    list.Add(new FileEntry
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        Length = fi.Length,
                        LastWriteTime = fi.LastWriteTime,
                        Kind = FileTypeDetector.Detect(fi.FullName, readContent: false),
                    });
                }
                catch { /* 跳过无权限条目 */ }
            }
        }
        catch { /* 目录枚举失败时仅返回已有结果 */ }

        list.Sort((a, b) =>
        {
            if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    public static string? GetParent(string directory)
    {
        try
        {
            return Directory.GetParent(directory)?.FullName;
        }
        catch
        {
            return null;
        }
    }

    public static List<FileEntry> GetDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady || d.DriveType == DriveType.Fixed)
            .Select(d => new FileEntry
            {
                Name = d.Name,
                FullPath = d.RootDirectory.FullName,
                IsDirectory = true,
            })
            .ToList();
    }
}
