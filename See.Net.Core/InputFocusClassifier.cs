namespace See.Net.Core;

/// <summary>
/// 判断按键发生时前台窗口与焦点控件（纯逻辑，便于单元测试）。
/// 用于全局空格预览时避免在输入框、无资源管理器场景消费空格。
/// </summary>
public static class InputFocusClassifier
{
    public static bool IsExplorerClass(string? className) =>
        className is "CabinetWClass" or "ExploreWClass";

    public static bool IsLikelyInputControl(string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;
        string upper = className.ToUpperInvariant();
        if (upper.Contains("EDIT")) return true;       // Edit / RichEdit20W / TextBoxEdit 等
        if (upper.Contains("COMBO")) return true;      // ComboBox 编辑区
        if (upper.Contains("TEXTBOX")) return true;
        if (upper.Contains("SCINTILLA")) return true;  // Notepad++ 等 Scintilla 编辑器
        if (upper.Contains("CHROME_WIDGET")) return true; // Chromium 内核输入框
        return false;
    }
}
