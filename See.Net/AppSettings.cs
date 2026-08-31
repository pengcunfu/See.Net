namespace See;

/// <summary>应用设置（持久化到 Documents/FNSoftware/See/settings.json）。</summary>
public sealed class AppSettings
{
    public string? LastDirectory { get; set; }
    public string TextFontFamily { get; set; } = "Consolas";
    public double TextFontSize { get; set; } = 13;
    public double HexFontSize { get; set; } = 14;
    public int BytesPerRow { get; set; } = 16;
    public bool BackupEnabled { get; set; } = true;
    public bool WordWrap { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = true;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public bool TrayHintShown { get; set; }
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 760;
    public bool WindowMaximized { get; set; }
}
