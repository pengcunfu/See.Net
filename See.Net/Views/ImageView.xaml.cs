using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using See.Net.ViewModels;

namespace See.Net.Views;

public partial class ImageView : UserControl
{
    private ImageContentViewModel? _vm;

    public ImageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as ImageContentViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        ApplyLayout();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageContentViewModel.IsFit)
            or nameof(ImageContentViewModel.DisplayWidth)
            or nameof(ImageContentViewModel.DisplayHeight))
        {
            ApplyLayout();
        }
    }

    private void ApplyLayout()
    {
        if (_vm is null) return;
        if (_vm.IsFit)
        {
            Pic.Stretch = System.Windows.Media.Stretch.Uniform;
            Pic.Width = double.NaN;
            Pic.Height = double.NaN;
        }
        else
        {
            Pic.Stretch = System.Windows.Media.Stretch.Fill;
            Pic.Width = _vm.DisplayWidth;
            Pic.Height = _vm.DisplayHeight;
        }
    }

    private void OnFit(object sender, RoutedEventArgs e) => _vm?.FitToWindow();
    private void OnActualSize(object sender, RoutedEventArgs e) => _vm?.ActualSize();
    private void OnZoomIn(object sender, RoutedEventArgs e) => _vm?.ZoomIn();
    private void OnZoomOut(object sender, RoutedEventArgs e) => _vm?.ZoomOut();
}
