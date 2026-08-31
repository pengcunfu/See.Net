using System.Reflection;

namespace See;

/// <summary>
/// 应用版本统一读取：优先取 AssemblyInformationalVersion（与 csproj &lt;Version&gt; 一致，
/// 含预发布后缀如 1.0.1-beta.1），并剥离自动追加的 +commit 段。
/// Assembly.GetName().Version 固定 4 段（如 1.0.0.0），不适合展示。
/// </summary>
public static class AppVersion
{
    public static string Display
    {
        get
        {
            var asm = typeof(AppVersion).Assembly;
            string? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plus = info.IndexOf('+');
                if (plus >= 0) info = info[..plus];
                return info;
            }

            var v = asm.GetName().Version;
            return v is null ? "未知" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
