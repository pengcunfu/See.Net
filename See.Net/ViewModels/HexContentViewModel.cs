using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using See.Controls;
using See.Net.Core;
using See.Services;

namespace See.ViewModels;

/// <summary>十六进制查看/编辑内容。</summary>
public sealed partial class HexContentViewModel : ObservableObject, IDisposable
{
    private readonly BackupService _backup;
    private HexEditor? _editor;

    public HexContentViewModel(HexDocument document, string filePath, BackupService backup,
        double fontSize = 14, int bytesPerRow = 16)
    {
        Document = document;
        FilePath = filePath;
        _backup = backup;
        FontSize = fontSize;
        BytesPerRow = bytesPerRow;
        UpdateStatus();
    }

    public HexDocument Document { get; }
    public string FilePath { get; }
    public double FontSize { get; }
    public int BytesPerRow { get; }

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _insertMode;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _gotoText = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private int _copyFormatIndex;

    public string ModeText => InsertMode ? "插入模式" : "覆盖模式";

    public void AttachEditor(HexEditor editor)
    {
        _editor = editor;
        editor.Document = Document;
        editor.FontSize = FontSize;
        editor.BytesPerRow = BytesPerRow;
        editor.InsertMode = InsertMode;
        editor.EditPerformed += (_, _) =>
        {
            IsDirty = true;
            UpdateStatus();
        };
        editor.CaretChanged += (_, _) => UpdateStatus();
        editor.SelectionChanged += (_, _) => UpdateStatus();
        editor.ModeChanged += (_, _) =>
        {
            InsertMode = editor.InsertMode;
            UpdateStatus();
        };
        UpdateStatus();
    }

    partial void OnInsertModeChanged(bool value)
    {
        if (_editor is not null) _editor.InsertMode = value;
        OnPropertyChanged(nameof(ModeText));
    }

    private void UpdateStatus()
    {
        if (_editor is null)
        {
            Status = $"大小 {FileEntry.FormatSize(Document.Length)} · 偏移 0x0";
            return;
        }
        long caret = _editor.CaretOffset;
        string sel = _editor.HasSelection ? $" · 选中 {_editor.SelectionLength} 字节" : "";
        Status = $"大小 {FileEntry.FormatSize(Document.Length)} · 偏移 0x{caret:X}{sel}";
    }

    [RelayCommand]
    private void FindNext()
    {
        var editor = _editor;
        if (editor is null || !HexFormat.TryParseHex(SearchText, out var pattern)) return;
        long start = Math.Min(editor.CaretOffset + 1, Document.Length);
        long pos = Document.Find(pattern, start);
        if (pos < 0) pos = Document.Find(pattern, 0);
        if (pos < 0)
        {
            Status = "未找到匹配内容";
            editor.ClearSearch();
            return;
        }
        editor.SelectRange(pos, pattern.Length);
        editor.SetSearchMatches([(pos, pattern.Length)], 0);
        Status = $"找到于 0x{pos:X}（共 {Document.Length:X} 字节）";
    }

    [RelayCommand]
    private void FindPrevious()
    {
        var editor = _editor;
        if (editor is null || !HexFormat.TryParseHex(SearchText, out var pattern)) return;
        var matches = new List<(long Start, long Length)>();
        long pos = Document.Find(pattern, 0);
        while (pos >= 0)
        {
            if (pos >= editor.CaretOffset) break;
            matches.Add((pos, pattern.Length));
            if (pos >= editor.CaretOffset - 1) break;
            pos = Document.Find(pattern, pos + 1);
        }
        if (matches.Count == 0)
        {
            Status = "未找到匹配内容";
            editor.ClearSearch();
            return;
        }
        var last = matches[^1];
        editor.SelectRange(last.Start, last.Length);
        editor.SetSearchMatches(matches, matches.Count - 1);
        Status = $"找到于 0x{last.Start:X}（共 {matches.Count} 处）";
    }

    [RelayCommand]
    private void Goto()
    {
        var editor = _editor;
        if (editor is null || !HexFormat.TryParseOffset(GotoText, out long offset)) return;
        offset = Math.Clamp(offset, 0, Document.Length);
        editor.MoveCaretTo(offset);
        Status = $"已跳转到 0x{offset:X}";
    }

    [RelayCommand]
    private void Copy()
    {
        var editor = _editor;
        if (editor is null) return;
        switch (CopyFormatIndex)
        {
            case 0: editor.CopyHex(); break;
            case 1: editor.CopyAscii(); break;
            case 2: editor.CopyCArray(); break;
        }
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _backup.Backup(FilePath);
            Document.Save(FilePath);
            IsDirty = false;
            Status = "已保存";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "See.Net", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Dispose() => Document.Dispose();
}
