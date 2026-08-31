using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace See;

/// <summary>关于对话框：产品名、版本、开源/文档地址、飞书群与版权信息。</summary>
public partial class AboutWindow : Window
{
    private readonly Action? _checkUpdates;

    public AboutWindow(Action? checkUpdates = null)
    {
        InitializeComponent();
        _checkUpdates = checkUpdates;

        ProductNameText.Text = "See.Net";
        VersionText.Text = $"版本 {AppVersion.Display}";
        CopyrightText.Text = "© 2026 See. All rights reserved.";
        CheckUpdatesButton.Visibility = _checkUpdates is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        try { _checkUpdates?.Invoke(); } catch { }
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch { /* 忽略无法打开浏览器的情况 */ }
        e.Handled = true;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
