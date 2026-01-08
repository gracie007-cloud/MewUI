using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Core;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

public sealed class ProgressBar : Control
{
    private ValueBinding<double>? _valueBinding;

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

    protected override Color DefaultBackground => Theme.Current.ControlBackground;
    protected override Color DefaultBorderBrush => Theme.Current.ControlBorder;

    public ProgressBar()
    {
        BorderThickness = 1;
        Padding = new Thickness(1);
        Height = 18;
    }

    public void SetValueBinding(
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
            onSourceChanged: () => Value = get());

        Value = get();
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

        var bounds = GetSnappedBorderBounds(Bounds);
        var borderInset = GetBorderVisualInset();
        var contentBounds = bounds.Deflate(Padding).Deflate(new Thickness(borderInset));

        var bg = IsEnabled ? Background : theme.TextBoxDisabledBackground;
        DrawBackgroundAndBorder(context, bounds, bg, BorderBrush, radius);

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

    protected override void OnDispose()
    {
        _valueBinding?.Dispose();
        _valueBinding = null;
    }
}
