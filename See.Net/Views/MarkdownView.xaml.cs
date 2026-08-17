using System.Windows;
using System.Windows.Controls;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// Markdown 双视图：渲染（MarkdownWebHost）与源码（TextView 复用文本编辑栈）切换。
/// 可见性与选中态由 VM 的 UseRendered 驱动（代码后置绑定，仿 OfficeView）。
/// </summary>
public partial class MarkdownView : UserControl
{
    private MarkdownContentViewModel? _vm;
    private MarkdownWebHost? _webHost;
    private bool _webLoadedFor;
    private bool _syncing;

    public MarkdownView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e) => RefreshAll();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as MarkdownContentViewModel;
        if (_vm is null) return;

        // 源码区挂内部 TextContentViewModel（编辑 / 保存 / 编码全套复用）
        SourceView.DataContext = _vm.Source;
        _vm.PropertyChanged += OnVmPropertyChanged;
        RefreshAll();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MarkdownContentViewModel.UseRendered):
                case nameof(MarkdownContentViewModel.Html):
                case nameof(MarkdownContentViewModel.RenderError):
                    RefreshAll();
                    break;
            }
        });
    }

    private void OnRenderToggleChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || _vm is null) return;
        _vm.UseRendered = true;
    }

    private void OnSourceToggleChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || _vm is null) return;
        _vm.UseRendered = false;
    }

    private void RefreshAll()
    {
        if (_vm is null) return;

        _syncing = true;
        RenderToggle.IsChecked = _vm.UseRendered;
        SourceToggle.IsChecked = !_vm.UseRendered;
        _syncing = false;

        bool wantWeb = _vm.UseRendered && _vm.CanRender;
        WebSlot.Visibility = wantWeb ? Visibility.Visible : Visibility.Collapsed;
        SourceView.Visibility = wantWeb ? Visibility.Collapsed : Visibility.Visible;

        if (wantWeb)
        {
            _webHost ??= new MarkdownWebHost { DataContext = _vm };
            if (WebSlot.Content != _webHost) WebSlot.Content = _webHost;
            // 首次进入或源码更新后重新加载（Html 变更触发导航刷新）
            if (!_webLoadedFor && _vm.Html is not null)
            {
                _ = _webHost.LoadAsync(_vm.FilePath);
                _webLoadedFor = true;
            }
        }
        else
        {
            _webHost?.Dispose();
            _webHost = null;
            _webLoadedFor = false;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _webHost?.Dispose();
        _webHost = null;
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}
