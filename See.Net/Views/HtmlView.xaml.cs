using System.Windows;
using System.Windows.Controls;
using See.Net.ViewModels;

namespace See.Net.Views;

/// <summary>
/// 网页双视图：渲染（HtmlWebHost，目录映射 + 脚本启用）与只读源码（TextView 懒加载）切换。
/// </summary>
public partial class HtmlView : UserControl
{
    private WebContentViewModel? _vm;
    private HtmlWebHost? _webHost;
    private bool _webLoadedFor;
    private bool _syncing;

    public HtmlView()
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
        _vm = e.NewValue as WebContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        RefreshAll();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(WebContentViewModel.UseRendered)
                || e.PropertyName == nameof(WebContentViewModel.RenderError))
                RefreshAll();
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
        RenderToggle.IsEnabled = _vm.CanRender;
        _syncing = false;

        bool wantWeb = _vm.UseRendered && _vm.CanRender;
        WebSlot.Visibility = wantWeb ? Visibility.Visible : Visibility.Collapsed;
        SourceView.Visibility = wantWeb ? Visibility.Collapsed : Visibility.Visible;

        if (wantWeb)
        {
            if (!_webLoadedFor && HtmlWebHost.IsPathRenderable(_vm.FilePath))
            {
                _webHost?.Dispose();
                _webHost = new HtmlWebHost(_vm.FilePath) { DataContext = _vm };
                WebSlot.Content = _webHost;
                _webLoadedFor = true;
            }
        }
        else
        {
            // 源码懒加载：首次进入才读取文件
            if (!ReferenceEquals(SourceView.DataContext, _vm.Source))
                SourceView.DataContext = _vm.GetSource();
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
