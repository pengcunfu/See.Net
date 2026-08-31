using System.ComponentModel;
using System.Windows;
using See.ViewModels;

namespace See;

/// <summary>更新对话框：检查 → 下载（进度）→ 重启应用更新。</summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateViewModel _vm;

    public UpdateWindow(UpdateViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => _vm.PropertyChanged -= OnVmPropertyChanged;
        RefreshUi();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.CheckCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess())
            RefreshUi();
        else
            Dispatcher.BeginInvoke(RefreshUi);
    }

    private void RefreshUi()
    {
        VersionLine.Text = $"当前版本 v{_vm.CurrentVersion}"
            + (_vm.NewVersion is null ? "" : $"  →  新版本 v{_vm.NewVersion}");

        PrimaryButton.IsDefault = false;
        NotesScroller.Visibility = Visibility.Collapsed;
        NotesHeader.Visibility = Visibility.Collapsed;
        ProgressBar.IsIndeterminate = false;

        switch (_vm.State)
        {
            case UpdateFlowState.Checking:
                StateText.Text = _vm.Message;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
                PrimaryButton.Visibility = Visibility.Collapsed;
                SecondaryButton.Content = "关闭";
                break;
            case UpdateFlowState.UpToDate:
            case UpdateFlowState.Error:
                StateText.Text = _vm.Message;
                ProgressBar.Visibility = Visibility.Collapsed;
                PrimaryButton.Visibility = Visibility.Collapsed;
                SecondaryButton.Content = "确定";
                break;
            case UpdateFlowState.UpdateAvailable:
                StateText.Text = _vm.Message;
                ProgressBar.Visibility = Visibility.Collapsed;
                NotesHeader.Visibility = Visibility.Visible;
                NotesScroller.Visibility = Visibility.Visible;
                NotesText.Text = string.IsNullOrWhiteSpace(_vm.ReleaseNotes)
                    ? "（本次更新无发布说明）"
                    : _vm.ReleaseNotes;
                PrimaryButton.Visibility = Visibility.Visible;
                PrimaryButton.Content = "下载更新";
                PrimaryButton.IsDefault = true;
                SecondaryButton.Content = "稍后";
                break;
            case UpdateFlowState.Downloading:
                StateText.Text = _vm.Message;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.Value = _vm.Progress;
                PrimaryButton.Visibility = Visibility.Collapsed;
                SecondaryButton.Content = "后台下载";
                break;
            case UpdateFlowState.ReadyToRestart:
                StateText.Text = _vm.Message;
                ProgressBar.Visibility = Visibility.Collapsed;
                PrimaryButton.Visibility = Visibility.Visible;
                PrimaryButton.Content = "立即重启";
                PrimaryButton.IsDefault = true;
                SecondaryButton.Content = "稍后";
                break;
        }
    }

    private async void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        switch (_vm.State)
        {
            case UpdateFlowState.UpdateAvailable:
                await _vm.DownloadCommand.ExecuteAsync(null);
                RefreshUi();
                break;
            case UpdateFlowState.ReadyToRestart:
                if (MessageBox.Show(
                        "即将退出并重启应用以完成更新。\n未保存的编辑内容可能丢失，是否继续？",
                        "See.Net 更新",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question) != MessageBoxResult.OK)
                    return;
                _vm.RestartCommand.Execute(null);
                break;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
