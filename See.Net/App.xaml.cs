using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using See.Services;

namespace See;

public partial class App : Application
{
    private ServiceProvider? _services;
    private TrayIconService? _tray;
    private ShellPreviewService? _shellPreview;
    private SingleInstanceService? _singleton;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 无主窗口：托盘常驻 + 预览浮窗；退出需显式 Shutdown
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 单例：后续实例转发参数后立即退出，不创建窗口/托盘/全局键盘钩子
        _singleton = SingleInstanceService.Acquire(e.Args, Dispatcher);
        if (_singleton is null) { Shutdown(); return; }
        _singleton.FileOpened += OnFileOpenedFromPipe;

        AppPaths.EnsureCreated();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        var services = new ServiceCollection();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<BackupService>();
        _services = services.BuildServiceProvider();

        var settings = _services.GetRequiredService<SettingsService>();
        var backup = _services.GetRequiredService<BackupService>();

        // 注册自定义语法高亮定义（JSON、TOML、YAML、Log）
        SyntaxHighlightingService.Initialize();

        // 资源管理器空格预览：全局键盘钩子（需在托盘之前创建，托盘左键调用其启动器）
        _shellPreview = new ShellPreviewService(settings, backup, Dispatcher);
        _shellPreview.Start();

        // 系统托盘：打开文件预览、启动器、设置、关于与退出
        _tray = new TrayIconService(OpenFileForPreview, () => _shellPreview.ShowLauncher(), OpenSettings, OpenAbout, ExitApplication);

        // 随 Windows 启动（启动文件夹快捷方式，MSIX 下注册表 Run 会被虚拟化）。
        AutoStartService.Apply(settings.Current.AutoStartEnabled);
        bool actual = AutoStartService.IsEnabled();
        if (actual != settings.Current.AutoStartEnabled)
        {
            settings.Current.AutoStartEnabled = actual;
            settings.Save();
        }

        // 命令行参数：直接弹出预览浮窗
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            _shellPreview.ShowPreviewForPaths([e.Args[0]]);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleton?.Dispose(); } catch { }
        try { _shellPreview?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        _services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>托盘「打开文件…」：选择文件后弹出预览浮窗。</summary>
    private void OpenFileForPreview()
    {
        if (Dispatcher.CheckAccess())
            DoOpenFileForPreview();
        else
            Dispatcher.Invoke(DoOpenFileForPreview);
    }

    private void DoOpenFileForPreview()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择要预览的文件",
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;
        _shellPreview?.ShowPreviewForPaths(dlg.FileNames);
    }

    /// <summary>单例管道转发：非空 payload 则打开对应文件预览。</summary>
    private void OnFileOpenedFromPipe(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        if (!File.Exists(payload)) return;
        _shellPreview?.ShowPreviewForPaths([payload]);
    }

    /// <summary>打开设置窗口（单实例复用，关闭后重建）。</summary>
    private void OpenSettings()
    {
        if (_services is null) return;
        if (Dispatcher.CheckAccess())
            DoOpenSettings();
        else
            Dispatcher.Invoke(DoOpenSettings);
    }

    private void DoOpenSettings()
    {
        if (_services is null) return;
        if (_settingsWindow is not null)
        {
            try
            {
                _settingsWindow.Show();
                _settingsWindow.Activate();
                _settingsWindow.WindowState = WindowState.Normal;
                return;
            }
            catch (ArgumentException)
            {
                // 窗口 VisualTree 已损坏（如跨线程关闭后引用未清空），重建窗口
                try { _settingsWindow.Close(); } catch { }
                _settingsWindow = null;
            }
        }
        var settings = _services.GetRequiredService<SettingsService>();
        _settingsWindow = new SettingsWindow(settings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>打开关于窗口（单实例复用，关闭后重建）。</summary>
    private void OpenAbout()
    {
        if (Dispatcher.CheckAccess())
            DoOpenAbout();
        else
            Dispatcher.Invoke(DoOpenAbout);
    }

    private void DoOpenAbout()
    {
        if (_aboutWindow is not null)
        {
            try
            {
                _aboutWindow.Show();
                _aboutWindow.Activate();
                _aboutWindow.WindowState = WindowState.Normal;
                return;
            }
            catch (ArgumentException)
            {
                try { _aboutWindow.Close(); } catch { }
                _aboutWindow = null;
            }
        }
        _aboutWindow = new AboutWindow();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    private void ExitApplication()
    {
        if (Dispatcher.CheckAccess())
            DoExit();
        else
            Dispatcher.Invoke(DoExit);
    }

    private void DoExit()
    {
        try { _shellPreview?.Dispose(); _shellPreview = null; } catch { }
        try { _tray?.Dispose(); _tray = null; } catch { }
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "UI线程");

        var errorMessage = $"发生未处理的异常:{e.Exception.Message}";

        if (e.Exception.Message.Contains("Office") ||
            e.Exception.Message.Contains("文档") ||
            e.Exception.Message.Contains("Excel") ||
            e.Exception.Message.Contains("Word") ||
            e.Exception.Message.Contains("PowerPoint"))
        {
            errorMessage += "建议:- 检查文件是否损坏或被加密\n- 尝试使用网页预览模式\n- 确认文件没有被其他程序占用";
        }

        MessageBox.Show(errorMessage, "See.Net 错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex, "后台线程");

            if (e.IsTerminating)
            {
                var criticalMessage = $"程序遇到严重错误，即将退出:{ex.Message}详细信息已记录到: {AppPaths.LogDirectory}";
                try
                {
                    MessageBox.Show(criticalMessage, "See.Net 严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            }
        }
    }

    private static void LogException(Exception ex, string context = "General")
    {
        try
        {
            AppPaths.EnsureCreated();
            string file = Path.Combine(AppPaths.LogDirectory, $"error-{DateTime.Now:yyyyMMddHHmmss}.log");

            var logBuilder = new System.Text.StringBuilder();
            logBuilder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}异常");
            logBuilder.AppendLine($"消息: {ex.Message}");
            logBuilder.AppendLine($"类型: {ex.GetType().FullName}");
            logBuilder.AppendLine($"源: {ex.Source}");

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                logBuilder.AppendLine("堆栈跟踪:");
                logBuilder.AppendLine(ex.StackTrace);
            }

            if (ex.InnerException != null)
            {
                logBuilder.AppendLine("内部异常:");
                logBuilder.AppendLine($"消息: {ex.InnerException.Message}");
                if (!string.IsNullOrEmpty(ex.InnerException.StackTrace))
                {
                    logBuilder.AppendLine("内部堆栈跟踪:");
                    logBuilder.AppendLine(ex.InnerException.StackTrace);
                }
            }

            logBuilder.AppendLine(new string('-', 80));
            logBuilder.AppendLine();

            File.AppendAllText(file, logBuilder.ToString());
        }
        catch (Exception logEx)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write exception log: {logEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Original exception: {ex.Message}");
        }
    }
}
