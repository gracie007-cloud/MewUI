using Aprillz.MewUI.Binding;
using System.Collections.Generic;

using Aprillz.MewUI.Core;
using Aprillz.MewUI.Elements;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.Controls;

public abstract class TextBase : Control
{
    private ValueBinding<string>? _textBinding;
    private bool _suppressBindingSet;
    private readonly TextDocument _document = new();
    private int _documentVersion;
    private string? _cachedText;
    private int _cachedTextVersion = -1;

    protected int _selectionStart;
    protected int _selectionLength;

    private readonly List<Edit> _undo = new();
    private readonly List<Edit> _redo = new();
    private bool _suppressUndoRecording;

    private double _horizontalOffset;
    private double _verticalOffset;

    private bool _suppressTextInputNewline;
    private bool _suppressTextInputTab;

    private protected TextDocument Document => _document;

    protected int DocumentVersion => _documentVersion;

    public string Text
    {
        get => GetTextCore();
        set
        {
            var normalized = NormalizeText(value ?? string.Empty);
            var current = GetTextCore();
            if (current == normalized)
            {
                return;
            }

            var old = current;
            SetTextCore(normalized);
            ClearUndoRedo();

            CaretPosition = Math.Min(CaretPosition, GetTextLengthCore());
            _selectionStart = 0;
            _selectionLength = 0;

            OnTextChanged(old, normalized);
            TextChanged?.Invoke(GetTextCore());
        }
    }

    public string Placeholder
    {
        get;
        set { field = value ?? string.Empty; InvalidateVisual(); }
    } = string.Empty;

    public bool IsReadOnly
    {
        get;
        set { field = value; InvalidateVisual(); }
    }

    public bool AcceptTab { get; set; }

    public bool AcceptReturn { get; set; }

    public int CaretPosition
    {
        get;
        set { field = Math.Clamp(value, 0, GetTextLengthCore()); InvalidateVisual(); }
    }

    public event Action<string>? TextChanged;
    public event Action<bool>? WrapChanged;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public override bool Focusable => true;

    protected double HorizontalOffset => _horizontalOffset;

    protected double VerticalOffset => _verticalOffset;

    protected void SetHorizontalOffset(double value, bool invalidateVisual = true)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = 0;
        }

        if (_horizontalOffset == value)
        {
            return;
        }

        _horizontalOffset = value;
        if (invalidateVisual)
        {
            InvalidateVisual();
        }
    }

    protected void SetVerticalOffset(double value, bool invalidateVisual = true)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = 0;
        }

        if (_verticalOffset == value)
        {
            return;
        }

        _verticalOffset = value;
        if (invalidateVisual)
        {
            InvalidateVisual();
        }
    }

    protected void SetScrollOffsets(double horizontal, double vertical, bool invalidateVisual = true)
    {
        SetHorizontalOffset(horizontal, invalidateVisual: false);
        SetVerticalOffset(vertical, invalidateVisual: false);
        if (invalidateVisual)
        {
            InvalidateVisual();
        }
    }

    protected virtual TextAlignment PlaceholderVerticalAlignment => TextAlignment.Center;

    protected virtual UIElement? HitTestOverride(Point point) => null;

    public override UIElement? HitTest(Point point)
    {
        if (!IsVisible || !IsHitTestVisible)
        {
            return null;
        }

        var hit = HitTestOverride(point);
        if (hit != null)
        {
            return hit;
        }

        return base.HitTest(point);
    }

    protected bool HasSelection => _selectionLength != 0;

    protected (int start, int end) GetSelectionRange()
    {
        if (_selectionLength == 0)
        {
            return (CaretPosition, CaretPosition);
        }

        int a = _selectionStart;
        int b = _selectionStart + _selectionLength;
        return a <= b ? (a, b) : (b, a);
    }

    protected virtual string NormalizeText(string text)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return string.Empty;
        }

        bool needsTabRemoval = !AcceptTab && text.IndexOf('\t') >= 0;

        if (AcceptReturn)
        {
            int firstCr = text.IndexOf('\r');
            if (firstCr < 0 && !needsTabRemoval)
            {
                return text;
            }

            if (firstCr >= 0)
            {
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            }
        }
        else
        {
            bool hasCr = text.IndexOf('\r') >= 0;
            bool hasLf = text.IndexOf('\n') >= 0;
            if (!hasCr && !hasLf && !needsTabRemoval)
            {
                return text;
            }

            if (hasCr || hasLf)
            {
                text = text.Replace("\r\n", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            }
        }

        if (needsTabRemoval)
        {
            text = text.Replace("\t", string.Empty);
        }

        return text;
    }

    protected virtual string NormalizePastedText(string text) => text ?? string.Empty;

    protected virtual Rect GetInteractionContentBounds() => Bounds.Deflate(Padding);

    protected Rect GetTextInnerBounds()
    {
        var bounds = GetSnappedBorderBounds(Bounds);
        var borderInset = GetBorderVisualInset();
        return bounds.Deflate(new Thickness(borderInset));
    }

    protected virtual Rect AdjustViewportBoundsForScrollbars(Rect innerBounds, Theme theme) => innerBounds;

    protected Rect GetViewportInnerBounds()
    {
        var theme = GetTheme();
        var innerBounds = GetTextInnerBounds();
        return AdjustViewportBoundsForScrollbars(innerBounds, theme);
    }

    protected Rect GetViewportContentBounds()
    {
        var viewportBounds = GetViewportInnerBounds();
        return viewportBounds.Deflate(Padding);
    }

    protected abstract void RenderTextContent(IGraphicsContext context, Rect contentBounds, IFont font, Theme theme, in VisualState state);

    protected virtual void RenderAfterContent(IGraphicsContext context, Theme theme, in VisualState state)
    {
    }

    protected sealed override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = GetSnappedBorderBounds(Bounds);
        double radius = theme.ControlCornerRadius;

        var state = GetVisualState();
        var borderColor = PickAccentBorder(theme, BorderBrush, state, hoverMix: 0.6);

        DrawBackgroundAndBorder(
            context,
            bounds,
            state.IsEnabled ? Background : theme.Palette.DisabledControlBackground,
            borderColor,
            radius);

        var contentBounds = GetViewportContentBounds();

        context.Save();
        context.SetClip(contentBounds);

        var font = GetFont();

        if (Document.IsEmpty && !string.IsNullOrEmpty(Placeholder) && !state.IsFocused)
        {
            context.DrawText(Placeholder, contentBounds, font, theme.Palette.PlaceholderText,
                TextAlignment.Left, PlaceholderVerticalAlignment, TextWrapping.NoWrap);
        }
        else
        {
            RenderTextContent(context, contentBounds, font, theme, state);
        }

        context.Restore();

        RenderAfterContent(context, theme, state);
    }

    protected abstract void SetCaretFromPoint(Point point, Rect contentBounds);

    protected virtual void AutoScrollForSelectionDrag(Point point, Rect contentBounds)
    {
    }

    protected virtual void EnsureCaretVisibleCore(Rect contentBounds)
    {
    }

    protected virtual void MoveCaretHorizontalKey(int direction, bool extendSelection, bool word)
        => MoveCaretHorizontal(direction, extendSelection, word);

    protected virtual void MoveCaretVerticalKey(int deltaLines, bool extendSelection)
    {
    }

    protected virtual string GetTextCore()
    {
        if (_cachedTextVersion == _documentVersion && _cachedText != null)
        {
            return _cachedText;
        }

        _cachedText = _document.GetText();
        _cachedTextVersion = _documentVersion;
        return _cachedText;
    }

    protected virtual void SetTextCore(string normalizedText)
    {
        BumpDocumentVersion();
        _document.SetText(normalizedText ?? string.Empty);
    }

    protected virtual int GetTextLengthCore() => _document.Length;

    protected virtual char GetTextCharCore(int index) => _document[index];

    protected virtual string GetTextSubstringCore(int start, int length) => _document.GetText(start, length);

    protected virtual void OnTextChanged(string oldText, string newText)
    {
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.ControlKey)
        {
            switch (e.Key)
            {
                case Key.Home:
                    MoveCaretToDocumentEdge(start: true, extendSelection: e.ShiftKey);
                    e.Handled = true;
                    EnsureCaretVisibleCore(GetInteractionContentBounds());
                    InvalidateVisual();
                    return;

                case Key.End:
                    MoveCaretToDocumentEdge(start: false, extendSelection: e.ShiftKey);
                    e.Handled = true;
                    EnsureCaretVisibleCore(GetInteractionContentBounds());
                    InvalidateVisual();
                    return;

                case Key.Z:
                    if (!IsReadOnly)
                    {
                        if (e.ShiftKey)
                        {
                            Redo();
                        }
                        else
                        {
                            Undo();
                        }

                        EnsureCaretVisibleCore(GetInteractionContentBounds());
                        InvalidateVisual();
                    }

                    e.Handled = true;
                    return;

                case Key.Y:
                    if (!IsReadOnly)
                    {
                        Redo();
                        EnsureCaretVisibleCore(GetInteractionContentBounds());
                        InvalidateVisual();
                    }

                    e.Handled = true;
                    return;

                case Key.A:
                    SelectAllCore();
                    e.Handled = true;
                    return;

                case Key.C:
                    CopyToClipboardCore();
                    e.Handled = true;
                    return;

                case Key.X:
                    if (!IsReadOnly)
                    {
                        CutToClipboardCore();
                        EnsureCaretVisibleCore(GetInteractionContentBounds());
                        InvalidateVisual();
                    }

                    e.Handled = true;
                    return;

                case Key.V:
                    if (!IsReadOnly)
                    {
                        PasteFromClipboardCore();
                        EnsureCaretVisibleCore(GetInteractionContentBounds());
                        InvalidateVisual();
                    }

                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case Key.Home:
                MoveCaretToLineEdge(start: true, extendSelection: e.ShiftKey);
                e.Handled = true;
                return;

            case Key.End:
                MoveCaretToLineEdge(start: false, extendSelection: e.ShiftKey);
                e.Handled = true;
                return;

            case Key.Left:
                MoveCaretHorizontalKey(-1, extendSelection: e.ShiftKey, word: e.ControlKey);
                e.Handled = true;
                break;

            case Key.Right:
                MoveCaretHorizontalKey(1, extendSelection: e.ShiftKey, word: e.ControlKey);
                e.Handled = true;
                break;

            case Key.Up:
                MoveCaretVerticalKey(-1, extendSelection: e.ShiftKey);
                e.Handled = true;
                break;

            case Key.Down:
                MoveCaretVerticalKey(1, extendSelection: e.ShiftKey);
                e.Handled = true;
                break;

            case Key.Backspace:
                if (!IsReadOnly)
                {
                    BackspaceForEdit(e.ControlKey);
                }

                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    DeleteForEdit(e.ControlKey);
                }

                e.Handled = true;
                break;

            case Key.Tab:
                if (!IsReadOnly && AcceptTab)
                {
                    InsertTextAtCaretForEdit("\t");
                    _suppressTextInputTab = true;
                    e.Handled = true;
                }

                break;

            case Key.Enter:
                if (!IsReadOnly && AcceptReturn)
                {
                    InsertTextAtCaretForEdit("\n");
                    _suppressTextInputNewline = true;
                    e.Handled = true;
                }

                break;
        }

        if (e.Handled)
        {
            EnsureCaretVisibleCore(GetInteractionContentBounds());
            InvalidateVisual();
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || IsReadOnly)
        {
            return;
        }

        var text = e.Text ?? string.Empty;

        if (_suppressTextInputNewline)
        {
            _suppressTextInputNewline = false;
            if (text.Contains('\r') || text.Contains('\n'))
            {
                e.Handled = true;
                return;
            }
        }

        if (_suppressTextInputTab)
        {
            _suppressTextInputTab = false;
            if (text.Contains('\t'))
            {
                e.Handled = true;
                return;
            }
        }

        text = NormalizeText(text);
        if (text.Length == 0)
        {
            return;
        }

        InsertTextAtCaretForEdit(text);
        e.Handled = true;

        EnsureCaretVisibleCore(GetInteractionContentBounds());
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!IsEnabled || e.Button != MouseButton.Left)
        {
            return;
        }

        Focus();

        var contentBounds = GetInteractionContentBounds();
        SetCaretFromPoint(e.Position, contentBounds);
        _selectionStart = CaretPosition;
        _selectionLength = 0;

        var root = FindVisualRoot();
        if (root is Window window)
        {
            window.CaptureMouse(this);
        }

        EnsureCaretVisibleCore(contentBounds);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!IsEnabled || !IsMouseCaptured || !e.LeftButton)
        {
            return;
        }

        var contentBounds = GetInteractionContentBounds();
        AutoScrollForSelectionDrag(e.Position, contentBounds);
        SetCaretFromPoint(e.Position, contentBounds);

        _selectionLength = CaretPosition - _selectionStart;
        EnsureCaretVisibleCore(contentBounds);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButton.Left)
        {
            return;
        }

        var root = FindVisualRoot();
        if (root is Window window)
        {
            window.ReleaseMouseCapture();
        }
    }

    public void SetTextBinding(
        Func<string> get,
        Action<string> set,
        Action<Action>? subscribe = null,
        Action<Action>? unsubscribe = null)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);

        _textBinding?.Dispose();
        _textBinding = new ValueBinding<string>(
            get,
            set,
            subscribe,
            unsubscribe,
            onSourceChanged: () =>
            {
                if (IsFocused)
                {
                    return;
                }

                var value = NormalizeText(get() ?? string.Empty);
                if (GetTextCore() == value)
                {
                    return;
                }

                _suppressBindingSet = true;
                try { Text = value; }
                finally { _suppressBindingSet = false; }
            });

        // Ensure the binding forwarder is registered once (no duplicates), without tracking extra state.
        TextChanged -= ForwardTextChangedToBinding;
        TextChanged += ForwardTextChangedToBinding;

        _suppressBindingSet = true;
        try { Text = NormalizeText(get() ?? string.Empty); }
        finally { _suppressBindingSet = false; }
    }

    protected override void OnDispose()
    {
        _document.Dispose();
        TextChanged -= ForwardTextChangedToBinding;
        _textBinding?.Dispose();
        _textBinding = null;
    }

    protected void NotifyTextChanged()
    {
        TextChanged?.Invoke(GetTextCore());
    }

    protected void NotifyWrapChanged(bool value)
    {
        WrapChanged?.Invoke(value);
    }

    private void ForwardTextChangedToBinding(string text)
    {
        if (_suppressBindingSet)
        {
            return;
        }

        _textBinding?.Set(text);
    }

    public void Undo()
    {
        if (IsReadOnly || _undo.Count == 0)
        {
            return;
        }

        var edit = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        _suppressUndoRecording = true;
        try
        {
            ApplyInverseEdit(edit);
        }
        finally
        {
            _suppressUndoRecording = false;
        }

        _redo.Add(edit);
    }

    public void Redo()
    {
        if (IsReadOnly || _redo.Count == 0)
        {
            return;
        }

        var edit = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        _suppressUndoRecording = true;
        try
        {
            ApplyEdit(edit);
        }
        finally
        {
            _suppressUndoRecording = false;
        }

        _undo.Add(edit);
    }

    private void ApplyInverseEdit(Edit edit)
    {
        if (edit.Kind == EditKind.Insert)
        {
            ApplyRemoveForEdit(edit.Index, edit.Text.Length);
            SetCaretAndSelection(edit.Index, extendSelection: false);
        }
        else
        {
            ApplyInsertForEdit(edit.Index, edit.Text);
            SetCaretAndSelection(edit.Index + edit.Text.Length, extendSelection: false);
        }

        _selectionStart = CaretPosition;
        _selectionLength = 0;
        OnEditCommitted();
    }

    private void ApplyEdit(Edit edit)
    {
        if (edit.Kind == EditKind.Insert)
        {
            ApplyInsertForEdit(edit.Index, edit.Text);
            SetCaretAndSelection(edit.Index + edit.Text.Length, extendSelection: false);
        }
        else
        {
            ApplyRemoveForEdit(edit.Index, edit.Text.Length);
            SetCaretAndSelection(edit.Index, extendSelection: false);
        }

        _selectionStart = CaretPosition;
        _selectionLength = 0;
        OnEditCommitted();
    }

    private void ClearUndoRedo()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void RecordEdit(Edit edit)
    {
        if (_suppressUndoRecording)
        {
            return;
        }

        _undo.Add(edit);
        _redo.Clear();
    }

    protected void BumpDocumentVersion()
    {
        _documentVersion++;
        _cachedTextVersion = -1;
        _cachedText = null;
    }

    protected void InsertIntoDocument(int index, ReadOnlySpan<char> text)
    {
        if (text.Length == 0)
        {
            return;
        }

        BumpDocumentVersion();
        _document.Insert(index, text);
    }

    protected void RemoveFromDocument(int index, int length)
    {
        if (length <= 0)
        {
            return;
        }

        BumpDocumentVersion();
        _document.Remove(index, length);
    }

    protected int ApplyInsertCore(int index, ReadOnlySpan<char> text)
    {
        if (text.Length == 0)
        {
            return index;
        }

        int max = GetTextLengthCore();
        index = Math.Clamp(index, 0, max);
        InsertIntoDocument(index, text);
        return index;
    }

    protected int ApplyRemoveCore(int index, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        int max = GetTextLengthCore();
        index = Math.Clamp(index, 0, max);
        length = Math.Min(length, max - index);
        if (length <= 0)
        {
            return 0;
        }

        RemoveFromDocument(index, length);
        return length;
    }

    protected virtual void SelectAllCore()
    {
        _selectionStart = 0;
        _selectionLength = GetTextLengthCore();
        CaretPosition = GetTextLengthCore();
        InvalidateVisual();
    }

    protected virtual void CopyToClipboardCore()
    {
        if (!HasSelection)
        {
            return;
        }

        var (start, end) = GetSelectionRange();
        string selected = GetTextSubstringCore(start, end - start);
        TryClipboardSetText(selected);
    }

    protected virtual void CutToClipboardCore()
    {
        if (_selectionLength == 0)
        {
            return;
        }

        CopyToClipboardCore();
        DeleteSelectionForEdit();
    }

    protected virtual void PasteFromClipboardCore()
    {
        if (!TryClipboardGetText(out var text) || string.IsNullOrEmpty(text))
        {
            return;
        }

        InsertTextAtCaretForEdit(NormalizePastedText(text));
    }

    protected virtual void ApplyInsertForEdit(int index, string text) => InsertIntoDocument(index, text.AsSpan());

    protected virtual void ApplyRemoveForEdit(int index, int length) => RemoveFromDocument(index, length);

    protected virtual void OnEditCommitted() => NotifyTextChanged();

    protected void SetCaretAndSelection(int newPos, bool extendSelection)
    {
        if (!extendSelection)
        {
            CaretPosition = newPos;
            _selectionStart = newPos;
            _selectionLength = 0;
            return;
        }

        if (_selectionLength == 0)
        {
            _selectionStart = CaretPosition;
        }

        CaretPosition = newPos;
        _selectionLength = CaretPosition - _selectionStart;
    }

    protected void MoveCaretHorizontal(int direction, bool extendSelection, bool word)
    {
        int length = GetTextLengthCore();
        if (length == 0)
        {
            SetCaretAndSelection(0, extendSelection);
            return;
        }

        int newPos;
        if (word)
        {
            newPos = direction < 0 ? FindPreviousWordBoundary(CaretPosition) : FindNextWordBoundary(CaretPosition);
        }
        else
        {
            newPos = Math.Clamp(CaretPosition + direction, 0, length);
        }

        SetCaretAndSelection(newPos, extendSelection);
    }

    protected void MoveCaretToDocumentEdge(bool start, bool extendSelection)
    {
        int newPos = start ? 0 : GetTextLengthCore();
        SetCaretAndSelection(newPos, extendSelection);
    }

    protected virtual void MoveCaretToLineEdge(bool start, bool extendSelection)
        => MoveCaretToDocumentEdge(start, extendSelection);

    protected void BackspaceForEdit(bool word)
    {
        if (DeleteSelectionForEdit())
        {
            return;
        }

        if (CaretPosition <= 0)
        {
            return;
        }

        int deleteFrom = word ? FindPreviousWordBoundary(CaretPosition) : CaretPosition - 1;
        int deleteLen = CaretPosition - deleteFrom;
        if (deleteLen <= 0)
        {
            return;
        }

        string deleted = GetTextSubstringCore(deleteFrom, deleteLen);
        ApplyRemoveForEdit(deleteFrom, deleteLen);
        RecordEdit(new Edit(EditKind.Delete, deleteFrom, deleted));
        SetCaretAndSelection(deleteFrom, extendSelection: false);
        OnEditCommitted();
    }

    protected void DeleteForEdit(bool word)
    {
        if (DeleteSelectionForEdit())
        {
            return;
        }

        int length = GetTextLengthCore();
        if (CaretPosition >= length)
        {
            return;
        }

        int deleteTo = word ? FindNextWordBoundary(CaretPosition) : CaretPosition + 1;
        deleteTo = Math.Clamp(deleteTo, CaretPosition, length);

        int deleteLen = deleteTo - CaretPosition;
        if (deleteLen <= 0)
        {
            return;
        }

        string deleted = GetTextSubstringCore(CaretPosition, deleteLen);
        ApplyRemoveForEdit(CaretPosition, deleteLen);
        RecordEdit(new Edit(EditKind.Delete, CaretPosition, deleted));
        SetCaretAndSelection(CaretPosition, extendSelection: false);
        OnEditCommitted();
    }

    protected int FindPreviousWordBoundary(int from)
    {
        if (from <= 0)
        {
            return 0;
        }

        int pos = Math.Min(from - 1, GetTextLengthCore() - 1);
        while (pos > 0 && char.IsWhiteSpace(GetTextCharCore(pos)))
        {
            pos--;
        }

        while (pos > 0 && !char.IsWhiteSpace(GetTextCharCore(pos - 1)))
        {
            pos--;
        }

        return pos;
    }

    protected int FindNextWordBoundary(int from)
    {
        int length = GetTextLengthCore();
        if (from >= length)
        {
            return length;
        }

        int pos = from;
        while (pos < length && !char.IsWhiteSpace(GetTextCharCore(pos)))
        {
            pos++;
        }

        while (pos < length && char.IsWhiteSpace(GetTextCharCore(pos)))
        {
            pos++;
        }

        return pos;
    }

    protected virtual bool DeleteSelectionForEdit()
    {
        if (!HasSelection)
        {
            return false;
        }

        var (start, end) = GetSelectionRange();
        int length = end - start;
        string deleted = GetTextSubstringCore(start, length);

        ApplyRemoveForEdit(start, length);
        RecordEdit(new Edit(EditKind.Delete, start, deleted));
        CaretPosition = start;
        _selectionStart = start;
        _selectionLength = 0;
        OnEditCommitted();
        return true;
    }

    protected virtual void InsertTextAtCaretForEdit(string text)
    {
        text = NormalizeText(text ?? string.Empty);
        if (text.Length == 0)
        {
            return;
        }

        DeleteSelectionForEdit();

        ApplyInsertForEdit(CaretPosition, text);
        RecordEdit(new Edit(EditKind.Insert, CaretPosition, text));
        CaretPosition += text.Length;
        _selectionStart = CaretPosition;
        _selectionLength = 0;
        OnEditCommitted();
    }

    protected bool TryClipboardSetText(string text)
    {
        if (!Application.IsRunning)
        {
            return false;
        }

        var clipboard = Application.Current.PlatformHost.Clipboard;
        return clipboard.TrySetText(text ?? string.Empty);
    }

    protected bool TryClipboardGetText(out string text)
    {
        text = string.Empty;
        if (!Application.IsRunning)
        {
            return false;
        }

        var clipboard = Application.Current.PlatformHost.Clipboard;
        return clipboard.TryGetText(out text);
    }

    protected virtual bool SupportsWrap => false;

    protected bool WrapEnabled
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            var old = field;
            field = value;
            OnWrapChanged(old, value);
            WrapChanged?.Invoke(value);
        }
    }

    protected void SetWrapEnabled(bool value)
    {
        if (!SupportsWrap)
        {
            WrapEnabled = false;
            return;
        }

        WrapEnabled = value;
    }

    protected virtual void OnWrapChanged(bool oldValue, bool newValue)
    {
    }

    protected static double ClampOffset(double value, double extent, double viewport)
    {
        extent = Math.Max(0, extent);
        viewport = Math.Max(0, viewport);
        double max = Math.Max(0, extent - viewport);
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        if (value < 0)
        {
            return 0;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private enum EditKind { Insert, Delete }

    private readonly record struct Edit(EditKind Kind, int Index, string Text);
}
