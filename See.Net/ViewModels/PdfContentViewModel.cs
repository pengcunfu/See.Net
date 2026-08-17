using CommunityToolkit.Mvvm.ComponentModel;

namespace See.ViewModels;

/// <summary>PDF 预览内容：由 WebView2 Chromium PDF 查看器承载。</summary>
public sealed partial class PdfContentViewModel : ObservableObject, IDisposable
{
    public PdfContentViewModel(string path) => FilePath = path;

    public string FilePath { get; }

    [ObservableProperty]
    private string? _error;

    public void Dispose()
    {
    }
}
