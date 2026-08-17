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

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
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
            PinButton.Content = "&#xE840;"; // 置顶图标
            PinButton.ToolTip = "取消置顶";
        }
        else
        {
            PinButton.Content = "&#xE718;"; // 非置顶图标
            PinButton.ToolTip = "切换置顶";
        }
    }

    public void ClosePreview()
    {
        Vm?.DisposeContent();
        Hide();
    }
}
