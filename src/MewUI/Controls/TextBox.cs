using Aprillz.MewUI.Core;
using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

/// <summary>
/// A single-line text input control.
/// </summary>
public class TextBox : Control, IDisposable
{
    private int _selectionStart;
    private int _selectionLength;
    private double _scrollOffset;
    private ValueBinding<string>? _textBinding;
    private bool _suppressBindingSet;
    private bool _disposed;

    public TextBox()
    {
        var theme = Theme.Current;
        Background = theme.ControlBackground;
        BorderBrush = theme.ControlBorder;
        BorderThickness = 1; 
        Padding = new Thickness(4, 2, 4, 2);
    }

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        if (Background == oldTheme.ControlBackground)
            Background = newTheme.ControlBackground;
        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;
        base.OnThemeChanged(oldTheme, newTheme);
    }

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    public string Text
    {
        get;
        set
        {
            if (field != value)
            {
                field = value ?? string.Empty;
                CaretPosition = Math.Min(CaretPosition, Text.Length);
                _selectionStart = 0;
                _selectionLength = 0;
                TextChanged?.Invoke(Text);
                InvalidateVisual();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder text shown when empty.
    /// </summary>
    public string Placeholder
    {
        get;
        set { field = value ?? string.Empty; InvalidateVisual(); }
    } = string.Empty;

    /// <summary>
    /// Gets or sets whether the text box is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get;
        set { field = value; InvalidateVisual(); }
    }

    /// <summary>
    /// Gets or sets the caret position.
    /// </summary>
    public int CaretPosition
    {
        get;
        set { field = Math.Clamp(value, 0, Text.Length); InvalidateVisual(); }
    }

    /// <summary>
    /// Text changed event (AOT-compatible).
    /// </summary>
    public Action<string>? TextChanged { get; set; }

    public override bool Focusable => true;

    protected override Size MeasureContent(Size availableSize)
    {
        // Default size for empty text box
        var minHeight = FontSize + Padding.VerticalThickness + 4;
        return new Size(100, minHeight);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = Bounds;
        var contentBounds = bounds.Deflate(Padding);
        double radius = theme.ControlCornerRadius;

        // Draw background
        if (radius > 0)
            context.FillRoundedRectangle(bounds, radius, radius, IsEnabled ? Background : theme.TextBoxDisabledBackground);
        else
            context.FillRectangle(bounds, IsEnabled ? Background : theme.TextBoxDisabledBackground);

        // Draw border
        var borderColor = BorderBrush;
        if (IsEnabled)
        {
            if (IsFocused)
                borderColor = theme.Accent;
            else if (IsMouseOver)
                borderColor = BorderBrush.Lerp(theme.Accent, 0.6);
        }
        if (BorderThickness > 0)
        {
            if (radius > 0)
                context.DrawRoundedRectangle(bounds, radius, radius, borderColor, BorderThickness);
            else
                context.DrawRectangle(bounds, borderColor, BorderThickness);
        }

        // Set up clipping for content
        context.Save();
        context.SetClip(contentBounds);

        var font = GetFont();

        // Draw placeholder or text
        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder) && !IsFocused)
        {
            context.DrawText(Placeholder, contentBounds, font, theme.PlaceholderText,
                TextAlignment.Left, TextAlignment.Center, TextWrapping.NoWrap);
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            // Calculate text position with scroll offset
            var textX = contentBounds.X - _scrollOffset;

            // Draw selection background if any
            if (_selectionLength > 0 && IsFocused)
            {
                var selStart = Math.Min(_selectionStart, _selectionStart + _selectionLength);
                var selEnd = Math.Max(_selectionStart, _selectionStart + _selectionLength);

                var beforeSel = Text[..selStart];
                var selection = Text[selStart..selEnd];

                var beforeWidth = context.MeasureText(beforeSel, font).Width;
                var selWidth = context.MeasureText(selection, font).Width;

                var selRect = new Rect(textX + beforeWidth, contentBounds.Y,
                    selWidth, contentBounds.Height);
                context.FillRectangle(selRect, theme.Accent);
            }

            // Draw text
            var textColor = IsEnabled ? Foreground : theme.DisabledText;
            // Use backend vertical centering (font metrics differ from FontSize across renderers).
            context.DrawText(Text, new Rect(textX, contentBounds.Y, 1_000_000, contentBounds.Height), font, textColor,
                TextAlignment.Left, TextAlignment.Center, TextWrapping.NoWrap);
        }

        // Draw caret if focused
        if (IsFocused && !IsReadOnly)
        {
            var caretX = contentBounds.X - _scrollOffset;
            if (CaretPosition > 0)
            {
                var textBefore = Text[..CaretPosition];
                caretX += context.MeasureText(textBefore, font).Width;
            }

            // Simple caret - could be animated
            context.DrawLine(
                new Point(caretX, contentBounds.Y + 2),
                new Point(caretX, contentBounds.Bottom - 2),
                theme.WindowText, 1);
        }

        context.Restore();
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButton.Left)
        {
            Focus();

            // Calculate caret position from click
            var contentBounds = Bounds.Deflate(Padding);
            var clickX = e.Position.X - contentBounds.X + _scrollOffset;

            CaretPosition = GetCharacterIndexFromX(clickX);
            _selectionStart = CaretPosition;
            _selectionLength = 0;

            // Capture for selection
            var root = FindVisualRoot();
            if (root is Window window)
                window.CaptureMouse(this);

            InvalidateVisual();
            e.Handled = true;
        }
    }

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsMouseCaptured && e.LeftButton)
        {
            // Update selection
            var contentBounds = Bounds.Deflate(Padding);
            var clickX = e.Position.X - contentBounds.X + _scrollOffset;

            var newPos = GetCharacterIndexFromX(clickX);
            _selectionLength = newPos - _selectionStart;
            CaretPosition = newPos;

            InvalidateVisual();
            e.Handled = true;
        }
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButton.Left)
        {
            var root = FindVisualRoot();
            if (root is Window window)
                window.ReleaseMouseCapture();
        }
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsReadOnly && !IsNavigationKey(e.Key)) return;

        bool shift = e.ShiftKey;
        bool ctrl = e.ControlKey;

        switch (e.Key)
        {
            case Native.Constants.VirtualKeys.VK_LEFT:
                MoveCaret(-1, shift, ctrl);
                e.Handled = true;
                break;

            case Native.Constants.VirtualKeys.VK_RIGHT:
                MoveCaret(1, shift, ctrl);
                e.Handled = true;
                break;

            case Native.Constants.VirtualKeys.VK_HOME:
                MoveCaret(-Text.Length, shift, false);
                e.Handled = true;
                break;

            case Native.Constants.VirtualKeys.VK_END:
                MoveCaret(Text.Length, shift, false);
                e.Handled = true;
                break;

            case Native.Constants.VirtualKeys.VK_BACK:
                if (!IsReadOnly) HandleBackspace(ctrl);
                e.Handled = true;
                break;

            case Native.Constants.VirtualKeys.VK_DELETE:
                if (!IsReadOnly) HandleDelete(ctrl);
                e.Handled = true;
                break;

            case 'A' when ctrl:
                SelectAll();
                e.Handled = true;
                break;

            case 'C' when ctrl:
                CopyToClipboard();
                e.Handled = true;
                break;

            case 'X' when ctrl:
                if (!IsReadOnly) CutToClipboard();
                e.Handled = true;
                break;

            case 'V' when ctrl:
                if (!IsReadOnly) PasteFromClipboard();
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    internal override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (IsReadOnly) return;

        // Delete selection if any
        DeleteSelection();

        // Insert text
        Text = Text.Insert(CaretPosition, e.Text);
        CaretPosition += e.Text.Length;
        TextChanged?.Invoke(Text);

        EnsureCaretVisible();
        InvalidateVisual();
        e.Handled = true;
    }

    private int GetCharacterIndexFromX(double x)
    {
        if (string.IsNullOrEmpty(Text)) return 0;

        using var measure = BeginTextMeasurement();

        double prevWidth = 0;
        for (int i = 0; i <= Text.Length; i++)
        {
            var width = i > 0 ? measure.Context.MeasureText(Text[..i], measure.Font).Width : 0;
            if (x < (prevWidth + width) / 2)
                return Math.Max(0, i - 1);
            prevWidth = width;
        }
        return Text.Length;
    }

    private void MoveCaret(int direction, bool extend, bool word)
    {
        int newPos = CaretPosition;

        if (word)
        {
            // Move by word
            if (direction < 0)
            {
                newPos = FindPreviousWordBoundary(CaretPosition);
            }
            else
            {
                newPos = FindNextWordBoundary(CaretPosition);
            }
        }
        else
        {
            newPos = Math.Clamp(CaretPosition + direction, 0, Text.Length);
        }

        if (extend)
        {
            _selectionLength += newPos - CaretPosition;
        }
        else
        {
            _selectionStart = newPos;
            _selectionLength = 0;
        }

        CaretPosition = newPos;
        EnsureCaretVisible();
    }

    private int FindPreviousWordBoundary(int from)
    {
        if (from <= 0) return 0;
        int pos = from - 1;
        while (pos > 0 && char.IsWhiteSpace(Text[pos])) pos--;
        while (pos > 0 && !char.IsWhiteSpace(Text[pos - 1])) pos--;
        return pos;
    }

    private int FindNextWordBoundary(int from)
    {
        if (from >= Text.Length) return Text.Length;
        int pos = from;
        while (pos < Text.Length && !char.IsWhiteSpace(Text[pos])) pos++;
        while (pos < Text.Length && char.IsWhiteSpace(Text[pos])) pos++;
        return pos;
    }

    private void HandleBackspace(bool word)
    {
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }
        else if (CaretPosition > 0)
        {
            int deleteFrom = word ? FindPreviousWordBoundary(CaretPosition) : CaretPosition - 1;
            Text = Text.Remove(deleteFrom, CaretPosition - deleteFrom);
            CaretPosition = deleteFrom;
            TextChanged?.Invoke(Text);
        }
    }

    private void HandleDelete(bool word)
    {
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }
        else if (CaretPosition < Text.Length)
        {
            int deleteTo = word ? FindNextWordBoundary(CaretPosition) : CaretPosition + 1;
            Text = Text.Remove(CaretPosition, deleteTo - CaretPosition);
            TextChanged?.Invoke(Text);
        }
    }

    private void DeleteSelection()
    {
        if (_selectionLength == 0) return;

        int start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        int length = Math.Abs(_selectionLength);

        Text = Text.Remove(start, length);
        CaretPosition = start;
        _selectionStart = start;
        _selectionLength = 0;
        TextChanged?.Invoke(Text);
    }

    private void SelectAll()
    {
        _selectionStart = 0;
        _selectionLength = Text.Length;
        CaretPosition = Text.Length;
    }

    private void CopyToClipboard()
    {
        if (_selectionLength == 0) return;

        int start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        int length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        SetClipboardText(selectedText);
    }

    private void CutToClipboard()
    {
        CopyToClipboard();
        DeleteSelection();
    }

    private void PasteFromClipboard()
    {
        var text = GetClipboardText();
        if (string.IsNullOrEmpty(text)) return;

        // Remove newlines for single-line textbox
        text = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

        DeleteSelection();
        Text = Text.Insert(CaretPosition, text);
        CaretPosition += text.Length;
        TextChanged?.Invoke(Text);
    }

    private void EnsureCaretVisible()
    {
        var contentBounds = Bounds.Deflate(Padding);
        using var measure = BeginTextMeasurement();

        var caretX = CaretPosition > 0
            ? measure.Context.MeasureText(Text[..CaretPosition], measure.Font).Width
            : 0;

        if (caretX - _scrollOffset > contentBounds.Width - 5)
        {
            _scrollOffset = caretX - contentBounds.Width + 10;
        }
        else if (caretX - _scrollOffset < 5)
        {
            _scrollOffset = Math.Max(0, caretX - 10);
        }
    }

    private static bool IsNavigationKey(int key) =>
        key is Native.Constants.VirtualKeys.VK_LEFT or
               Native.Constants.VirtualKeys.VK_RIGHT or
               Native.Constants.VirtualKeys.VK_HOME or
               Native.Constants.VirtualKeys.VK_END;

    private static void SetClipboardText(string text)
    {
        if (!Native.User32.OpenClipboard(0)) return;
        try
        {
            Native.User32.EmptyClipboard();
            var bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
            var hGlobal = Native.Kernel32.GlobalAlloc(0x0042, (nuint)bytes.Length); // GMEM_MOVEABLE | GMEM_ZEROINIT
            if (hGlobal != 0)
            {
                var ptr = Native.Kernel32.GlobalLock(hGlobal);
                if (ptr != 0)
                {
                    System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    Native.Kernel32.GlobalUnlock(hGlobal);
                    Native.User32.SetClipboardData(13, hGlobal); // CF_UNICODETEXT
                }
            }
        }
        finally
        {
            Native.User32.CloseClipboard();
        }
    }

    private static string GetClipboardText()
    {
        if (!Native.User32.IsClipboardFormatAvailable(13)) return string.Empty; // CF_UNICODETEXT
        if (!Native.User32.OpenClipboard(0)) return string.Empty;
        try
        {
            var hGlobal = Native.User32.GetClipboardData(13);
            if (hGlobal == 0) return string.Empty;

            var ptr = Native.Kernel32.GlobalLock(hGlobal);
            if (ptr == 0) return string.Empty;

            try
            {
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
            }
            finally
            {
                Native.Kernel32.GlobalUnlock(hGlobal);
            }
        }
        finally
        {
            Native.User32.CloseClipboard();
        }
    }

    public TextBox BindText(
        Func<string> get,
        Action<string> set,
        Action<Action>? subscribe = null,
        Action<Action>? unsubscribe = null)
    {
        _textBinding?.Dispose();
        _textBinding = new ValueBinding<string>(
            get,
            set,
            subscribe,
            unsubscribe,
            onSourceChanged: () =>
            {
                if (IsFocused)
                    return;

                var value = get() ?? string.Empty;
                if (Text == value)
                    return;

                _suppressBindingSet = true;
                try { Text = value; }
                finally { _suppressBindingSet = false; }
            });

        var existing = TextChanged;
        TextChanged = text =>
        {
            existing?.Invoke(text);

            if (_suppressBindingSet)
                return;

            _textBinding?.Set(text);
        };

        _suppressBindingSet = true;
        try { Text = get() ?? string.Empty; }
        finally { _suppressBindingSet = false; }

        return this;
    }

    public TextBox BindText(ObservableValue<string> source)
        => BindText(() => source.Value, v => source.Value = v, h => source.Changed += h, h => source.Changed -= h);

    public void Dispose()
    {
        if (_disposed)
            return;

        _textBinding?.Dispose();
        _textBinding = null;
        _disposed = true;
    }
}
