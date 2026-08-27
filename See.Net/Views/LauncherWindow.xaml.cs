using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using See.Services;

namespace See.Views;

/// <summary>
/// 全局启动器窗口：搜索应用、文件，执行命令，拖拽固定应用。
/// </summary>
public partial class LauncherWindow : Window
{
    private readonly LauncherService _launcher = new();
    private readonly DispatcherTimer _debounce;
    private List<LauncherResult> _results = [];
    private string _lastQuery = "";

    public LauncherWindow()
    {
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick += OnDebounceTick;
    }

    /// <summary>显示启动器并聚焦搜索框。</summary>
    public void ShowLauncher()
    {
        if (!IsVisible)
        {
            SearchBox.Text = "";
            Show();
        }
        ShowPopularApps();
        Activate();
        SearchBox.Focus();
    }

    private void ShowPopularApps()
    {
        _results = _launcher.GetPopularApps();
        _lastQuery = "";
        ResultsList.ItemsSource = _results.Select(r => new LauncherItemViewModel(r)).ToList();
        EmptyHint.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CategoryLabel.Text = _results.Count > 0 ? "快捷应用" : "";
        if (_results.Count > 0) ResultsList.SelectedIndex = 0;
    }

    #region 搜索

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        string query = SearchBox.Text.Trim();
        if (query == _lastQuery) return;
        _lastQuery = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            ShowPopularApps();
            return;
        }

        _results = _launcher.Search(query);
        ResultsList.ItemsSource = _results.Select(r => new LauncherItemViewModel(r)).ToList();
        EmptyHint.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_results.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            int appCount = _results.Count(r => r.Category == LauncherCategory.Application);
            int fileCount = _results.Count(r => r.Category == LauncherCategory.File);
            int cmdCount = _results.Count(r => r.Category == LauncherCategory.Command);
            var parts = new List<string>();
            if (appCount > 0) parts.Add($"{appCount} 个应用");
            if (fileCount > 0) parts.Add($"{fileCount} 个文件");
            if (cmdCount > 0) parts.Add($"{cmdCount} 个命令");
            CategoryLabel.Text = string.Join(" · ", parts);
        }
        else
        {
            CategoryLabel.Text = "";
        }
    }

    #endregion

    #region 列表交互

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedIndex >= 0 && ResultsList.SelectedIndex < _results.Count)
        {
            EmptyHint.Visibility = Visibility.Collapsed;
        }
        // 右键菜单：已固定的应用显示"取消固定"
        int idx = ResultsList.SelectedIndex;
        if (idx >= 0 && idx < _results.Count && string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            var item = _results[idx];
            RemovePinnedMenuItem.Visibility = _launcher.IsPinned(item.Path)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            RemovePinnedMenuItem.Visibility = Visibility.Collapsed;
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelected();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CloseLauncher();
                break;

            case Key.Enter:
                e.Handled = true;
                ExecuteSelected();
                break;

            case Key.Up:
                e.Handled = true;
                if (ResultsList.SelectedIndex > 0)
                    ResultsList.SelectedIndex--;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;

            case Key.Down:
                e.Handled = true;
                if (ResultsList.SelectedIndex < _results.Count - 1)
                    ResultsList.SelectedIndex++;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;

            case Key.Tab:
                e.Handled = true;
                if (ResultsList.SelectedIndex < _results.Count - 1)
                    ResultsList.SelectedIndex++;
                else
                    ResultsList.SelectedIndex = 0;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsActive) CloseLauncher();
        }, DispatcherPriority.Input);
    }

    private void ExecuteSelected()
    {
        int index = ResultsList.SelectedIndex;
        if (index < 0 || index >= _results.Count) return;
        var result = _results[index];
        CloseLauncher();
        _launcher.Execute(result);
    }

    private void CloseLauncher()
    {
        _debounce.Stop();
        Hide();
    }

    #endregion

    #region 取消固定

    private void OnRemovePinned(object sender, RoutedEventArgs e)
    {
        int idx = ResultsList.SelectedIndex;
        if (idx < 0 || idx >= _results.Count) return;
        _launcher.RemovePinnedApp(_results[idx].Path);
        ShowPopularApps();
    }

    #endregion

    #region 拖拽

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            // 显示拖拽叠加层
            DropOverlay.Opacity = 0.9;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // 隐藏拖拽叠加层
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        DropOverlay.BeginAnimation(OpacityProperty, anim);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        // 隐藏拖拽叠加层
        DropOverlay.Opacity = 0;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0) return;

        foreach (string file in files)
        {
            if (!File.Exists(file)) continue;
            // 只固定可执行文件和快捷方式
            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".exe" or ".lnk" or ".msc" or ".bat" or ".cmd")
            {
                _launcher.AddPinnedApp(file);
            }
        }

        // 刷新列表
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            ShowPopularApps();
        }

        e.Handled = true;
    }

    #endregion
}

/// <summary>启动器列表项的显示模型。</summary>
public sealed class LauncherItemViewModel(LauncherResult result)
{
    public string Name => result.Name;
    public string Description => result.Description ?? result.Path;

    /// <summary>真实图标（从 exe/lnk 提取）。</summary>
    public BitmapSource? Icon => result.Icon;

    /// <summary>是否有真实图标。</summary>
    public Visibility HasIcon => result.Icon is not null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>字体图标（无真实图标时的回退）。</summary>
    public string FontIcon => result.Category switch
    {
        LauncherCategory.Application => " ",
        LauncherCategory.File => " ",
        LauncherCategory.Command => " ",
        _ => " ",
    };

    public string CategoryText => result.Category switch
    {
        LauncherCategory.Application => "应用",
        LauncherCategory.File => "文件",
        LauncherCategory.Command => "命令",
        _ => "",
    };
}
