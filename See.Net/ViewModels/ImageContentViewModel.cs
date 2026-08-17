using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using See.Net.Core;

namespace See.Net.ViewModels;

/// <summary>图片预览内容。</summary>
public sealed partial class ImageContentViewModel : ObservableObject
{
    public ImageContentViewModel(BitmapSource image, string filePath, long length)
    {
        Image = image;
        FilePath = filePath;
        Info = $"{image.PixelWidth} × {image.PixelHeight} · {FileEntry.FormatSize(length)}";
        NaturalWidth = image.PixelWidth;
        NaturalHeight = image.PixelHeight;
    }

    public BitmapSource Image { get; }
    public string FilePath { get; }
    public string Info { get; }
    public int NaturalWidth { get; }
    public int NaturalHeight { get; }

    [ObservableProperty]
    private bool _isFit = true;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private double _displayWidth = 1;

    [ObservableProperty]
    private double _displayHeight = 1;

    partial void OnIsFitChanged(bool value)
    {
        if (value)
        {
            DisplayWidth = 1;
            DisplayHeight = 1;
        }
        else
        {
            DisplayWidth = NaturalWidth * Zoom;
            DisplayHeight = NaturalHeight * Zoom;
        }
    }

    partial void OnZoomChanged(double value)
    {
        if (!IsFit)
        {
            DisplayWidth = NaturalWidth * value;
            DisplayHeight = NaturalHeight * value;
        }
    }

    public void ZoomIn()
    {
        Zoom = Math.Min(16, Math.Round(Zoom * 1.25, 3));
        if (IsFit) IsFit = false;
    }

    public void ZoomOut()
    {
        Zoom = Math.Max(0.05, Math.Round(Zoom / 1.25, 3));
        if (IsFit) IsFit = false;
    }

    public void FitToWindow() => IsFit = true;

    public void ActualSize()
    {
        Zoom = 1.0;
        IsFit = false;
    }
}
