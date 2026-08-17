using System.Windows;
using System.Windows.Controls;
using See.ViewModels;

namespace See.Views;

/// <summary>音频预览视图：AudioWebHost 播放页 + 错误条。</summary>
public partial class AudioView : UserControl
{
    private AudioContentViewModel? _vm;
    private AudioWebHost? _webHost;

    public AudioView()
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
        _vm = e.NewValue as AudioContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Refresh();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(AudioContentViewModel.Error)) Refresh();
        });
    }

    private void Refresh()
    {
        if (_vm is null) return;

        ErrorBar.Visibility = string.IsNullOrEmpty(_vm.Error) ? Visibility.Collapsed : Visibility.Visible;

        if (_webHost is null && WebSlot.Content is null)
        {
            _webHost = new AudioWebHost(_vm.FilePath, _vm.Name, _vm.Length) { DataContext = _vm };
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
