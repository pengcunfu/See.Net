using System.Windows;
using Velopack;

namespace See;

/// <summary>
/// 自定义入口：先处理 Velopack 钩子（首次安装 / 已更新 / 启动时应用待安装更新），
/// 再启动 WPF 应用。vpk 打包校验要求 VelopackApp.Build().Run() 出现在 Program.Main。
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetArgs(args)
            .OnFirstRun(_ => StartupHooks.FirstRun = true)
            .OnRestarted(v => StartupHooks.UpdatedTo = v.ToNormalizedString())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

/// <summary>从 Program.Main 向 App.OnStartup 传递 Velopack 钩子结果（气泡提示用）。</summary>
internal static class StartupHooks
{
    public static bool FirstRun { get; set; }
    public static string? UpdatedTo { get; set; }
}
