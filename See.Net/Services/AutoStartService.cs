using System.IO;

namespace See.Net.Services;

/// <summary>通过启动文件夹快捷方式实现随 Windows 启动（MSIX 下注册表 Run 会被虚拟化，故用 LNK）。</summary>
public static class AutoStartService
{
    private static string StartupLinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "See.Net.lnk");

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
            if (File.Exists(link)) return;

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
            // 自启配置失败不阻塞应用
        }
    }
}
