using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using See.Net.Core;

namespace See.Controls;

/// <summary>
/// 自绘十六进制编辑器控件（实现 IScrollInfo 支持任意大小文件的虚拟化滚动）。
/// 交互：方向键移动、Shift 扩展选择、Tab 切换 Hex/ASCII 区、Insert 切换覆盖/插入、
/// Delete/Backspace 删除、Ctrl+A 全选、Ctrl+C 复制 Hex、Ctrl+Shift+C 复制 ASCII、Ctrl+Alt+C 复制 C 数组。
/// </summary>
public sealed class HexEditor : FrameworkElement, IScrollInfo
{
    private const double RowPadding = 3;

    private long _caretOffset;
    private int _caretNibble;
    private long _anchorOffset;
    private bool _asciiPane;
    private double _rowHeight = 20;
    private double _charWidth = 8;
    private double _gutterWidth;
    private double _hexAreaStart;
    private double _hexCellWidth;
    private double _asciiAreaStart;
    private double _contentWidth;
    private double _viewportWidth;
    private double _viewportHeight;
    private double _verticalOffset;
    private double _horizontalOffset;
    private bool _isDragging;

    private IReadOnlyList<(long Start, long Length)> _searchMatches = [];
    private int _currentMatchIndex = -1;

    public HexEditor()
    {
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        Background = Brushes.White;
        Cursor = Cursors.IBeam;
        FocusVisualStyle = null;
    }

    public event EventHandler? EditPerformed;
    public event EventHandler? CaretChanged;
    public event EventHandler? SelectionChanged;
    public event EventHandler? ModeChanged;

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(HexDocument), typeof(HexEditor),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnDocumentChanged));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(HexEditor),
        new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty BytesPerRowProperty = DependencyProperty.Register(
        nameof(BytesPerRow), typeof(int), typeof(HexEditor),
        new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(HexEditor), new PropertyMetadata(false));

    public static readonly DependencyProperty InsertModeProperty = DependencyProperty.Register(
        nameof(InsertMode), typeof(bool), typeof(HexEditor), new PropertyMetadata(false));

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffsetBrushProperty = DependencyProperty.Register(
        nameof(OffsetBrush), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionBrushProperty = DependencyProperty.Register(
        nameof(SelectionBrush), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.LightSteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EditedBrushProperty = DependencyProperty.Register(
        nameof(EditedBrush), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.LightGoldenrodYellow, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MatchBrushProperty = DependencyProperty.Register(
        nameof(MatchBrush), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaretBrushProperty = DependencyProperty.Register(
        nameof(CaretBrush), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.DarkBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(HexEditor),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public HexDocument? Document
    {
        get => (HexDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public int BytesPerRow
    {
        get => (int)GetValue(BytesPerRowProperty);
        set => SetValue(BytesPerRowProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool InsertMode
    {
        get => (bool)GetValue(InsertModeProperty);
        set => SetValue(InsertModeProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public Brush OffsetBrush
    {
        get => (Brush)GetValue(OffsetBrushProperty);
        set => SetValue(OffsetBrushProperty, value);
    }

    public Brush SelectionBrush
    {
        get => (Brush)GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public Brush EditedBrush
    {
        get => (Brush)GetValue(EditedBrushProperty);
        set => SetValue(EditedBrushProperty, value);
    }

    public Brush MatchBrush
    {
        get => (Brush)GetValue(MatchBrushProperty);
        set => SetValue(MatchBrushProperty, value);
    }

    public Brush CaretBrush
    {
        get => (Brush)GetValue(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public long CaretOffset => _caretOffset;
    public bool HasSelection => _caretOffset != _anchorOffset;

    public long SelectionStart => Math.Min(_caretOffset, _anchorOffset);
    public long SelectionEnd => Math.Max(_caretOffset, _anchorOffset);
    public long SelectionLength => SelectionEnd - SelectionStart;

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (HexEditor)d;
        editor._caretOffset = 0;
        editor._caretNibble = 0;
        editor._anchorOffset = 0;
        editor._searchMatches = [];
        editor._currentMatchIndex = -1;
        editor.InvalidateMeasure();
        editor.InvalidateVisual();
        editor.ScrollOwner?.InvalidateScrollInfo();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (HexEditor)d;
        editor.InvalidateMeasure();
        editor.ScrollOwner?.InvalidateScrollInfo();
    }

    public void SetSearchMatches(IReadOnlyList<(long Start, long Length)> matches, int currentIndex = -1)
    {
        _searchMatches = matches;
        _currentMatchIndex = currentIndex;
        InvalidateVisual();
    }

    public void ClearSearch()
    {
        _searchMatches = [];
        _currentMatchIndex = -1;
        InvalidateVisual();
    }

    public void SelectRange(long start, long length)
    {
        var doc = Document;
        if (doc is null) return;
        start = Math.Clamp(start, 0, doc.Length);
        length = Math.Clamp(length, 0, doc.Length - start);
        _anchorOffset = start;
        _caretOffset = start + length;
        _caretNibble = 0;
        EnsureCaretVisible();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void MoveCaretTo(long offset)
    {
        var doc = Document;
        if (doc is null) return;
        _caretOffset = Math.Clamp(offset, 0, doc.Length);
        _caretNibble = 0;
        _anchorOffset = _caretOffset;
        EnsureCaretVisible();
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public byte[] GetSelectionBytes()
    {
        if (!HasSelection || Document is null) return [];
        return Document.ReadBytes(SelectionStart, (int)SelectionLength);
    }

    public void CopyHex() => Copy(HexFormat.ToHexSpaced(GetSelectionBytes()));
    public void CopyAscii() => Copy(HexFormat.ToAscii(GetSelectionBytes()));
    public void CopyCArray() => Copy(HexFormat.ToCArray(GetSelectionBytes()));

    private static void Copy(string text)
    {
        if (text.Length == 0) return;
        try { Clipboard.SetText(text); } catch { /* 剪贴板被占用时忽略 */ }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ComputeMetrics();
        long rows = RowCount;
        double contentHeight = rows * _rowHeight;
        _viewportWidth = availableSize.Width;
        _viewportHeight = availableSize.Height;

        if (ScrollOwner is null)
        {
            return new Size(_contentWidth, contentHeight);
        }
        return availableSize;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _viewportWidth = sizeInfo.NewSize.Width;
        _viewportHeight = sizeInfo.NewSize.Height;
        ClampOffsets();
        ScrollOwner?.InvalidateScrollInfo();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var doc = Document;
        dc.DrawRectangle(Background, null, new Rect(RenderSize));
        if (doc is null || RenderSize.Height <= 0 || _rowHeight <= 0) return;

        long rowCount = RowCount;
        long firstRow = (long)Math.Floor(_verticalOffset / _rowHeight);
        firstRow = Math.Clamp(firstRow, 0, rowCount - 1);
        long visibleRows = (long)Math.Ceiling((_verticalOffset + _viewportHeight) / _rowHeight) - firstRow + 1;
        visibleRows = Math.Min(visibleRows, rowCount - firstRow);

        double xShift = -_horizontalOffset;
        var typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var offsetFormat = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, TextBrush, 1.0);
        double lineHeight = offsetFormat.Height;

        for (long r = 0; r < visibleRows; r++)
        {
            long row = firstRow + r;
            long rowStart = row * BytesPerRow;
            int count = (int)Math.Min(BytesPerRow, doc.Length - rowStart);
            byte[] bytes = doc.ReadBytes(rowStart, count);
            double y = row * _rowHeight;

            // 偏移列
            var offsetText = new FormattedText(HexFormat.FormatOffset(rowStart),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, OffsetBrush, 1.0);
            dc.DrawText(offsetText, new Point(xShift + 4, y + (RowPadding - 1)));

            // 行背景选择/编辑/搜索高亮
            for (int i = 0; i < count; i++)
            {
                long absolute = rowStart + i;
                double cx = xShift + _hexAreaStart + i * _hexCellWidth;
                var hexRect = new Rect(cx, y, _hexCellWidth, _rowHeight);
                var asciiRect = new Rect(xShift + _asciiAreaStart + i * _charWidth, y, _charWidth, _rowHeight);

                if (IsInSelection(absolute))
                    dc.DrawRectangle(SelectionBrush, null, Rect.Union(hexRect, asciiRect));
                else if (doc.IsEdited(absolute))
                    dc.DrawRectangle(EditedBrush, null, Rect.Union(hexRect, asciiRect));

                foreach (var (ms, ml) in _searchMatches)
                {
                    if (absolute >= ms && absolute < ms + ml)
                    {
                        dc.DrawRectangle(MatchBrush, null, new Rect(hexRect.X, hexRect.Y, hexRect.Width * ml, hexRect.Height));
                        dc.DrawRectangle(MatchBrush, null, new Rect(asciiRect.X, asciiRect.Y, asciiRect.Width * ml, asciiRect.Height));
                        break;
                    }
                }
            }

            // 十六进制文本
            var hexText = new FormattedText(HexFormat.ToHexSpaced(bytes),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, TextBrush, 1.0);
            dc.DrawText(hexText, new Point(xShift + _hexAreaStart, y + (RowPadding - 1)));

            // ASCII 文本
            var asciiText = new FormattedText(HexFormat.ToAscii(bytes),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, TextBrush, 1.0);
            dc.DrawText(asciiText, new Point(xShift + _asciiAreaStart, y + (RowPadding - 1)));

            // 分隔线
            if (r > 0)
            {
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)), 1),
                    new Point(xShift, y), new Point(xShift + _contentWidth, y));
            }

            // 选中区域内的首行绘制分隔线（高亮字节之间的细线）
            _ = lineHeight;
        }

        DrawCaret(dc, typeface);
    }

    private void DrawCaret(DrawingContext dc, Typeface typeface)
    {
        var doc = Document;
        if (doc is null || _caretOffset >= doc.Length) return;
        long row = _caretOffset / BytesPerRow;
        int col = (int)(_caretOffset % BytesPerRow);
        double y = row * _rowHeight;
        if (y + _rowHeight < _verticalOffset || y > _verticalOffset + _viewportHeight) return;

        double x;
        double width;
        if (_asciiPane)
        {
            x = _asciiAreaStart + col * _charWidth - _horizontalOffset;
            width = _charWidth;
        }
        else
        {
            x = _hexAreaStart + col * _hexCellWidth + _caretNibble * _charWidth - _horizontalOffset;
            width = _charWidth;
        }
        var pen = new Pen(CaretBrush, 1.4);
        dc.DrawRectangle(null, pen, new Rect(x + 0.7, y + 2, width - 1.4, _rowHeight - 4));
    }

    private void ComputeMetrics()
    {
        var typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var sample = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.Black, 1.0);
        _charWidth = Math.Max(sample.WidthIncludingTrailingWhitespace, 6);
        _rowHeight = Math.Max(sample.Height + RowPadding * 2, 18);
        _hexCellWidth = _charWidth * 3;
        _gutterWidth = _charWidth * 10 + 8;
        _hexAreaStart = _gutterWidth + 8;
        _asciiAreaStart = _hexAreaStart + BytesPerRow * _hexCellWidth + 16;
        _contentWidth = _asciiAreaStart + BytesPerRow * _charWidth + 16;
    }

    private long RowCount
    {
        get
        {
            var doc = Document;
            if (doc is null || doc.Length == 0) return 1;
            return (doc.Length + BytesPerRow - 1) / BytesPerRow;
        }
    }

    private bool IsInSelection(long offset) => offset >= SelectionStart && offset < SelectionEnd;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.ChangedButton != MouseButton.Left) return;
        _isDragging = true;
        CaptureMouse();
        HitTestToCaret(e.GetPosition(this), extend: (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging) return;
        HitTestToCaret(e.GetPosition(this), extend: true);
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _isDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HitTestToCaret(Point point, bool extend)
    {
        var doc = Document;
        if (doc is null) return;
        long row = (long)Math.Floor((point.Y + _verticalOffset) / _rowHeight);
        row = Math.Clamp(row, 0, RowCount - 1);
        long rowStart = row * BytesPerRow;
        double x = point.X + _horizontalOffset;

        int col;
        if (x >= _asciiAreaStart)
        {
            _asciiPane = true;
            col = (int)Math.Clamp((x - _asciiAreaStart) / _charWidth, 0, BytesPerRow - 1);
        }
        else
        {
            _asciiPane = false;
            col = (int)Math.Clamp((x - _hexAreaStart) / _hexCellWidth, 0, BytesPerRow - 1);
            double within = x - (_hexAreaStart + col * _hexCellWidth);
            _caretNibble = within < _charWidth * 2 ? (within < _charWidth ? 0 : 1) : 1;
        }

        long offset = Math.Min(rowStart + col, doc.Length);
        if (!extend) _anchorOffset = offset;
        _caretOffset = offset;
        EnsureCaretVisible();
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var doc = Document;
        if (doc is null) return;

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        switch (e.Key)
        {
            case Key.Left:
                MoveCaretBy(shift ? -1 : -1, shift, ctrl);
                e.Handled = true;
                return;
            case Key.Right:
                MoveCaretBy(1, shift, ctrl);
                e.Handled = true;
                return;
            case Key.Up:
                MoveCaretBy(-BytesPerRow, shift, ctrl);
                e.Handled = true;
                return;
            case Key.Down:
                MoveCaretBy(BytesPerRow, shift, ctrl);
                e.Handled = true;
                return;
            case Key.Home:
                MoveCaretTo(ctrl ? 0 : _caretOffset / BytesPerRow * BytesPerRow, shift);
                e.Handled = true;
                return;
            case Key.End:
                MoveCaretTo(ctrl ? doc.Length : Math.Min((_caretOffset / BytesPerRow + 1) * BytesPerRow - 1, doc.Length), shift);
                e.Handled = true;
                return;
            case Key.PageUp:
                MoveCaretBy(-(long)(_viewportHeight / _rowHeight) * BytesPerRow, shift);
                e.Handled = true;
                return;
            case Key.PageDown:
                MoveCaretBy((long)(_viewportHeight / _rowHeight) * BytesPerRow, shift);
                e.Handled = true;
                return;
            case Key.Tab:
                _asciiPane = !_asciiPane;
                _caretNibble = 0;
                e.Handled = true;
                InvalidateVisual();
                return;
            case Key.Insert:
                if (!IsReadOnly)
                {
                    InsertMode = !InsertMode;
                    ModeChanged?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                return;
            case Key.Delete:
                if (!IsReadOnly) DeleteSelectionOrByte(forward: true);
                e.Handled = true;
                return;
            case Key.Back:
                if (!IsReadOnly) DeleteSelectionOrByte(forward: false);
                e.Handled = true;
                return;
            case Key.A when ctrl:
                _anchorOffset = 0;
                _caretOffset = doc.Length;
                _caretNibble = 0;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
                e.Handled = true;
                return;
            case Key.C when ctrl && alt:
                CopyCArray();
                e.Handled = true;
                return;
            case Key.C when ctrl && shift:
                CopyAscii();
                e.Handled = true;
                return;
            case Key.C when ctrl:
                CopyHex();
                e.Handled = true;
                return;
        }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        var doc = Document;
        if (doc is null || IsReadOnly || string.IsNullOrEmpty(e.Text)) return;

        char ch = e.Text[0];
        if (_asciiPane)
        {
            if (ch < 0x20 || ch > 0x7E) return;
            ReplaceSelectionOrType([(byte)ch]);
        }
        else if (ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')
        {
            TypeHexNibble(ch);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }

    private void TypeHexNibble(char ch)
    {
        var doc = Document!;
        byte value = ch switch
        {
            >= '0' and <= '9' => (byte)(ch - '0'),
            >= 'a' and <= 'f' => (byte)(ch - 'a' + 10),
            _ => (byte)(ch - 'A' + 10),
        };

        if (HasSelection)
        {
            doc.DeleteRange(SelectionStart, SelectionLength);
            _caretOffset = SelectionStart;
            _anchorOffset = _caretOffset;
            _caretNibble = 0;
        }

        if (InsertMode)
        {
            if (_caretNibble == 0)
            {
                doc.InsertBytes(_caretOffset, [value]);
                _caretNibble = 1;
            }
            else
            {
                byte current = doc.ReadByte(_caretOffset);
                doc.WriteByte(_caretOffset, (byte)((current & 0xF0) | value));
                _caretNibble = 0;
                _caretOffset++;
            }
        }
        else
        {
            byte current = _caretOffset < doc.Length ? doc.ReadByte(_caretOffset) : (byte)0;
            byte merged;
            if (_caretNibble == 0)
            {
                merged = (byte)((current & 0x0F) | (value << 4));
            }
            else
            {
                merged = (byte)((current & 0xF0) | value);
            }
            if (_caretOffset >= doc.Length) doc.InsertBytes(_caretOffset, [merged]);
            else doc.WriteByte(_caretOffset, merged);

            if (_caretNibble == 1)
            {
                _caretNibble = 0;
                _caretOffset++;
            }
            else
            {
                _caretNibble = 1;
            }
        }

        EnsureCaretVisible();
        EditPerformed?.Invoke(this, EventArgs.Empty);
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void ReplaceSelectionOrType(byte[] bytes)
    {
        var doc = Document!;
        if (HasSelection)
        {
            doc.WriteBytes(SelectionStart, bytes);
            _caretOffset = SelectionStart + bytes.Length;
            _anchorOffset = _caretOffset;
        }
        else if (InsertMode)
        {
            doc.InsertBytes(_caretOffset, bytes);
            _caretOffset += bytes.Length;
        }
        else
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                long pos = _caretOffset + i;
                if (pos < doc.Length) doc.WriteByte(pos, bytes[i]);
                else doc.InsertBytes(pos, [bytes[i]]);
            }
            _caretOffset += bytes.Length;
        }
        _caretNibble = 0;
        EnsureCaretVisible();
        EditPerformed?.Invoke(this, EventArgs.Empty);
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void DeleteSelectionOrByte(bool forward)
    {
        var doc = Document!;
        if (HasSelection)
        {
            doc.DeleteRange(SelectionStart, SelectionLength);
            _caretOffset = SelectionStart;
            _anchorOffset = _caretOffset;
        }
        else if (forward)
        {
            if (_caretOffset < doc.Length) doc.DeleteRange(_caretOffset, 1);
        }
        else
        {
            if (_caretOffset > 0) doc.DeleteRange(_caretOffset - 1, 1);
            _caretOffset = Math.Max(0, _caretOffset - 1);
        }
        _caretNibble = 0;
        EnsureCaretVisible();
        EditPerformed?.Invoke(this, EventArgs.Empty);
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void MoveCaretBy(long delta, bool extend, bool ctrl = false)
    {
        var doc = Document!;
        long target = Math.Clamp(_caretOffset + delta, 0, doc.Length);
        if (ctrl && delta < 0) target = 0;
        if (ctrl && delta > 0) target = doc.Length;
        _caretOffset = target;
        _caretNibble = 0;
        if (!extend) _anchorOffset = _caretOffset;
        EnsureCaretVisible();
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void MoveCaretTo(long offset, bool extend)
    {
        var doc = Document!;
        _caretOffset = Math.Clamp(offset, 0, doc.Length);
        _caretNibble = 0;
        if (!extend) _anchorOffset = _caretOffset;
        EnsureCaretVisible();
        CaretChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void EnsureCaretVisible()
    {
        long row = _caretOffset / BytesPerRow;
        double top = row * _rowHeight;
        double bottom = top + _rowHeight;
        if (top < _verticalOffset) SetVerticalOffset(top);
        else if (bottom > _verticalOffset + _viewportHeight) SetVerticalOffset(bottom - _viewportHeight);
    }

    // ---------- IScrollInfo ----------

    private ScrollViewer? _scrollOwner;

    public ScrollViewer? ScrollOwner
    {
        get => _scrollOwner;
        set
        {
            if (_scrollOwner != value)
            {
                _scrollOwner = value;
                ScrollOwner?.InvalidateScrollInfo();
            }
        }
    }

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }

    public double ExtentWidth => _contentWidth;
    public double ExtentHeight => RowCount * _rowHeight;
    public double ViewportWidth => _viewportWidth;
    public double ViewportHeight => _viewportHeight;
    public double HorizontalOffset => _horizontalOffset;
    public double VerticalOffset => _verticalOffset;

    public void LineUp() => SetVerticalOffset(_verticalOffset - _rowHeight);
    public void LineDown() => SetVerticalOffset(_verticalOffset + _rowHeight);
    public void LineLeft() => SetHorizontalOffset(_horizontalOffset - _charWidth * 3);
    public void LineRight() => SetHorizontalOffset(_horizontalOffset + _charWidth * 3);
    public void MouseWheelUp() => SetVerticalOffset(_verticalOffset - _rowHeight * 3);
    public void MouseWheelDown() => SetVerticalOffset(_verticalOffset + _rowHeight * 3);
    public void MouseWheelLeft() => SetHorizontalOffset(_horizontalOffset - _charWidth * 3);
    public void MouseWheelRight() => SetHorizontalOffset(_horizontalOffset + _charWidth * 3);
    public void PageUp() => SetVerticalOffset(_verticalOffset - _viewportHeight);
    public void PageDown() => SetVerticalOffset(_verticalOffset + _viewportHeight);
    public void PageLeft() => SetHorizontalOffset(_horizontalOffset - _viewportWidth);
    public void PageRight() => SetHorizontalOffset(_horizontalOffset + _viewportWidth);

    public void SetHorizontalOffset(double offset)
    {
        _horizontalOffset = Math.Clamp(offset, 0, Math.Max(0, ExtentWidth - ViewportWidth));
        InvalidateVisual();
        ScrollOwner?.InvalidateScrollInfo();
    }

    public void SetVerticalOffset(double offset)
    {
        _verticalOffset = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        InvalidateVisual();
        ScrollOwner?.InvalidateScrollInfo();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

    private void ClampOffsets()
    {
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0, Math.Max(0, ExtentWidth - ViewportWidth));
        _verticalOffset = Math.Clamp(_verticalOffset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
    }
}
