using System.Globalization;
using System.Windows;
using See.Services;

namespace See;

/// <summary>设置窗口：开机自启动、保存前备份、十六进制字号与每行字节数。</summary>
public partial class SettingsWindow : Window
{
    private const double MinHexFontSize = 8;
    private const double MaxHexFontSize = 32;
    private const int MinBytesPerRow = 8;
    private const int MaxBytesPerRow = 32;

    private readonly SettingsService _settings;

    public SettingsWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        var s = settings.Current;
        AutoStartCheck.IsChecked = s.AutoStartEnabled;
        BackupCheck.IsChecked = s.BackupEnabled;
        HexFontSizeBox.Text = s.HexFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        BytesPerRowBox.Text = s.BytesPerRow.ToString(CultureInfo.InvariantCulture);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(HexFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fontSize)
            || fontSize < MinHexFontSize || fontSize > MaxHexFontSize)
        {
            MessageBox.Show($"十六进制字号需在 {MinHexFontSize:0}–{MaxHexFontSize:0} 之间。", "See.Net",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(BytesPerRowBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bytesPerRow)
            || bytesPerRow < MinBytesPerRow || bytesPerRow > MaxBytesPerRow)
        {
            MessageBox.Show($"每行字节数需在 {MinBytesPerRow}–{MaxBytesPerRow} 之间。", "See.Net",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool autoStart = AutoStartCheck.IsChecked == true;
        AutoStartService.Apply(autoStart);
        if (AutoStartService.IsEnabled() != autoStart)
        {
            MessageBox.Show("开机自启动设置失败，请稍后重试。", "See.Net",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return; // 保持对话框
        }

        var s = _settings.Current;
        s.AutoStartEnabled = autoStart;
        s.BackupEnabled = BackupCheck.IsChecked == true;
        s.HexFontSize = fontSize;
        s.BytesPerRow = bytesPerRow;
        _settings.Save();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}