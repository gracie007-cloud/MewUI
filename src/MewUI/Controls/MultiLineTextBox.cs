using System.Buffers;

using Aprillz.MewUI.Core;
using Aprillz.MewUI.Elements;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Panels;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;
using WrapLayout = Aprillz.MewUI.Controls.TextWrapVirtualizer.WrapLayout;

namespace Aprillz.MewUI.Controls;

/// <summary>
/// A multi-line text input control with thin scrollbars.
/// </summary>
public sealed class MultiLineTextBox : TextBase
    , IVisualTreeHost
{
    private const int WrapSegmentHardLimit = 4096;
    private const int WrapLineCountHardLimit = 4096;
    private const int ExtentWidthLineCountHardLimit = 4096;

    private double _lineHeight;
    private readonly List<int> _lineStarts = new() { 0 };

    private int _pendingViewAnchorIndex = -1;
    private double _pendingViewAnchorYOffset;
    private double _pendingViewAnchorXOffset;

    private readonly Dictionary<int, CachedLine> _lineTextCache = new();
    private readonly TextWrapVirtualizer _wrapVirtualizer;
    private readonly TextLineWidthEstimator _lineWidthEstimator;

    // Threshold for larger cache sizes.
    private const int LargeDocumentThreshold = 1000;

    // Undo/Redo handled by TextBase.

    private readonly ScrollBar _vBar;
    private readonly ScrollBar _hBar;

    protected override Color DefaultBackground => Theme.Current.Palette.ControlBackground;
    protected override Color DefaultBorderBrush => Theme.Current.Palette.ControlBorder;

    public MultiLineTextBox()
    {
        BorderThickness = 1;
        Padding = new Thickness(4);
        MinHeight = Theme.Current.BaseControlHeight;
        AcceptReturn = true;

        _wrapVirtualizer = new TextWrapVirtualizer(
            GetLineSpan,
            GetLineText,
            () => DocumentVersion,
            () => _lineStarts.Count,
            GetTextLengthCore,
            WrapSegmentHardLimit);

        _lineWidthEstimator = new TextLineWidthEstimator(
            GetLineSpan,
            Document.CopyTo,
            () => _lineStarts.Count,
            GetTextLengthCore);

        _vBar = new ScrollBar { Orientation = Orientation.Vertical, IsVisible = false };
        _hBar = new ScrollBar { Orientation = Orientation.Horizontal, IsVisible = false };
        _vBar.Parent = this;
        _hBar.Parent = this;

        _vBar.ValueChanged += v => SetVerticalOffset(v);
        _hBar.ValueChanged += v => SetHorizontalOffset(v);
    }

    protected override Rect GetInteractionContentBounds()
        => GetViewportContentBounds();

    protected override Rect AdjustViewportBoundsForScrollbars(Rect innerBounds, Theme theme)
    {
        var viewportBounds = innerBounds;
        if (_vBar.IsVisible)
        {
            viewportBounds = viewportBounds.Deflate(new Thickness(0, 0, theme.ScrollBarHitThickness, 0));
        }

        if (_hBar.IsVisible)
        {
            viewportBounds = viewportBounds.Deflate(new Thickness(0, 0, 0, theme.ScrollBarHitThickness));
        }

        return viewportBounds;
    }

    protected override void SetCaretFromPoint(Point point, Rect contentBounds) => SetCaretFromPointCore(point, contentBounds);

    protected override void AutoScrollForSelectionDrag(Point point, Rect contentBounds)
    {
        const double edgeDip = 10;
        if (point.Y < contentBounds.Y + edgeDip)
        {
            SetVerticalOffset(VerticalOffset + point.Y - (contentBounds.Y + edgeDip), invalidateVisual: false);
        }
        else if (point.Y > contentBounds.Bottom - edgeDip)
        {
            SetVerticalOffset(VerticalOffset + point.Y - (contentBounds.Bottom - edgeDip), invalidateVisual: false);
        }

        if (point.X < contentBounds.X + edgeDip)
        {
            SetHorizontalOffset(HorizontalOffset + point.X - (contentBounds.X + edgeDip), invalidateVisual: false);
        }
        else if (point.X > contentBounds.Right - edgeDip)
        {
            SetHorizontalOffset(HorizontalOffset + point.X - (contentBounds.Right - edgeDip), invalidateVisual: false);
        }

        ClampOffsets(contentBounds);
    }

    protected override void EnsureCaretVisibleCore(Rect contentBounds) => EnsureCaretVisible(contentBounds);

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        base.OnThemeChanged(oldTheme, newTheme);

        if (MinHeight == oldTheme.BaseControlHeight)
        {
            MinHeight = newTheme.BaseControlHeight;
        }
    }

    /// <summary>
    /// Enables hard-wrapping at the available width. When enabled, horizontal scrolling is disabled.
    /// </summary>
    public bool Wrap
    {
        get => WrapEnabled;
        set
        {
            if (WrapEnabled == value)
            {
                return;
            }

            if (value && _lineStarts.Count > WrapLineCountHardLimit)
            {
                // Cannot re-enable for very large documents.
                NotifyWrapChanged(false);
                return;
            }

            SetWrapEnabled(value);
        }
    }

    protected override bool SupportsWrap => true;

    protected override TextAlignment PlaceholderVerticalAlignment => TextAlignment.Top;

    protected override void OnWrapChanged(bool oldValue, bool newValue)
    {
        CaptureViewAnchor();

        _wrapVirtualizer.Reset();
        _lineWidthEstimator.Reset();
        SetHorizontalOffset(0);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private bool CanComputeExtentWidth() => !WrapEnabled && _lineStarts.Count <= ExtentWidthLineCountHardLimit;

    protected override void OnTextChanged(string oldText, string newText)
    {
        base.OnTextChanged(oldText, newText);
        InvalidateMeasure();
    }

    protected override void SetTextCore(string normalizedText)
    {
        _lineTextCache.Clear();
        _wrapVirtualizer.Reset();
        _lineWidthEstimator.Reset();
        base.SetTextCore(normalizedText);
        RebuildLineStartsFromDocument();
        EnforceWrapLineLimit();
    }

    protected override void ApplyInsertForEdit(int index, string text) => ApplyInsert(index, text);

    protected override void ApplyRemoveForEdit(int index, int length) => ApplyRemove(index, length);

    protected override void OnEditCommitted()
    {
        EnforceWrapLineLimit();
        InvalidateMeasure();
        InvalidateVisual();
        NotifyTextChanged();
    }

    protected override Size MeasureContent(Size availableSize)
    {
        _lineHeight = Math.Max(16, FontSize * 1.4);
        if (Document.Length > 0)
        {
            using var measure = BeginTextMeasurement();
            _lineHeight = Math.Max(_lineHeight, measure.Context.MeasureText("Mg", measure.Font).Height);
        }

        double minHeight = _lineHeight * 3 + Padding.VerticalThickness + 4;
        return new Size(240, minHeight);
    }

    protected override void ArrangeContent(Rect bounds)
    {
        base.ArrangeContent(bounds);

        var theme = GetTheme();
        var snapped = GetSnappedBorderBounds(Bounds);
        var borderInset = GetBorderVisualInset();
        var innerBounds = snapped.Deflate(new Thickness(borderInset));

        double wrapWidth = Math.Max(1, innerBounds.Width - Padding.HorizontalThickness);
        double extentH = GetExtentHeight(wrapWidth);
        double extentW = CanComputeExtentWidth() ? GetExtentWidth() : 0;

        double viewportH = Math.Max(0, innerBounds.Height - Padding.VerticalThickness);
        double viewportW = Math.Max(0, innerBounds.Width - Padding.HorizontalThickness);

        SetVerticalOffset(ClampOffset(VerticalOffset, extentH, viewportH), invalidateVisual: false);
        SetHorizontalOffset(
            CanComputeExtentWidth() ? ClampOffset(HorizontalOffset, extentW, viewportW) : 0,
            invalidateVisual: false);

        bool needV = extentH > viewportH + 0.5;

        // Wrap: a vertical scrollbar reduces wrap width, which may increase the total height.
        if (WrapEnabled && needV)
        {
            double reducedWrapWidth = Math.Max(1, wrapWidth - theme.ScrollBarHitThickness);
            extentH = GetExtentHeight(reducedWrapWidth);
            needV = extentH > viewportH + 0.5;
        }

        bool needH = CanComputeExtentWidth() && extentW > viewportW + 0.5;

        _vBar.IsVisible = needV;
        _hBar.IsVisible = needH;

        const double inset = 0;
        double t = theme.ScrollBarHitThickness;

        if (_vBar.IsVisible)
        {
            _vBar.Minimum = 0;
            _vBar.Maximum = Math.Max(0, extentH - viewportH);
            _vBar.ViewportSize = viewportH;
            _vBar.SmallChange = theme.ScrollBarSmallChange;
            _vBar.LargeChange = theme.ScrollBarLargeChange;
            _vBar.Value = VerticalOffset;
            _vBar.Arrange(new Rect(innerBounds.Right - t - inset, innerBounds.Y + inset, t, Math.Max(0, innerBounds.Height - inset * 2)));
        }

        if (_hBar.IsVisible)
        {
            _hBar.Minimum = 0;
            _hBar.Maximum = Math.Max(0, extentW - viewportW);
            _hBar.ViewportSize = viewportW;
            _hBar.SmallChange = theme.ScrollBarSmallChange;
            _hBar.LargeChange = theme.ScrollBarLargeChange;
            _hBar.Value = HorizontalOffset;
            _hBar.Arrange(new Rect(innerBounds.X + inset, innerBounds.Bottom - t - inset, Math.Max(0, innerBounds.Width - inset * 2), t));
        }
        else
        {
            SetHorizontalOffset(0, invalidateVisual: false);
        }

        ApplyViewAnchorIfPending();
    }

    void IVisualTreeHost.VisitChildren(Action<Element> visitor)
    {
        visitor(_vBar);
        visitor(_hBar);
    }

    protected override void RenderTextContent(IGraphicsContext context, Rect contentBounds, IFont font, Theme theme, in VisualState state)
    {
        RenderText(context, contentBounds, font, theme);
    }

    protected override void RenderAfterContent(IGraphicsContext context, Theme theme, in VisualState state)
    {
        if (_vBar.IsVisible)
        {
            _vBar.Render(context);
        }

        if (_hBar.IsVisible)
        {
            _hBar.Render(context);
        }
    }

    protected override UIElement? HitTestOverride(Point point)
    {
        if (!IsEnabled)
        {
            return null;
        }

        if (_vBar.IsVisible && _vBar.Bounds.Contains(point))
        {
            return _vBar;
        }

        if (_hBar.IsVisible && _hBar.Bounds.Contains(point))
        {
            return _hBar;
        }

        return null;
    }



    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.Handled || !_vBar.IsVisible)
        {
            return;
        }

        int notches = Math.Sign(e.Delta);
        if (notches == 0)
        {
            return;
        }

        var viewportBounds = GetViewportContentBounds();
        double viewportH = viewportBounds.Height;
        double viewportW = viewportBounds.Width;
        SetVerticalOffset(ClampOffset(VerticalOffset - notches * GetTheme().ScrollWheelStep, GetExtentHeight(viewportW), viewportH), invalidateVisual: false);
        _vBar.Value = VerticalOffset;
        InvalidateVisual();
        e.Handled = true;
    }

    // Key handling is centralized in TextBase.

    protected override void MoveCaretToLineEdge(bool start, bool extendSelection)
        => MoveToLineEdge(start, extendSelection);

    protected override void MoveCaretVerticalKey(int deltaLines, bool extendSelection)
        => MoveCaretVertical(deltaLines, extendSelection);

    private void RenderText(IGraphicsContext context, Rect contentBounds, IFont font, Theme theme)
    {
        double lineHeight = GetLineHeight();
        int lineCount = Math.Max(1, _lineStarts.Count);
        var textColor = IsEnabled ? Foreground : theme.Palette.DisabledText;

        if (!WrapEnabled)
        {
            int firstLine = lineHeight <= 0 ? 0 : Math.Max(0, (int)Math.Floor(VerticalOffset / lineHeight));
            double offsetInLine = lineHeight <= 0 ? 0 : VerticalOffset - firstLine * lineHeight;
            double y = contentBounds.Y - offsetInLine;

            int maxLines = lineHeight <= 0 ? lineCount : (int)Math.Ceiling((contentBounds.Height + offsetInLine) / lineHeight) + 1;
            int lastExclusive = Math.Min(lineCount, firstLine + Math.Max(0, maxLines));

            for (int line = firstLine; line < lastExclusive; line++)
            {
                GetLineSpan(line, out int start, out int end);
                string lineText = GetLineText(line, start, end);

                var lineRect = new Rect(contentBounds.X - HorizontalOffset, y, 1_000_000, lineHeight);
                RenderSelectionForRow(context, font, theme, start, 0, lineText, y, contentBounds.X - HorizontalOffset);
                context.DrawText(lineText, lineRect, font, textColor, TextAlignment.Left, TextAlignment.Top, TextWrapping.NoWrap);
                y += lineHeight;
            }

            if (IsFocused)
            {
                DrawCaret(context, contentBounds, font, theme);
            }

            return;
        }

        using var measure = BeginTextMeasurement();
        double wrapWidth = Math.Max(1, contentBounds.Width);

        int firstRow = lineHeight <= 0 ? 0 : Math.Max(0, (int)Math.Floor(VerticalOffset / lineHeight));
        double offsetInRow = lineHeight <= 0 ? 0 : VerticalOffset - firstRow * lineHeight;
        double yRow = contentBounds.Y - offsetInRow;

        _wrapVirtualizer.MapVisualRowToLine(firstRow, wrapWidth, measure.Context, measure.Font, out int lineIndex, out int rowInLine);

        double yWrap = yRow;
        int maxRows = lineHeight <= 0 ? 1 : (int)Math.Ceiling((contentBounds.Height + offsetInRow) / lineHeight) + 1;
        int rendered = 0;

        while (rendered < maxRows && lineIndex < lineCount)
        {
            GetLineSpan(lineIndex, out int lineStart, out int lineEnd);
            string fullLine = GetLineText(lineIndex, lineStart, lineEnd);
            var layout = _wrapVirtualizer.GetWrapLayout(lineIndex, fullLine, wrapWidth, measure.Context, measure.Font);

            for (int row = rowInLine; row < layout.SegmentStarts.Length && rendered < maxRows; row++)
            {
                int segStart = layout.SegmentStarts[row];
                int segEnd = (row + 1 < layout.SegmentStarts.Length) ? layout.SegmentStarts[row + 1] : fullLine.Length;
                ReadOnlySpan<char> rowText = segStart < segEnd ? fullLine.AsSpan(segStart, segEnd - segStart) : ReadOnlySpan<char>.Empty;

                var rowRect = new Rect(contentBounds.X, yWrap, wrapWidth, lineHeight);
                RenderSelectionForRow(context, font, theme, lineStart, segStart, rowText, yWrap, contentBounds.X);
                context.DrawText(rowText, rowRect, font, textColor, TextAlignment.Left, TextAlignment.Top, TextWrapping.NoWrap);
                DrawCaretForWrappedRow(context, contentBounds, font, theme, lineStart, segStart, segEnd, rowText, yWrap);

                yWrap += lineHeight;
                rendered++;
            }

            lineIndex++;
            rowInLine = 0;
        }
    }

    private void DrawCaret(IGraphicsContext context, Rect contentBounds, IFont font, Theme theme)
    {
        if (!IsEnabled)
        {
            return;
        }

        GetLineFromIndex(CaretPosition, out int line, out int lineStart, out int lineEnd);
        double lineHeight = GetLineHeight();
        double y = contentBounds.Y + line * lineHeight - VerticalOffset;

        double x = contentBounds.X - HorizontalOffset + MeasureSubstringWidth(context, font, lineStart, CaretPosition);
        var caretRect = new Rect(x, y, 1, lineHeight);

        if (caretRect.Bottom < contentBounds.Y || caretRect.Y > contentBounds.Bottom)
        {
            return;
        }

        context.FillRectangle(caretRect, theme.Palette.WindowText);
    }

    private void SetCaretFromPointCore(Point p, Rect contentBounds)
    {
        double lineHeight = GetLineHeight();
        if (!WrapEnabled)
        {
            int line = lineHeight <= 0 ? 0 : (int)Math.Floor((p.Y - contentBounds.Y + VerticalOffset) / lineHeight);
            line = Math.Clamp(line, 0, _lineStarts.Count - 1);

            GetLineSpan(line, out int start, out int end);
            double x = p.X - contentBounds.X + HorizontalOffset;

            CaretPosition = GetCharIndexFromXInLine(x, start, end);
            return;
        }

        using var measure = BeginTextMeasurement();
        double wrapWidth = Math.Max(1, contentBounds.Width);
        int row = lineHeight <= 0 ? 0 : (int)Math.Floor((p.Y - contentBounds.Y + VerticalOffset) / lineHeight);
        _wrapVirtualizer.MapVisualRowToLine(row, wrapWidth, measure.Context, measure.Font, out int lineIndex, out int rowInLine, out int lineStartRow);

        GetLineSpan(lineIndex, out int lineStart, out int lineEnd);
        string fullLine = GetLineText(lineIndex, lineStart, lineEnd);
        var layout = _wrapVirtualizer.GetWrapLayout(lineIndex, fullLine, wrapWidth, measure.Context, measure.Font);
        rowInLine = Math.Clamp(rowInLine, 0, layout.SegmentStarts.Length - 1);
        int segStart = layout.SegmentStarts[rowInLine];
        int segEnd = (rowInLine + 1 < layout.SegmentStarts.Length) ? layout.SegmentStarts[rowInLine + 1] : fullLine.Length;
        ReadOnlySpan<char> rowText = segStart < segEnd ? fullLine.AsSpan(segStart, segEnd - segStart) : ReadOnlySpan<char>.Empty;

        double xInRow = p.X - contentBounds.X;
        int col = GetCharIndexFromXInString(xInRow, rowText, measure.Context, measure.Font);
        CaretPosition = lineStart + segStart + col;
    }

    private int GetCharIndexFromXInLine(double x, int start, int end)
    {
        if (start >= end)
        {
            return start;
        }

        using var measure = BeginTextMeasurement();
        var font = measure.Font;

        if (x <= 0)
        {
            return start;
        }

        int lineLength = end - start;
        const int StackAllocThreshold = 512;
        char[]? rented = null;
        Span<char> lineBuffer = lineLength <= StackAllocThreshold
            ? stackalloc char[lineLength]
            : (rented = ArrayPool<char>.Shared.Rent(lineLength)).AsSpan(0, lineLength);
        try
        {
            Document.CopyTo(lineBuffer, start, lineLength);
            ReadOnlySpan<char> line = lineBuffer;

            double total = measure.Context.MeasureText(line, font).Width;
            if (x >= total)
            {
                return end;
            }

            int lo = 0;
            int hi = line.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                double w = measure.Context.MeasureText(line.Slice(0, mid), font).Width;
                if (w < x)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            return start + lo;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private void MoveCaretHorizontal(int delta, bool extendSelection)
    {
        int newPos = Math.Clamp(CaretPosition + delta, 0, GetTextLengthCore());
        SetCaretAndSelection(newPos, extendSelection);
    }

    private void MoveCaretVertical(int deltaLines, bool extendSelection)
    {
        GetLineFromIndex(CaretPosition, out int line, out int lineStart, out int lineEnd);
        int newLine = Math.Clamp(line + deltaLines, 0, _lineStarts.Count - 1);
        if (newLine == line)
        {
            return;
        }

        using var measure = BeginTextMeasurement();
        double x = CaretPosition <= lineStart ? 0 : MeasureSubstringWidth(measure.Context, measure.Font, lineStart, CaretPosition);

        GetLineSpan(newLine, out int ns, out int ne);
        int newPos = GetCharIndexFromXInLine(x, ns, ne);
        SetCaretAndSelection(newPos, extendSelection);
    }

    private void MoveToLineEdge(bool start, bool extendSelection)
    {
        GetLineFromIndex(CaretPosition, out _, out int lineStart, out int lineEnd);
        int newPos = start ? lineStart : lineEnd;
        SetCaretAndSelection(newPos, extendSelection);
    }



    private void EnsureCaretVisible(Rect contentBounds)
    {
        using var measure = BeginTextMeasurement();
        var font = measure.Font;

        GetLineFromIndex(CaretPosition, out int line, out int lineStart, out _);
        double lineHeight = GetLineHeight();

        double caretY;
        double caretX;

        if (!WrapEnabled)
        {
            caretY = line * lineHeight;
            caretX = CaretPosition <= lineStart ? 0 : MeasureSubstringWidth(measure.Context, font, lineStart, CaretPosition);
        }
        else
        {
            double wrapWidth = Math.Max(1, contentBounds.Width);
            GetLineSpan(line, out _, out int lineEnd);
            string fullLine = GetLineText(line, lineStart, lineEnd);
            var layout = _wrapVirtualizer.GetWrapLayout(line, fullLine, wrapWidth, measure.Context, font);
            int caretCol = Math.Clamp(CaretPosition - lineStart, 0, fullLine.Length);
            int caretRow = TextWrapVirtualizer.GetWrapRowFromColumn(layout, caretCol);
            int lineStartRow = _wrapVirtualizer.GetVisualRowStartForLine(line, wrapWidth, measure.Context, font);
            caretY = (lineStartRow + caretRow) * lineHeight;

            int segStart = layout.SegmentStarts[caretRow];
            int rel = Math.Clamp(caretCol - segStart, 0, fullLine.Length - segStart);
            caretX = rel <= 0 ? 0 : measure.Context.MeasureText(fullLine.AsSpan(segStart, rel), font).Width;
        }

        double viewportH = Math.Max(1, contentBounds.Height);
        double viewportW = Math.Max(1, contentBounds.Width);
        double extentH = GetExtentHeight(viewportW);
        double extentW = (CanComputeExtentWidth() && _hBar.IsVisible) ? GetExtentWidth(measure.Context, font) : 0;

        if (caretY < VerticalOffset)
        {
            SetVerticalOffset(caretY, invalidateVisual: false);
        }
        else if (caretY + lineHeight > VerticalOffset + viewportH)
        {
            SetVerticalOffset(caretY + lineHeight - viewportH, invalidateVisual: false);
        }

        if (!WrapEnabled)
        {
            if (caretX < HorizontalOffset)
            {
                SetHorizontalOffset(caretX, invalidateVisual: false);
            }
            else if (caretX > HorizontalOffset + viewportW)
            {
                SetHorizontalOffset(caretX - viewportW, invalidateVisual: false);
            }
        }

        SetVerticalOffset(ClampOffset(VerticalOffset, extentH, viewportH), invalidateVisual: false);
        SetHorizontalOffset((CanComputeExtentWidth() && _hBar.IsVisible) ? ClampOffset(HorizontalOffset, extentW, viewportW) : 0, invalidateVisual: false);

        if (_vBar.IsVisible)
        {
            _vBar.Value = VerticalOffset;
        }

        if (_hBar.IsVisible)
        {
            _hBar.Value = HorizontalOffset;
        }
    }

    private void ClampOffsets(Rect contentBounds)
    {
        double wrapWidth = Math.Max(1, contentBounds.Width);
        SetVerticalOffset(ClampOffset(VerticalOffset, GetExtentHeight(wrapWidth), Math.Max(1, contentBounds.Height)), invalidateVisual: false);
        if (!CanComputeExtentWidth() || !_hBar.IsVisible)
        {
            SetHorizontalOffset(0, invalidateVisual: false);
        }
        else
        {
            SetHorizontalOffset(ClampOffset(HorizontalOffset, GetExtentWidth(), Math.Max(1, contentBounds.Width)), invalidateVisual: false);
        }
        if (_vBar.IsVisible)
        {
            _vBar.Value = VerticalOffset;
        }

        if (_hBar.IsVisible)
        {
            _hBar.Value = HorizontalOffset;
        }
    }

    private double GetExtentHeight(double wrapWidth)
    {
        if (!WrapEnabled)
        {
            return Math.Max(0, _lineStarts.Count * GetLineHeight());
        }

        return _wrapVirtualizer.GetExtentHeight(wrapWidth, GetLineHeight(), FontSize);
    }

    private double GetExtentWidth()
    {
        int version = DocumentVersion;
        var fontKey = new TextLineWidthEstimator.FontKey(FontFamily, FontSize, FontWeight, GetDpi());
        if (_lineWidthEstimator.TryGetCached(version, fontKey, out double cached))
        {
            return cached;
        }

        using var measure = BeginTextMeasurement();
        return _lineWidthEstimator.Compute(measure.Context, measure.Font, version, fontKey);
    }

    private double GetExtentWidth(IGraphicsContext context, IFont font)
    {
        int version = DocumentVersion;
        var fontKey = new TextLineWidthEstimator.FontKey(FontFamily, FontSize, FontWeight, GetDpi());
        if (_lineWidthEstimator.TryGetCached(version, fontKey, out double cached))
        {
            return cached;
        }

        return _lineWidthEstimator.Compute(context, font, version, fontKey);
    }

    private double GetLineHeight() => _lineHeight > 0 ? _lineHeight : Math.Max(16, FontSize * 1.4);

    private void RebuildLineStartsFromDocument()
    {
        _lineStarts.Clear();
        _lineStarts.Add(0);

        for (int i = 0; i < Document.Length; i++)
        {
            if (Document[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }

        if (_lineStarts.Count == 0)
        {
            _lineStarts.Add(0);
        }
    }

    private void EnforceWrapLineLimit()
    {
        if (_lineStarts.Count <= WrapLineCountHardLimit)
        {
            return;
        }

        if (!WrapEnabled)
        {
            return;
        }

        SetWrapEnabled(false);
    }

    private void GetLineSpan(int line, out int start, out int end)
    {
        if (_lineStarts.Count == 0)
        {
            RebuildLineStartsFromDocument();
        }

        line = Math.Clamp(line, 0, _lineStarts.Count - 1);
        start = _lineStarts[line];
        end = line + 1 < _lineStarts.Count ? _lineStarts[line + 1] - 1 : Document.Length;
        if (end < start)
        {
            end = start;
        }

        if (end > start && Document[end - 1] == '\r')
        {
            end--;
        }
    }

    private void GetLineFromIndex(int index, out int line, out int lineStart, out int lineEnd)
    {
        if (_lineStarts.Count == 0)
        {
            RebuildLineStartsFromDocument();
        }

        index = Math.Clamp(index, 0, Document.Length);

        int lo = 0;
        int hi = _lineStarts.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int s = _lineStarts[mid];
            if (s <= index)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        line = Math.Clamp(lo - 1, 0, _lineStarts.Count - 1);
        GetLineSpan(line, out lineStart, out lineEnd);
    }

    private double MeasureSubstringWidth(IGraphicsContext context, IFont font, int start, int end)
    {
        int length = end - start;
        if (length <= 0)
        {
            return 0;
        }

        const int StackAllocThreshold = 512;
        char[]? rented = null;
        Span<char> buffer = length <= StackAllocThreshold
            ? stackalloc char[length]
            : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            Document.CopyTo(buffer, start, length);
            return context.MeasureText(buffer, font).Width;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private void RenderSelectionForRow(
        IGraphicsContext context,
        IFont font,
        Theme theme,
        int lineStart,
        int rowSegmentStart,
        ReadOnlySpan<char> rowText,
        double y,
        double xBase)
    {
        if (!IsFocused || !HasSelection || rowText.IsEmpty)
        {
            return;
        }

        var (selA, selB) = GetSelectionRange();

        int rowStart = lineStart + rowSegmentStart;
        int rowEnd = rowStart + rowText.Length;
        int s = Math.Max(selA, rowStart);
        int t = Math.Min(selB, rowEnd);
        if (s >= t)
        {
            return;
        }

        int relS = s - rowStart;
        int relT = t - rowStart;

        double beforeW = relS <= 0 ? 0 : context.MeasureText(rowText[..relS], font).Width;
        double selW = context.MeasureText(rowText[relS..relT], font).Width;

        context.FillRectangle(new Rect(xBase + beforeW, y, selW, GetLineHeight()), theme.Palette.SelectionBackground);
    }

    private void DrawCaretForWrappedRow(
        IGraphicsContext context,
        Rect contentBounds,
        IFont font,
        Theme theme,
        int lineStart,
        int segStart,
        int segEnd,
        ReadOnlySpan<char> rowText,
        double y)
    {
        if (!IsFocused || !IsEnabled)
        {
            return;
        }

        int caret = CaretPosition;
        int rowStart = lineStart + segStart;
        int rowEnd = lineStart + segEnd;
        if (caret < rowStart || caret > rowEnd)
        {
            return;
        }

        int rel = Math.Clamp(caret - rowStart, 0, rowText.Length);
        double x = contentBounds.X + (rel <= 0 ? 0 : context.MeasureText(rowText[..rel], font).Width);
        context.FillRectangle(new Rect(x, y, 1, GetLineHeight()), theme.Palette.WindowText);
    }

    private static int GetCharIndexFromXInString(double x, ReadOnlySpan<char> text, IGraphicsContext context, IFont font)
    {
        if (text.IsEmpty)
        {
            return 0;
        }

        if (x <= 0)
        {
            return 0;
        }

        double total = context.MeasureText(text, font).Width;
        if (x >= total)
        {
            return text.Length;
        }

        int lo = 0;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            double w = mid <= 0 ? 0 : context.MeasureText(text[..mid], font).Width;
            if (w < x)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    private string GetLineText(int lineIndex, int start, int end)
    {
        if (end <= start)
        {
            return string.Empty;
        }

        // Use larger cache limit for large documents
        int cacheLimit = _lineStarts.Count > LargeDocumentThreshold ? 1024 : 256;
        if (_lineTextCache.Count > cacheLimit)
        {
            _lineTextCache.Clear();
        }

        if (_lineTextCache.TryGetValue(lineIndex, out var cached) &&
            cached.Version == DocumentVersion &&
            cached.Start == start &&
            cached.End == end)
        {
            return cached.Text;
        }

        var text = GetTextSubstringCore(start, end - start);
        _lineTextCache[lineIndex] = new CachedLine(DocumentVersion, start, end, text);
        return text;
    }

    private void ApplyInsert(int index, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _lineTextCache.Clear();
        _wrapVirtualizer.Reset();
        _lineWidthEstimator.Reset();

        index = ApplyInsertCore(index, text.AsSpan());
        UpdateLineStartsOnInsert(index, text);

        CaretPosition = Math.Clamp(CaretPosition, 0, GetTextLengthCore());
    }

    private void ApplyRemove(int index, int length)
    {
        if (length <= 0)
        {
            return;
        }

        _lineTextCache.Clear();
        _wrapVirtualizer.Reset();
        _lineWidthEstimator.Reset();

        int removed = ApplyRemoveCore(index, length);
        if (removed > 0)
        {
            UpdateLineStartsOnRemove(index, removed);
        }

        CaretPosition = Math.Clamp(CaretPosition, 0, GetTextLengthCore());
    }

    private void UpdateLineStartsOnInsert(int index, string insertedText)
    {
        int len = insertedText.Length;
        for (int i = 0; i < _lineStarts.Count; i++)
        {
            if (_lineStarts[i] > index)
            {
                _lineStarts[i] += len;
            }
        }

        int insertPos = LowerBoundLineStart(index + 1);
        for (int i = 0; i < insertedText.Length; i++)
        {
            if (insertedText[i] != '\n')
            {
                continue;
            }

            _lineStarts.Insert(insertPos, index + i + 1);
            insertPos++;
        }
    }

    private void UpdateLineStartsOnRemove(int index, int removedLength)
    {
        int end = index + removedLength;

        for (int i = _lineStarts.Count - 1; i >= 0; i--)
        {
            int s = _lineStarts[i];
            if (s > index && s <= end)
            {
                _lineStarts.RemoveAt(i);
            }
        }

        for (int i = 0; i < _lineStarts.Count; i++)
        {
            if (_lineStarts[i] > end)
            {
                _lineStarts[i] -= removedLength;
            }
        }

        if (_lineStarts.Count == 0)
        {
            _lineStarts.Add(0);
        }
    }

    private int LowerBoundLineStart(int value)
    {
        int lo = 0;
        int hi = _lineStarts.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_lineStarts[mid] < value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    protected override void OnDispose()
    {
        base.OnDispose();
        _vBar.Dispose();
        _hBar.Dispose();
    }

    private readonly record struct CachedLine(int Version, int Start, int End, string Text);
    private readonly record struct WrapLayout(int Version, double Width, int[] SegmentStarts);
    private readonly record struct WrapAnchor(int LineIndex, int StartRow);

    private void CaptureViewAnchor()
    {
        _pendingViewAnchorIndex = -1;
        _pendingViewAnchorYOffset = 0;
        _pendingViewAnchorXOffset = 0;

        double lineHeight = GetLineHeight();
        if (lineHeight <= 0)
        {
            return;
        }

        _pendingViewAnchorYOffset = VerticalOffset - Math.Floor(VerticalOffset / lineHeight) * lineHeight;

        using var measure = BeginTextMeasurement();
        double viewportW = GetViewportContentBounds().Width;
        double wrapWidth = Math.Max(1, viewportW);

        if (WrapEnabled)
        {
            int firstRow = Math.Max(0, (int)Math.Floor(VerticalOffset / lineHeight));
            _wrapVirtualizer.MapVisualRowToLine(firstRow, wrapWidth, measure.Context, measure.Font, out int lineIndex, out int rowInLine);

            GetLineSpan(lineIndex, out int lineStart, out int lineEnd);
            string fullLine = GetLineText(lineIndex, lineStart, lineEnd);
            var layout = _wrapVirtualizer.GetWrapLayout(lineIndex, fullLine, wrapWidth, measure.Context, measure.Font);
            rowInLine = Math.Clamp(rowInLine, 0, Math.Max(0, layout.SegmentStarts.Length - 1));

            int segStart = layout.SegmentStarts.Length == 0 ? 0 : layout.SegmentStarts[rowInLine];
            _pendingViewAnchorIndex = Math.Clamp(lineStart + segStart, 0, GetTextLengthCore());
            _pendingViewAnchorXOffset = 0;
            return;
        }

        int firstLine = Math.Max(0, (int)Math.Floor(VerticalOffset / lineHeight));
        firstLine = Math.Clamp(firstLine, 0, Math.Max(0, _lineStarts.Count - 1));

        GetLineSpan(firstLine, out int start, out int end);
        string lineText = GetLineText(firstLine, start, end);

        int col = GetCharIndexFromXInString(HorizontalOffset, lineText, measure.Context, measure.Font);
        col = Math.Clamp(col, 0, lineText.Length);

        double colWidth = col <= 0 ? 0 : measure.Context.MeasureText(lineText.AsSpan(0, col), measure.Font).Width;
        _pendingViewAnchorXOffset = HorizontalOffset - colWidth;
        _pendingViewAnchorIndex = Math.Clamp(start + col, 0, GetTextLengthCore());
    }

    private void ApplyViewAnchorIfPending()
    {
        if (_pendingViewAnchorIndex < 0)
        {
            return;
        }

        double lineHeight = GetLineHeight();
        if (lineHeight <= 0)
        {
            _pendingViewAnchorIndex = -1;
            return;
        }

        using var measure = BeginTextMeasurement();

        var viewportBounds = GetViewportContentBounds();
        double viewportW = viewportBounds.Width;
        double viewportH = viewportBounds.Height;
        double wrapWidth = Math.Max(1, viewportW);

        GetLineFromIndex(_pendingViewAnchorIndex, out int line, out int lineStart, out int lineEnd);
        int col = Math.Clamp(_pendingViewAnchorIndex - lineStart, 0, Math.Max(0, lineEnd - lineStart));

        if (WrapEnabled)
        {
            string fullLine = GetLineText(line, lineStart, lineEnd);
            var layout = _wrapVirtualizer.GetWrapLayout(line, fullLine, wrapWidth, measure.Context, measure.Font);
            int rowInLine = TextWrapVirtualizer.GetWrapRowFromColumn(layout, col);
            int rowStart = _wrapVirtualizer.GetVisualRowStartForLine(line, wrapWidth, measure.Context, measure.Font);
            SetVerticalOffset((rowStart + rowInLine) * lineHeight + _pendingViewAnchorYOffset, invalidateVisual: false);
            SetHorizontalOffset(0, invalidateVisual: false);
        }
        else
        {
            SetVerticalOffset(line * lineHeight + _pendingViewAnchorYOffset, invalidateVisual: false);

            string lineText = GetLineText(line, lineStart, lineEnd);
            col = Math.Clamp(col, 0, lineText.Length);
            double colWidth = col <= 0 ? 0 : measure.Context.MeasureText(lineText.AsSpan(0, col), measure.Font).Width;
            SetHorizontalOffset(Math.Max(0, colWidth + _pendingViewAnchorXOffset), invalidateVisual: false);
        }

        _pendingViewAnchorIndex = -1;
        _pendingViewAnchorYOffset = 0;
        _pendingViewAnchorXOffset = 0;

        double extentH = GetExtentHeight(wrapWidth);
        SetVerticalOffset(ClampOffset(VerticalOffset, extentH, viewportH), invalidateVisual: false);

        double extentW = CanComputeExtentWidth() ? GetExtentWidth() : 0;
        SetHorizontalOffset(CanComputeExtentWidth() ? ClampOffset(HorizontalOffset, extentW, viewportW) : 0, invalidateVisual: false);

        if (_vBar.IsVisible)
        {
            _vBar.Value = VerticalOffset;
        }

        if (_hBar.IsVisible)
        {
            _hBar.Value = HorizontalOffset;
        }
    }
}
