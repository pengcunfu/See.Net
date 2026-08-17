using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using See.Net.Core;
using See.Net.Services;

namespace See.Net.ViewModels;

/// <summary>资源管理器空格预览浮窗的视图模型（多选文件时支持 ↑/↓ 切换）。</summary>
public sealed partial class ShellPreviewViewModel : ObservableObject
{
    private readonly PreviewViewModel _preview;
    private readonly List<FileEntry> _files = [];
    private int _index;

    public ShellPreviewViewModel(SettingsService settings, BackupService backup)
    {
        _preview = new PreviewViewModel(settings, backup);
    }

    public PreviewViewModel Preview => _preview;

    [ObservableProperty]
    private string _positionText = "";

    public void LoadFiles(IReadOnlyList<FileEntry> files)
    {
        _files.Clear();
        _files.AddRange(files.Where(f => !f.IsDirectory));
        _index = 0;
        PositionText = _files.Count <= 1 ? "" : $"1 / {_files.Count}";
        if (_files.Count > 0)
        {
            _ = ShowCurrentAsync();
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (_files.Count == 0) return;
        _index = _index <= 0 ? _files.Count - 1 : _index - 1;
        _ = ShowCurrentAsync();
    }

    [RelayCommand]
    private void Next()
    {
        if (_files.Count == 0) return;
        _index = (_index + 1) % _files.Count;
        _ = ShowCurrentAsync();
    }

    [RelayCommand]
    private void OpenInSeeNet()
    {
        if (_files.Count == 0) return;
        try
        {
            string exe = Environment.ProcessPath ?? "See.Net.exe";
            Process.Start(new ProcessStartInfo(exe)
            {
                Arguments = $"\"{_files[_index].FullPath}\"",
                UseShellExecute = true,
            });
        }
        catch { /* 打开失败时忽略 */ }
    }

    private async Task ShowCurrentAsync()
    {
        if (_files.Count == 0) return;
        PositionText = _files.Count <= 1 ? "" : $"{_index + 1} / {_files.Count}";
        await _preview.LoadAsync(_files[_index]);
    }

    public void DisposeContent()
    {
        _preview.CloseDocument();
        _preview.IsOpen = false;
    }
}
