using System.Windows;
using System.Windows.Controls;
using See.ViewModels;

namespace See.Views;

/// <summary>PDF 预览视图：PdfWebHost + 错误条。</summary>
public partial class PdfView : UserControl
{
    private PdfContentViewModel? _vm;
    private PdfWebHost? _webHost;

    public PdfView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as PdfContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Refresh();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(PdfContentViewModel.Error)) Refresh();
        });
    }

    private void Refresh()
    {
        if (_vm is null) return;

        ErrorBar.Visibility = string.IsNullOrEmpty(_vm.Error) ? Visibility.Collapsed : Visibility.Visible;

        if (_webHost is null && WebSlot.Content is null)
        {
            _webHost = new PdfWebHost(_vm.FilePath) { DataContext = _vm };
            WebSlot.Content = _webHost;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _webHost?.Dispose();
        _webHost = null;
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}
