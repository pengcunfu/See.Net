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
    private SingleInstanceService? _singleton;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        // 系统托盘：后台常驻、设置与退出入口
        _tray = new TrayIconService(ShowMainWindow, OpenSettings, ExitApplication);
        window.ConfigureTray(_tray);

        // 资源管理器空格预览：全局键盘钩子
        _shellPreview = new ShellPreviewService(settings, backup, Dispatcher);
        _shellPreview.Start();

        // 随 Windows 启动（启动文件夹快捷方式，MSIX 下注册表 Run 会被虚拟化）。
        // 按实际状态回写设置：自愈失效快捷方式（应用被移动 / MSIX 重装后 LNK 指向旧路径）。
        AutoStartService.Apply(settings.Current.AutoStartEnabled);
        bool actual = AutoStartService.IsEnabled();
        if (actual != settings.Current.AutoStartEnabled)
        {
            settings.Current.AutoStartEnabled = actual;
            settings.Save();
        }

        window.Show();
        _ = vm.InitializeAsync();

        // 命令行参数：以 See.Net 打开指定文件（与单例转发共用同一逻辑）
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            window.Show();
            window.Activate();
            _ = OpenFilePathAsync(e.Args[0]);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleton?.Dispose(); } catch { }
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

    /// <summary>单例管道转发：展示主窗口，非空 payload 则打开对应文件。</summary>
    private void OnFileOpenedFromPipe(string payload)
    {
        DoShowMainWindow();
        if (string.IsNullOrWhiteSpace(payload)) return;
        _ = OpenFilePathAsync(payload);
    }

    /// <summary>导航到文件所在目录并打开预览（addHistory:false，避免污染后退栈）。</summary>
    private async Task OpenFilePathAsync(string path)
    {
        if (!File.Exists(path)) return;
        var fi = new FileInfo(path);
        var entry = new FileEntry
        {
            Name = fi.Name,
            FullPath = fi.FullName,
            Length = fi.Length,
            LastWriteTime = fi.LastWriteTime,
            Kind = FileTypeDetector.Detect(fi.FullName),
        };
        var vm = _services!.GetRequiredService<MainViewModel>();
        string? dir = FileSystemService.GetParent(entry.FullPath);
        if (!string.IsNullOrEmpty(dir)) await vm.NavigateToAsync(dir, addHistory: false);
        await vm.OpenPreviewFileAsync(entry);
    }

    /// <summary>打开设置窗口（单实例复用，关闭后重建）。</summary>
    private void OpenSettings()
    {
        if (_services is null) return;
        if (_settingsWindow is null)
        {
            var settings = _services.GetRequiredService<SettingsService>();
            _settingsWindow = new SettingsWindow(settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
        _settingsWindow.WindowState = WindowState.Normal;
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
