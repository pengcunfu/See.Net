using System.Windows;
using System.Windows.Input;
using See.ViewModels;

namespace See.Views;

public partial class ShellPreviewWindow : Window
{
    public ShellPreviewWindow()
    {
        InitializeComponent();
    }

    private ShellPreviewViewModel? Vm => DataContext as ShellPreviewViewModel;

    /// <summary>
    /// 检查当前内容是否处于编辑模式
    /// </summary>
    public bool IsInEditMode()
    {
        if (Vm?.Preview?.Content is TextContentViewModel textVm)
        {
            return textVm.IsEditMode;
        }
        return false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 编辑模式下不拦截按键，让编辑器处理
        if (IsInEditMode())
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                ClosePreview();
                break;
            case Key.Down:
                e.Handled = true;
                Vm?.NextCommand.Execute(null);
                break;
            case Key.Up:
                e.Handled = true;
                Vm?.PreviousCommand.Execute(null);
                break;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => ClosePreview();

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void OnTogglePin(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinButton();
    }

    private void UpdatePinButton()
    {
        if (Topmost)
        {
            PinButton.Content = "\uE840"; // 置顶图标
            PinButton.ToolTip = "取消置顶";
        }
        else
        {
            PinButton.Content = "\uE718"; // 非置顶图标
            PinButton.ToolTip = "切换置顶";
        }
    }

    public void ClosePreview()
    {
        // 延迟到当前事件处理完成后执行，避免在键盘/鼠标事件期间销毁编辑器
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                Vm?.DisposeContent();
            }
            catch
            {
                // 忽略清理异常，避免阻止窗口关闭
            }
            Hide();
        });
    }
}
