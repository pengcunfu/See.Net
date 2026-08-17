using System.IO;

namespace See.Services;

/// <summary>通过启动文件夹快捷方式实现随 Windows 启动（MSIX 下注册表 Run 会被虚拟化，故用 LNK）。</summary>
public static class AutoStartService
{
    private static string StartupLinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "See.Net.lnk");

    /// <summary>是否存在已启用且指向当前 exe 的自启动快捷方式。</summary>
    public static bool IsEnabled()
    {
        string link = StartupLinkPath;
        if (!File.Exists(link)) return false;
        try
        {
            Type? type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return false;
            dynamic shell = Activator.CreateInstance(type, true)!;
            dynamic shortcut = shell.CreateShortcut(link);
            string target = (string)shortcut.TargetPath;
            return string.Equals(target, Environment.ProcessPath ?? "", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true; // 无法解析 LNK 时按「存在即开启」处理，避免误判
        }
    }

    public static void Apply(bool enabled)
    {
        try
        {
            string link = StartupLinkPath;
            if (!enabled)
            {
                if (File.Exists(link)) File.Delete(link);
                return;
            }
            if (IsEnabled()) return;                          // 已正确安装
            if (File.Exists(link)) File.Delete(link);          // 残留失效快捷方式，重建

            Type? type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return;
            dynamic shell = Activator.CreateInstance(type, true)!;
            dynamic shortcut = shell.CreateShortcut(link);
            shortcut.TargetPath = Environment.ProcessPath ?? "";
            shortcut.WorkingDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
            shortcut.Description = "See.Net 空格预览";
            shortcut.Save();
        }
        catch
        {
            // 自启配置失败不阻塞应用（由调用方通过 IsEnabled 复核并提示）
        }
    }
}