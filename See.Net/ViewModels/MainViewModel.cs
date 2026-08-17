using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using See.Net.Core;
using See.Net.Services;

namespace See.Net.ViewModels;

/// <summary>主窗口视图模型：目录导航、文件列表与预览层控制。</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private CancellationTokenSource? _navCts;

    public MainViewModel(SettingsService settings, PreviewViewModel preview)
    {
        _settings = settings;
        Preview = preview;
    }

    public PreviewViewModel Preview { get; }

    [ObservableProperty]
    private string _currentDirectory = "";

    [ObservableProperty]
    private ObservableCollection<FileEntry> _entries = [];

    [ObservableProperty]
    private FileEntry? _selectedEntry;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private string _statusText = "";

    public async Task InitializeAsync()
    {
        string start = _settings.Current.LastDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!Directory.Exists(start)) start = @"C:\";
        await NavigateToAsync(start, addHistory: false);
    }

    public async Task NavigateToAsync(string path, bool addHistory = true)
    {
        if (!Directory.Exists(path))
        {
            string? parent = FileSystemService.GetParent(path);
            if (parent is not null) path = parent;
        }

        if (addHistory && CurrentDirectory.Length > 0 && !string.Equals(CurrentDirectory, path, StringComparison.OrdinalIgnoreCase))
        {
            _backStack.Push(CurrentDirectory);
            _forwardStack.Clear();
        }

        _navCts?.Cancel();
        var cts = new CancellationTokenSource();
        _navCts = cts;

        CurrentDirectory = path;
        StatusText = "正在加载…";
        try
        {
            var list = await Task.Run(() => FileSystemService.Enumerate(path), cts.Token);
            if (cts.IsCancellationRequested) return;
            Entries = new ObservableCollection<FileEntry>(list);
            SelectedEntry = null;
            StatusText = $"{list.Count} 项";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        UpdateNavigationState();
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        string? parent = FileSystemService.GetParent(CurrentDirectory);
        if (parent is not null) await NavigateToAsync(parent);
    }

    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        if (_backStack.Count == 0) return;
        _forwardStack.Push(CurrentDirectory);
        string target = _backStack.Pop();
        await NavigateToAsync(target, addHistory: false);
    }

    [RelayCommand]
    private async Task NavigateForwardAsync()
    {
        if (_forwardStack.Count == 0) return;
        _backStack.Push(CurrentDirectory);
        string target = _forwardStack.Pop();
        await NavigateToAsync(target, addHistory: false);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await NavigateToAsync(CurrentDirectory, addHistory: false);

    [RelayCommand]
    private async Task TogglePreviewAsync()
    {
        if (Preview.IsOpen)
        {
            await ClosePreviewAsync();
        }
        else
        {
            await OpenPreviewAsync(SelectedEntry);
        }
    }

    [RelayCommand]
    private async Task OpenPreviewAsync(FileEntry? file)
    {
        file ??= SelectedEntry;
        if (file is null) return;

        if (file.IsDirectory)
        {
            await NavigateToAsync(file.FullPath);
            return;
        }
        await OpenPreviewFileAsync(file);
    }

    public async Task OpenPreviewFileAsync(FileEntry file)
    {
        if (!await Preview.SaveIfDirtyAsync()) return;
        await Preview.LoadAsync(file);
        Preview.IsOpen = true;
    }

    [RelayCommand]
    private async Task ClosePreviewAsync()
    {
        if (!Preview.IsOpen) return;
        if (!await Preview.SaveIfDirtyAsync()) return;
        Preview.CloseDocument();
        Preview.IsOpen = false;
    }

    [RelayCommand]
    private async Task NextFileAsync()
    {
        await SwitchFileAsync(1);
    }

    [RelayCommand]
    private async Task PrevFileAsync()
    {
        await SwitchFileAsync(-1);
    }

    private async Task SwitchFileAsync(int direction)
    {
        if (!Preview.IsOpen || Entries.Count == 0) return;
        if (!await Preview.SaveIfDirtyAsync()) return;

        var files = Entries.Where(e => !e.IsDirectory).ToList();
        if (files.Count == 0) return;

        int index = files.FindIndex(f => f.FullPath == Preview.CurrentFile?.FullPath);
        if (index < 0) index = 0;
        int next = (index + direction + files.Count) % files.Count;
        SelectedEntry = files[next];
        await Preview.LoadAsync(files[next]);
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedEntry is null) return;
        try { Clipboard.SetText(SelectedEntry.FullPath); } catch { /* 忽略 */ }
    }

    private void UpdateNavigationState()
    {
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
    }
}
