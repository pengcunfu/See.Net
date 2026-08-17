using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace See;

/// <summary>关于对话框：产品名、版本、开源/文档地址、飞书群与版权信息。</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version;
        var copyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
            ?? "© 2026 See. All rights reserved.";

        ProductNameText.Text = "See.Net";
        VersionText.Text = version is null
            ? "版本未知"
            : $"版本 {version.Major}.{version.Minor}.{version.Build}";
        CopyrightText.Text = copyright;
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
