using System.IO;

namespace See.Net.Services;

/// <summary>保存前的文件备份服务（备份到 Documents/See.Net/Backups）。</summary>
public sealed class BackupService
{
    private readonly SettingsService _settings;

    public BackupService(SettingsService settings) => _settings = settings;

    public void Backup(string sourcePath)
    {
        if (!_settings.Current.BackupEnabled) return;
        try
        {
            AppPaths.EnsureCreated();
            string fileName = Path.GetFileName(sourcePath);
            string stamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string target = Path.Combine(AppPaths.BackupDirectory, $"{fileName}.{stamp}.bak");
            File.Copy(sourcePath, target, overwrite: false);
        }
        catch
        {
            // 备份失败不阻塞保存，但后续保存流程会提示
        }
    }
}
