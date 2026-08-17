using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using See.ViewModels;

namespace See.Views;

public partial class ImageView : UserControl
{
    private const double ScrollStep = 48;

    private ImageContentViewModel? _vm;

    public ImageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            ApplyLayout();
            Focus();
        };
        SizeChanged += (_, _) =>
        {
            if (_vm?.IsFit == true) ApplyLayout();
        };
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
            or nameof(ImageContentViewModel.Zoom))
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
            ScaleTransform.ScaleX = 1;
            ScaleTransform.ScaleY = 1;
            ContentHost.Width = double.NaN;
            ContentHost.Height = double.NaN;
            Pic.Width = Scroller.ViewportWidth > 0 ? Scroller.ViewportWidth : double.NaN;
            Pic.Height = Scroller.ViewportHeight > 0 ? Scroller.ViewportHeight : double.NaN;
            ZoomLabel.Text = "适应窗口";
        }
        else
        {
            Pic.Stretch = System.Windows.Media.Stretch.None;
            Pic.Width = double.NaN;
            Pic.Height = double.NaN;
            ScaleTransform.ScaleX = _vm.Zoom;
            ScaleTransform.ScaleY = _vm.Zoom;
            ZoomLabel.Text = $"{_vm.Zoom * 100:0}%";
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_vm is null) return;

        // Ctrl + 滚轮：缩放
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double factor = e.Delta > 0 ? 1.25 : 1.0 / 1.25;
            ZoomAt(factor, e.GetPosition(Scroller));
            e.Handled = true;
            return;
        }

        // Shift + 滚轮：横向滚动
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta);
            e.Handled = true;
            return;
        }

        // 普通滚轮：纵向滚动（有可滚空间时消费；适应窗口时无滚动需求）
        if (Scroller.ScrollableHeight > 0)
        {
            Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset - e.Delta);
            e.Handled = true;
        }
        // 无可竖滚但有横滚时，普通滚轮也帮横向滚一点（更顺手）
        else if (Scroller.ScrollableWidth > 0)
        {
            Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;

        // Ctrl + / Ctrl - / Ctrl = ：缩放
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key is Key.OemPlus or Key.Add)
            {
                ZoomAt(1.25, CenterOfScroller());
                e.Handled = true;
                return;
            }
            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                ZoomAt(1.0 / 1.25, CenterOfScroller());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                _vm.ActualSize();
                e.Handled = true;
                return;
            }
        }

        // 方向键微调滚动
        double dx = 0, dy = 0;
        switch (e.Key)
        {
            case Key.Left: dx = -ScrollStep; break;
            case Key.Right: dx = ScrollStep; break;
            case Key.Up: dy = -ScrollStep; break;
            case Key.Down: dy = ScrollStep; break;
            default: return;
        }

        if (Scroller.ScrollableWidth > 0 || Scroller.ScrollableHeight > 0)
        {
            Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + dx);
            Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + dy);
            e.Handled = true;
        }
    }

    private void ZoomAt(double factor, Point mouseInScroller)
    {
        if (_vm is null) return;

        double oldZoom = _vm.IsFit ? EstimateFitZoom() : _vm.Zoom;
        double newZoom = Math.Clamp(Math.Round(oldZoom * factor, 3), 0.05, 16);

        Point contentBefore = new(
            mouseInScroller.X + Scroller.HorizontalOffset,
            mouseInScroller.Y + Scroller.VerticalOffset);

        double ratio = oldZoom <= 0 ? 1 : newZoom / oldZoom;
        _vm.SetZoom(newZoom);

        Dispatcher.BeginInvoke(() =>
        {
            Scroller.ScrollToHorizontalOffset(contentBefore.X * ratio - mouseInScroller.X);
            Scroller.ScrollToVerticalOffset(contentBefore.Y * ratio - mouseInScroller.Y);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private double EstimateFitZoom()
    {
        if (_vm is null || _vm.NaturalWidth <= 0 || _vm.NaturalHeight <= 0) return 1;

        double vw = Scroller.ViewportWidth;
        double vh = Scroller.ViewportHeight;
        if (vw <= 0 || vh <= 0) return 1;

        return Math.Min(vw / _vm.NaturalWidth, vh / _vm.NaturalHeight);
    }

    private void OnFit(object sender, RoutedEventArgs e) => _vm?.FitToWindow();
    private void OnActualSize(object sender, RoutedEventArgs e) => _vm?.ActualSize();
    private void OnZoomIn(object sender, RoutedEventArgs e) => ZoomAt(1.25, CenterOfScroller());
    private void OnZoomOut(object sender, RoutedEventArgs e) => ZoomAt(1.0 / 1.25, CenterOfScroller());

    private Point CenterOfScroller() =>
        new(Scroller.ViewportWidth / 2, Scroller.ViewportHeight / 2);
}
