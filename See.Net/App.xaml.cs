using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using See.Net.Core;
using See.Net.Services;
using See.Net.ViewModels;

namespace See.Net;

public partial class App : Application
{
    private ServiceProvider? _services;
    private TrayIconService? _tray;
    private ShellPreviewService? _shellPreview;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureCreated();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        var services = new ServiceCollection();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<PreviewViewModel>();
        services.AddSingleton<MainViewModel>();
        _services = services.BuildServiceProvider();

        var vm = _services.GetRequiredService<MainViewModel>();
        var settings = _services.GetRequiredService<SettingsService>();
        var backup = _services.GetRequiredService<BackupService>();

        var window = new MainWindow();
        window.Initialize(vm, settings);
        MainWindow = window;
        _mainWindow = window;

        // 系统托盘：后台常驻与退出入口
        _tray = new TrayIconService(ShowMainWindow, ExitApplication);
        window.ConfigureTray(_tray);

        // 资源管理器空格预览：全局键盘钩子
        _shellPreview = new ShellPreviewService(settings, backup, Dispatcher);
        _shellPreview.Start();

        // 随 Windows 启动（启动文件夹快捷方式，MSIX 下注册表 Run 会被虚拟化）
        AutoStartService.Apply(settings.Current.AutoStartEnabled);

        window.Show();
        _ = vm.InitializeAsync();

        // 命令行参数：以 See.Net 打开指定文件
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            window.Show();
            window.Activate();
            var fi = new FileInfo(e.Args[0]);
            var entry = new FileEntry
            {
                Name = fi.Name,
                FullPath = fi.FullName,
                Length = fi.Length,
                LastWriteTime = fi.LastWriteTime,
                Kind = FileTypeDetector.Detect(fi.FullName),
            };
            _ = vm.OpenPreviewFileAsync(entry);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _shellPreview?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        _services?.Dispose();
        base.OnExit(e);
    }

    private void ShowMainWindow()
    {
        if (Dispatcher.CheckAccess())
        {
            DoShowMainWindow();
        }
        else
        {
            Dispatcher.Invoke(DoShowMainWindow);
        }
    }

    private void DoShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        if (Dispatcher.CheckAccess())
        {
            DoExit();
        }
        else
        {
            Dispatcher.Invoke(DoExit);
        }
    }

    private void DoExit()
    {
        try { _shellPreview?.Dispose(); _shellPreview = null; } catch { }
        try { _tray?.Dispose(); _tray = null; } catch { }
        _mainWindow?.RequestExit();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show($"发生未处理的异常：{e.Exception.Message}", "See.Net", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) LogException(ex);
    }

    private static void LogException(Exception ex)
    {
        try
        {
            AppPaths.EnsureCreated();
            string file = Path.Combine(AppPaths.LogDirectory, $"error-{DateTime.Now:yyyyMMddHHmmss}.log");
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 日志写入失败时忽略
        }
    }
}
