using System.Text;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using See.Net.Core;
using See.Net.Services;

namespace See.Net.ViewModels;

/// <summary>文本预览/编辑内容。</summary>
public partial class TextContentViewModel : ObservableObject
{
    private readonly BackupService _backup;
    private bool _suppressDirty;

    public TextContentViewModel(string filePath, string initialText, Encoding detectedEncoding, BackupService backup)
    {
        FilePath = filePath;
        _backup = backup;
        Text = initialText;
        _suppressDirty = true;
        Encodings = EncodingService.Options
            .Select(o => new EncodingOption(o.Name, o.Encoding, o.WriteBom))
            .ToArray();
        SelectedEncoding = Encodings.FirstOrDefault(e => e.Encoding.CodePage == detectedEncoding.CodePage) ?? Encodings[0];
        _suppressDirty = false;
        Highlighting = Path.GetExtension(filePath);
    }

    public string FilePath { get; }
    public string Highlighting { get; }
    public IReadOnlyList<EncodingOption> Encodings { get; }

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isReadOnly = true;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private EncodingOption? _selectedEncoding;

    [ObservableProperty]
    private bool _wordWrap = true;

    partial void OnIsEditModeChanged(bool value) => IsReadOnly = !value;

    partial void OnSelectedEncodingChanged(EncodingOption? value)
    {
        if (value is not null && !_suppressDirty) IsDirty = true;
    }

    public void BeginLoad() => _suppressDirty = true;
    public void EndLoad() => _suppressDirty = false;

    public void MarkDirty()
    {
        if (_suppressDirty) return;
        IsDirty = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var encoding = SelectedEncoding;
        if (encoding is null) return;
        try
        {
            byte[] bytes = EncodingService.EncodeWithBom(Text, encoding.Encoding, encoding.WriteBom);
            _backup.Backup(FilePath);
            await AtomicFile.WriteAsync(FilePath, bytes);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "See.Net", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
