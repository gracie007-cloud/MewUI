using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Core;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

public sealed class Slider : Control, IDisposable
{
    private bool _isDragging;
    private ValueBinding<double>? _valueBinding;
    private bool _disposed;

    public double Minimum
    {
        get;
        set { field = value; InvalidateVisual(); }
    }

    public double Maximum
    {
        get;
        set { field = value; InvalidateVisual(); }
    } = 100;

    public double Value
    {
        get;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                value = 0;

            double clamped = ClampToRange(value);
            if (field.Equals(clamped))
                return;

            field = clamped;
            ValueChanged?.Invoke(field);
            InvalidateVisual();
        }
    }

    public Action<double>? ValueChanged { get; set; }

    public double SmallChange { get; set; } = 1;

    public Slider()
    {
        var theme = Theme.Current;
        Background = Color.Transparent;
        BorderBrush = theme.ControlBorder;
        BorderThickness = 1;
        Height = 24;
        Padding = new Thickness(6, 2, 6, 2);
    }

    public Slider BindValue(
        Func<double> get,
        Action<double> set,
        Action<Action>? subscribe = null,
        Action<Action>? unsubscribe = null)
    {
        _valueBinding?.Dispose();
        _valueBinding = new ValueBinding<double>(
            get,
            set,
            subscribe,
            unsubscribe,
            onSourceChanged: () =>
            {
                if (_isDragging)
                    return;
                Value = get();
            });

        Value = get();
        return this;
    }

    public Slider BindValue(ObservableValue<double> source)
        => BindValue(() => source.Value, v => source.Value = v, h => source.Changed += h, h => source.Changed -= h);

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;
        base.OnThemeChanged(oldTheme, newTheme);
    }

    protected override Size MeasureContent(Size availableSize) => new Size(160, Height);

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();

        if (_valueBinding != null && !_isDragging)
            Value = _valueBinding.Get();

        var bounds = Bounds;
        var contentBounds = bounds.Deflate(Padding);

        // Track
        double trackHeight = 4;
        double trackY = contentBounds.Y + (contentBounds.Height - trackHeight) / 2;
        var trackRect = new Rect(contentBounds.X, trackY, contentBounds.Width, trackHeight);

        var trackBg = IsEnabled
            ? theme.ControlBackground.Lerp(theme.WindowText, 0.12)
            : theme.TextBoxDisabledBackground;

        context.FillRoundedRectangle(trackRect, 2, 2, trackBg);
        if (IsEnabled)
        {
            var trackBorder = trackBg.Lerp(theme.WindowText, 0.12);
            context.DrawRoundedRectangle(trackRect, 2, 2, trackBorder, 1);
        }

        // Filled track
        double t = GetNormalizedValue();
        var fillRect = new Rect(trackRect.X, trackRect.Y, trackRect.Width * t, trackRect.Height);
        if (fillRect.Width > 0)
            context.FillRoundedRectangle(fillRect, 2, 2, theme.Accent);

        // Thumb
        double thumbSize = 14;
        double thumbX = trackRect.X + trackRect.Width * t - thumbSize / 2;
        thumbX = Math.Clamp(thumbX, contentBounds.X - thumbSize / 2, contentBounds.Right - thumbSize / 2);

        double thumbY = contentBounds.Y + (contentBounds.Height - thumbSize) / 2;
        var thumbRect = new Rect(thumbX, thumbY, thumbSize, thumbSize);

        var thumbFill = IsEnabled ? theme.ControlBackground : theme.TextBoxDisabledBackground;
        context.FillEllipse(thumbRect, thumbFill);

        var thumbBorder = BorderBrush;
        if (IsEnabled)
        {
            if (IsFocused || _isDragging)
                thumbBorder = theme.Accent;
            else if (IsMouseOver)
                thumbBorder = BorderBrush.Lerp(theme.Accent, 0.6);
        }
        context.DrawEllipse(thumbRect, thumbBorder, 1);
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!IsEnabled || e.Button != MouseButton.Left)
            return;

        Focus();
        _isDragging = true;
        SetValueFromPosition(e.Position.X);

        var root = FindVisualRoot();
        if (root is Window window)
            window.CaptureMouse(this);

        e.Handled = true;
    }

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!IsEnabled || !_isDragging || !IsMouseCaptured || !e.LeftButton)
            return;

        SetValueFromPosition(e.Position.X);
        e.Handled = true;
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButton.Left || !_isDragging)
            return;

        _isDragging = false;

        var root = FindVisualRoot();
        if (root is Window window)
            window.ReleaseMouseCapture();

        e.Handled = true;
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled)
            return;

        if (e.Key == Input.Key.Left)
        {
            SetValueInternal(Value - SmallChange, fromUser: true);
            e.Handled = true;
        }
        else if (e.Key == Input.Key.Right)
        {
            SetValueInternal(Value + SmallChange, fromUser: true);
            e.Handled = true;
        }
    }

    private void SetValueFromPosition(double x)
    {
        var contentBounds = Bounds.Deflate(Padding);
        double left = contentBounds.X;
        double width = Math.Max(1e-6, contentBounds.Width);
        double t = Math.Clamp((x - left) / width, 0, 1);
        double range = Maximum - Minimum;
        double value = range <= 0 ? Minimum : Minimum + t * range;
        SetValueInternal(value, fromUser: true);
    }

    private void SetValueInternal(double value, bool fromUser)
    {
        double clamped = ClampToRange(value);
        if (Value.Equals(clamped))
            return;

        // Update backing field without re-entering ValueChanged twice.
        Value = clamped;

        if (fromUser && _valueBinding != null)
            _valueBinding.Set(clamped);
    }

    private double GetNormalizedValue()
    {
        double range = Maximum - Minimum;
        if (range <= 0)
            return 0;
        return Math.Clamp((Value - Minimum) / range, 0, 1);
    }

    private double ClampToRange(double value)
    {
        double min = Math.Min(Minimum, Maximum);
        double max = Math.Max(Minimum, Maximum);
        return Math.Clamp(value, min, max);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _valueBinding?.Dispose();
        _valueBinding = null;
        _disposed = true;
    }
}
