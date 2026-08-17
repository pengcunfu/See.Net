using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using See.Controls;
using See.Net.Core;
using See.Services;
using See.ViewModels;

namespace See;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private SettingsService? _settings;
    private TrayIconService? _tray;
    private bool _exitRequested;
    private bool _hideRequested;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainViewModel vm, SettingsService settings)
    {
        _vm = vm;
        _settings = settings;
        DataContext = vm;

        var s = settings.Current;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        if (s.WindowMaximized) WindowState = WindowState.Maximized;

        vm.Preview.PropertyChanged += OnPreviewPropertyChanged;
    }

    /// <summary>接入系统托盘（关闭窗口时隐藏到托盘，后台继续提供空格预览）。</summary>
    public void ConfigureTray(TrayIconService tray) => _tray = tray;

    /// <summary>放行窗口真正关闭（托盘菜单「退出」）。</summary>
    public void RequestExit() => _exitRequested = true;

    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_hideRequested || _exitRequested) return;
        if (e.PropertyName != nameof(PreviewViewModel.IsOpen)) return;
        if (_vm is null) return;
        PreviewOverlay.Visibility = _vm.Preview.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        if (_vm.Preview.IsOpen)
        {
            PreviewPaneHost.Focus();
        }
        else
        {
            FileList.Focus();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = _vm;
        if (vm is null) return;

        bool editing = Keyboard.FocusedElement is TextBoxBase
            or TextBox
            or ComboBox
            or TextEditor
            or HexEditor;

        switch (e.Key)
        {
            case Key.Space when !editing && vm.SelectedEntry is not null:
                e.Handled = true;
                vm.TogglePreviewCommand.Execute(null);
                break;
            case Key.Escape when vm.Preview.IsOpen:
                e.Handled = true;
                vm.ClosePreviewCommand.Execute(null);
                break;
            case Key.Down when vm.Preview.IsOpen && !editing:
                e.Handled = true;
                vm.NextFileCommand.Execute(null);
                break;
            case Key.Up when vm.Preview.IsOpen && !editing:
                e.Handled = true;
                vm.PrevFileCommand.Execute(null);
                break;
            case Key.Enter when !vm.Preview.IsOpen && vm.SelectedEntry is not null:
                e.Handled = true;
                if (vm.SelectedEntry.IsDirectory)
                {
                    _ = vm.NavigateToAsync(vm.SelectedEntry.FullPath);
                }
                else
                {
                    vm.OpenPreviewCommand.Execute(vm.SelectedEntry);
                }
                break;
        }
    }

    private void OnFileDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = _vm;
        if (vm?.SelectedEntry is null) return;
        if (vm.SelectedEntry.IsDirectory)
        {
            _ = vm.NavigateToAsync(vm.SelectedEntry.FullPath);
        }
        else
        {
            vm.OpenPreviewCommand.Execute(vm.SelectedEntry);
        }
    }

    private void OnPathKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm is not null)
        {
            e.Handled = true;
            _ = _vm.NavigateToAsync(PathBox.Text);
        }
    }

    private void OnGoClick(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _ = _vm.NavigateToAsync(PathBox.Text);
    }

    private void OnOverlayClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid grid && ReferenceEquals(grid, PreviewOverlay) && _vm is not null)
        {
            _vm.ClosePreviewCommand.Execute(null);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        var vm = _vm;
        if (vm is null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null || paths.Length == 0) return;

        string first = paths[0];
        if (Directory.Exists(first))
        {
            await vm.NavigateToAsync(first);
            return;
        }
        if (!File.Exists(first)) return;

        var entry = vm.Entries.FirstOrDefault(f => string.Equals(f.FullPath, first, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            var fi = new FileInfo(first);
            entry = new FileEntry
            {
                Name = fi.Name,
                FullPath = fi.FullName,
                Length = fi.Length,
                LastWriteTime = fi.LastWriteTime,
                Kind = FileTypeDetector.Detect(fi.FullName),
            };
        }
        await vm.OpenPreviewFileAsync(entry);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // 托盘「退出」：放行真正关闭
        if (_exitRequested) return;

        if (_vm is null || _settings is null)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (_hideRequested)
        {
            e.Cancel = true;
            return;
        }

        // 取消本次关闭，异步处理未保存内容后隐藏到托盘（后台空格预览继续生效）。
        // 不能在 Closing 事件执行期间直接调用 Hide/Close，排队到事件返回后执行。
        e.Cancel = true;
        _hideRequested = true;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                var close = _vm.ClosePreviewCommand;
                close.Execute(null);
                if (close.ExecutionTask is not null) await close.ExecutionTask;
                if (_vm.Preview.IsOpen)
                {
                    _hideRequested = false;
                    return; // 用户取消
                }

                var s = _settings.Current;
                s.LastDirectory = _vm.CurrentDirectory;
                s.WindowWidth = Width;
                s.WindowHeight = Height;
                s.WindowMaximized = WindowState == WindowState.Maximized;

                _hideRequested = false;
                Hide();
                _settings.Save();

                if (!s.TrayHintShown)
                {
                    s.TrayHintShown = true;
                    _settings.Save();
                    _tray?.ShowBalloon(
                        "See.Net 仍在后台运行",
                        "已最小化到托盘。在 Windows 资源管理器中选中文件后按空格即可快速预览。双击托盘图标可重新打开。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"关闭窗口时出错：{ex.Message}", "See.Net", MessageBoxButton.OK, MessageBoxImage.Error);
                _hideRequested = false;
            }
        }));
    }
}
