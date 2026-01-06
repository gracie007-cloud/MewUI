using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Core;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

public sealed class ProgressBar : Control, IDisposable
{
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

    public ProgressBar()
    {
        var theme = Theme.Current;
        Background = theme.ControlBackground;
        BorderBrush = theme.ControlBorder;
        BorderThickness = 1;
        Padding = new Thickness(2);
        Height = 18;
    }

    public ProgressBar BindValue(
        Func<double> get,
        Action<Action>? subscribe = null,
        Action<Action>? unsubscribe = null)
    {
        _valueBinding?.Dispose();
        _valueBinding = new ValueBinding<double>(
            get,
            set: null,
            subscribe,
            unsubscribe,
            onSourceChanged: () =>
            {
                Value = get();
            });

        Value = get();
        return this;
    }

    public ProgressBar BindValue(ObservableValue<double> source)
        => BindValue(() => source.Value, h => source.Changed += h, h => source.Changed -= h);

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        if (Background == oldTheme.ControlBackground)
            Background = newTheme.ControlBackground;
        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;
        base.OnThemeChanged(oldTheme, newTheme);
    }

    protected override Size MeasureContent(Size availableSize) => new Size(120, Height);

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        double radius = theme.ControlCornerRadius;

        if (_valueBinding != null)
        {
            // Pull latest value at paint time (one-way).
            Value = _valueBinding.Get();
        }

        var bounds = Bounds;
        var contentBounds = bounds.Deflate(Padding);

        var bg = IsEnabled ? Background : theme.TextBoxDisabledBackground;
        if (radius > 0)
            context.FillRoundedRectangle(bounds, radius, radius, bg);
        else
            context.FillRectangle(bounds, bg);

        if (BorderThickness > 0)
        {
            var borderColor = BorderBrush;
            if (radius > 0)
                context.DrawRoundedRectangle(bounds, radius, radius, borderColor, BorderThickness);
            else
                context.DrawRectangle(bounds, borderColor, BorderThickness);
        }

        double range = Maximum - Minimum;
        double t = range <= 0 ? 0 : (Value - Minimum) / range;
        t = Math.Clamp(t, 0, 1);

        var fillRect = new Rect(contentBounds.X, contentBounds.Y, contentBounds.Width * t, contentBounds.Height);
        if (fillRect.Width > 0)
        {

            if (radius - 1 > 0)
            {
                double rx = Math.Min(radius-1, fillRect.Width / 2.0);
                context.FillRoundedRectangle(fillRect, rx, rx, theme.Accent);
            }
            else
            {
                context.FillRectangle(fillRect, theme.Accent);
            }
        }
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
