using System.IO;

namespace See;

/// <summary>应用数据目录（用户文档目录下）。</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "See.Net");

    public static string SettingsPath { get; } = Path.Combine(DataDirectory, "settings.json");
    public static string BackupDirectory { get; } = Path.Combine(DataDirectory, "Backups");
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");
    /// <summary>临时预览缓存（如 PPT 导出的幻灯片 PNG）。</summary>
    public static string PreviewCacheDirectory { get; } = Path.Combine(DataDirectory, "PreviewCache");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(PreviewCacheDirectory);
    }
}
