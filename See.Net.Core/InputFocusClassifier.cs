namespace See.Net.Core;

/// <summary>
/// 判断按键发生时前台窗口与焦点控件（纯逻辑，便于单元测试）。
/// 用于全局空格预览时避免在输入框、无资源管理器场景消费空格。
/// </summary>
public static class InputFocusClassifier
{
    public static bool IsExplorerClass(string? className) =>
        className is "CabinetWClass" or "ExploreWClass";

    /// <summary>是否为 Windows 任务栏。</summary>
    public static bool IsTaskbar(string? className) =>
        className is "Shell_TrayWnd";

    public static bool IsLikelyInputControl(string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;
        string upper = className.ToUpperInvariant();
        if (upper.Contains("EDIT")) return true;               // Edit / RichEdit20W / TextBoxEdit 等
        if (upper.Contains("COMBO")) return true;              // ComboBox 编辑区
        if (upper.Contains("TEXTBOX")) return true;
        if (upper.Contains("SCINTILLA")) return true;          // Notepad++ 等 Scintilla 编辑器
        if (upper.Contains("CHROME_WIDGET")) return true;      // Chromium 内核输入框
        if (upper.Contains("CONSOLEWINDOW")) return true;      // CMD / PowerShell 控制台
        if (upper.Contains("CASCADIA_HOSTING")) return true;   // Windows Terminal
        if (upper.Contains("PSEUDOCONSOLE")) return true;      // ConPTY 虚拟控制台
        if (upper.Contains("VIRTUALCONSOLE")) return true;     // 其他虚拟终端
        return false;
    }
}
