using System.Globalization;
using System.Windows;
using System.Windows.Media;
using See.Services;

namespace See;

/// <summary>设置窗口：字体、开机自启动、保存前备份、十六进制字号与每行字节数。</summary>
public partial class SettingsWindow : Window
{
    private const double MinFontSize = 8;
    private const double MaxFontSize = 72;
    private const double MinHexFontSize = 8;
    private const double MaxHexFontSize = 32;
    private const int MinBytesPerRow = 8;
    private const int MaxBytesPerRow = 32;

    private readonly SettingsService _settings;

    public SettingsWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        // 填充等宽字体列表
        var monoFonts = Fonts.SystemFontFamilies
            .Where(f => IsMonospaced(f))
            .OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontFamilyCombo.ItemsSource = monoFonts;

        var s = settings.Current;
        AutoStartCheck.IsChecked = s.AutoStartEnabled;
        BackupCheck.IsChecked = s.BackupEnabled;
        CheckUpdatesCheck.IsChecked = s.CheckUpdatesOnStartup;
        FontFamilyCombo.Text = s.TextFontFamily;
        TextFontSizeBox.Text = s.TextFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        HexFontSizeBox.Text = s.HexFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        BytesPerRowBox.Text = s.BytesPerRow.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsMonospaced(FontFamily family)
    {
        var typefaces = family.GetTypefaces();
        if (typefaces.Count == 0) return false;
        if (!typefaces.First().TryGetGlyphTypeface(out var gt)) return false;
        // 等宽字体的 'i' 和 'W' 字形宽度相同
        ushort iGlyph = gt.CharacterToGlyphMap.TryGetValue('i', out var ig) ? ig : (ushort)0;
        ushort wGlyph = gt.CharacterToGlyphMap.TryGetValue('W', out var wg) ? wg : (ushort)0;
        return gt.AdvanceWidths[iGlyph] == gt.AdvanceWidths[wGlyph];
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string fontFamilyText = FontFamilyCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(fontFamilyText))
        {
            MessageBox.Show("请输入或选择字体名称。", "See.Net",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TextFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double textFontSize)
            || textFontSize < MinFontSize || textFontSize > MaxFontSize)
        {
            MessageBox.Show($"文本字号需在 {MinFontSize:0}–{MaxFontSize:0} 之间。", "See.Net",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
        s.CheckUpdatesOnStartup = CheckUpdatesCheck.IsChecked == true;
        s.TextFontFamily = fontFamilyText;
        s.TextFontSize = textFontSize;
        s.HexFontSize = fontSize;
        s.BytesPerRow = bytesPerRow;
        _settings.Save();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}