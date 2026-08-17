using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace See.Net.ViewModels;

/// <summary>提示信息内容（不支持预览、文件过大等）。</summary>
public sealed class InfoContentViewModel
{
    public InfoContentViewModel(string title, string message, string? actionLabel = null, Action? action = null)
    {
        Title = title;
        Message = message;
        ActionLabel = actionLabel;
        ActionCommand = action is null ? null : new RelayCommand(action);
    }

    public string Title { get; }
    public string Message { get; }
    public string? ActionLabel { get; }
    public ICommand? ActionCommand { get; }
}
