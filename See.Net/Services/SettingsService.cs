using System.IO;
using System.Text.Json;

namespace See.Services;

/// <summary>设置读写服务。</summary>
public sealed class SettingsService
{
    private readonly object _lock = new();
    private AppSettings? _current;

    public AppSettings Current
    {
        get
        {
            lock (_lock)
            {
                if (_current is not null) return _current;
                try
                {
                    if (File.Exists(AppPaths.SettingsPath))
                    {
                        string json = File.ReadAllText(AppPaths.SettingsPath);
                        _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    }
                }
                catch
                {
                    // 设置损坏时回退默认值
                }
                _current ??= new AppSettings();
                return _current;
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            if (_current is null) return;
            AppPaths.EnsureCreated();
            string json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
            string temp = AppPaths.SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, AppPaths.SettingsPath, overwrite: true);
        }
    }
}
