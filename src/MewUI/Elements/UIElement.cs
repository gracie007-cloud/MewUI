using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Elements;

/// <summary>
/// Base class for elements that support input handling and visibility.
/// </summary>
public abstract class UIElement : Element
{
    private List<IDisposable>? _bindings;
    private ValueBinding<bool>? _isVisibleBinding;
    private ValueBinding<bool>? _isEnabledBinding;
    /// <summary>
    /// Gets or sets whether the element is visible.
    /// </summary>
    public bool IsVisible
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateMeasure();
                OnVisibilityChanged();
            }
        }
    } = true;

    /// <summary>
    /// Gets or sets whether the element is enabled for input.
    /// </summary>
    public bool IsEnabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateVisual();
                OnEnabledChanged();
            }
        }
    } = true;

    /// <summary>
    /// Gets or sets whether the element participates in hit testing.
    /// </summary>
    public bool IsHitTestVisible { get; set; } = true;

    /// <summary>
    /// Gets whether the element has keyboard focus.
    /// </summary>
    public bool IsFocused { get; private set; }

    /// <summary>
    /// Gets whether the mouse is over this element.
    /// </summary>
    public bool IsMouseOver { get; private set; }

    /// <summary>
    /// Gets whether this element has mouse capture.
    /// </summary>
    public bool IsMouseCaptured { get; private set; }

    /// <summary>
    /// Gets whether this element can receive focus.
    /// </summary>
    public virtual bool Focusable => false;

    #region Events (using Action delegates for AOT compatibility)

    public Action? GotFocus { get; set; }
    public Action? LostFocus { get; set; }
    public Action? MouseEnter { get; set; }
    public Action? MouseLeave { get; set; }
    public Action<MouseEventArgs>? MouseDown { get; set; }
    public Action<MouseEventArgs>? MouseUp { get; set; }
    public Action<MouseEventArgs>? MouseMove { get; set; }
    public Action<MouseWheelEventArgs>? MouseWheel { get; set; }
    public Action<KeyEventArgs>? KeyDown { get; set; }
    public Action<KeyEventArgs>? KeyUp { get; set; }
    public Action<TextInputEventArgs>? TextInput { get; set; }

    #endregion

    protected override Size MeasureCore(Size availableSize)
    {
        if (!IsVisible)
            return Size.Empty;

        return MeasureOverride(availableSize);
    }

    protected virtual Size MeasureOverride(Size availableSize) => Size.Empty;

    protected override void ArrangeCore(Rect finalRect)
    {
        if (!IsVisible)
            return;

        ArrangeOverride(new Size(finalRect.Width, finalRect.Height));
    }

    protected virtual Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(IGraphicsContext context)
    {
        if (!IsVisible)
            return;

        OnRender(context);
    }

    protected virtual void OnRender(IGraphicsContext context) { }

    /// <summary>
    /// Performs hit testing to find the element at the specified point.
    /// </summary>
    public virtual UIElement? HitTest(Point point)
    {
        if (!IsVisible || !IsHitTestVisible || !IsEnabled)
            return null;

        if (Bounds.Contains(point))
            return this;

        return null;
    }

    /// <summary>
    /// Attempts to focus this element.
    /// </summary>
    public bool Focus()
    {
        if (!Focusable || !IsEnabled || !IsVisible)
            return false;

        var root = FindVisualRoot();
        if (root is Controls.Window window)
        {
            return window.SetFocusedElement(this);
        }
        return false;
    }

    internal void SetFocused(bool focused)
    {
        if (IsFocused != focused)
        {
            IsFocused = focused;
            if (focused)
            {
                OnGotFocus();
                GotFocus?.Invoke();
            }
            else
            {
                OnLostFocus();
                LostFocus?.Invoke();
            }
            InvalidateVisual();
        }
    }

    internal void SetMouseOver(bool mouseOver)
    {
        if (IsMouseOver != mouseOver)
        {
            IsMouseOver = mouseOver;
            if (mouseOver)
            {
                OnMouseEnter();
                MouseEnter?.Invoke();
            }
            else
            {
                OnMouseLeave();
                MouseLeave?.Invoke();
            }
            InvalidateVisual();
        }
    }

    internal void SetMouseCaptured(bool captured) => IsMouseCaptured = captured;

    internal void RegisterBinding(IDisposable binding)
    {
        if (binding == null)
            return;
        _bindings ??= new List<IDisposable>(capacity: 2);
        _bindings.Add(binding);
    }

    internal void DisposeBindings()
    {
        _isVisibleBinding?.Dispose();
        _isVisibleBinding = null;
        _isEnabledBinding?.Dispose();
        _isEnabledBinding = null;

        if (_bindings == null)
            return;

        for (int i = 0; i < _bindings.Count; i++)
        {
            try { _bindings[i].Dispose(); }
            catch { /* best-effort */ }
        }

        _bindings.Clear();
        _bindings = null;
    }

    #region Input Handlers

    protected virtual void OnGotFocus() { }
    protected virtual void OnLostFocus() { }
    protected virtual void OnMouseEnter() { }
    protected virtual void OnMouseLeave() { }

    internal void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);

    internal void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);

    internal void RaiseMouseMove(MouseEventArgs e) => OnMouseMove(e);

    internal void RaiseMouseWheel(MouseWheelEventArgs e) => OnMouseWheel(e);

    internal void RaiseKeyDown(KeyEventArgs e) => OnKeyDown(e);

    internal void RaiseKeyUp(KeyEventArgs e) => OnKeyUp(e);

    internal void RaiseTextInput(TextInputEventArgs e) => OnTextInput(e);

    // Protected virtual hooks for derived controls (public API surface stays small).
    protected virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(e);

    protected virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(e);

    protected virtual void OnMouseMove(MouseEventArgs e) => MouseMove?.Invoke(e);

    protected virtual void OnMouseWheel(MouseWheelEventArgs e) => MouseWheel?.Invoke(e);

    protected virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(e);

    protected virtual void OnKeyUp(KeyEventArgs e) => KeyUp?.Invoke(e);

    protected virtual void OnTextInput(TextInputEventArgs e) => TextInput?.Invoke(e);

    #endregion

    protected virtual void OnVisibilityChanged() { }
    protected virtual void OnEnabledChanged() { }

    #region Binding Helpers

    internal void SetIsVisibleBinding(Func<bool> get, Action<Action>? subscribe = null, Action<Action>? unsubscribe = null)
    {
        if (get == null) throw new ArgumentNullException(nameof(get));

        _isVisibleBinding?.Dispose();
        _isVisibleBinding = new ValueBinding<bool>(
            get,
            set: null,
            subscribe,
            unsubscribe,
            onSourceChanged: () => IsVisible = get());

        IsVisible = get();
    }

    internal void SetIsEnabledBinding(Func<bool> get, Action<Action>? subscribe = null, Action<Action>? unsubscribe = null)
    {
        if (get == null) throw new ArgumentNullException(nameof(get));

        _isEnabledBinding?.Dispose();
        _isEnabledBinding = new ValueBinding<bool>(
            get,
            set: null,
            subscribe,
            unsubscribe,
            onSourceChanged: () => IsEnabled = get());

        IsEnabled = get();
    }

    #endregion
}
