using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using See.Services;

namespace See.ViewModels;

/// <summary>更新流程状态。</summary>
public enum UpdateFlowState
{
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToRestart,
    Error,
}

/// <summary>更新对话框 ViewModel：检查 → 下载（进度）→ 重启应用更新。</summary>
public partial class UpdateViewModel : ObservableObject
{
    private readonly UpdateService _updateService;
    private Velopack.UpdateInfo? _info;

    [ObservableProperty]
    private UpdateFlowState _state = UpdateFlowState.Checking;

    [ObservableProperty]
    private string _message = "正在检查更新…";

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _newVersion;

    public string CurrentVersion { get; } = AppVersion.Display;

    public UpdateViewModel(UpdateService updateService)
    {
        _updateService = updateService;
    }

    /// <summary>检查更新（窗口加载后自动触发）。</summary>
    [RelayCommand]
    private async Task CheckAsync()
    {
        if (!_updateService.IsUpdateCapable)
        {
            State = UpdateFlowState.Error;
            Message = "当前为手动安装的版本，无法自动更新。\n"
                + "请前往 GitHub Releases 下载最新安装包：\nhttps://github.com/pengcunfu/See.Net/releases";
            return;
        }

        State = UpdateFlowState.Checking;
        Message = "正在检查更新…";
        try
        {
            _info = await _updateService.CheckForUpdatesAsync();
            if (_info is null)
            {
                State = UpdateFlowState.UpToDate;
                Message = $"当前已是最新版本 v{CurrentVersion}";
                return;
            }

            NewVersion = _info.TargetFullRelease.Version.ToNormalizedString();
            ReleaseNotes = _info.TargetFullRelease.NotesMarkdown;
            State = UpdateFlowState.UpdateAvailable;
            Message = $"发现新版本 v{NewVersion}";
        }
        catch (Exception ex)
        {
            State = UpdateFlowState.Error;
            Message = $"检查更新失败：{ex.Message}";
        }
    }

    /// <summary>下载更新并显示进度（0..100）。</summary>
    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (_info is null) return;
        State = UpdateFlowState.Downloading;
        Message = "正在下载更新…";
        var progress = new Progress<int>(p =>
        {
            Progress = p;
            Message = $"正在下载更新 {p}%";
        });
        try
        {
            _info = await _updateService.DownloadUpdatesAsync(_info, progress);
            State = UpdateFlowState.ReadyToRestart;
            Message = "更新已下载完成，重启后生效。";
        }
        catch (Exception ex)
        {
            State = UpdateFlowState.Error;
            Message = $"下载失败：{ex.Message}";
        }
    }

    /// <summary>立即退出进程、应用更新并重启。调用后当前进程将退出。</summary>
    [RelayCommand]
    private void Restart()
    {
        if (_info is null) return;
        _updateService.RestartAndApply(_info);
    }
}
