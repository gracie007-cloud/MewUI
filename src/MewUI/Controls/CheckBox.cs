using Aprillz.MewUI.Core;
using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

public class CheckBox : Control
{
    private bool _isPressed;
    private ValueBinding<bool>? _checkedBinding;
    private bool _updatingFromSource;

    public string Text
    {
        get;
        set { field = value ?? string.Empty; InvalidateMeasure(); }
    } = string.Empty;

    public bool IsChecked
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            CheckedChanged?.Invoke(value);
            InvalidateVisual();
        }
    }

    public Action<bool>? CheckedChanged { get; set; }

    public override bool Focusable => true;

    protected override Color DefaultBorderBrush => Theme.Current.ControlBorder;

    public CheckBox()
    {
        Background = Color.Transparent;
        BorderThickness = 1;
        Padding = new Thickness(2);
    }

    protected override Size MeasureContent(Size availableSize)
    {
        const double boxSize = 14;
        const double spacing = 6;

        double width = boxSize + spacing;
        double height = boxSize;

        if (!string.IsNullOrEmpty(Text))
        {
            using var measure = BeginTextMeasurement();
            var textSize = measure.Context.MeasureText(Text, measure.Font);
            width += textSize.Width;
            height = Math.Max(height, textSize.Height);
        }

        return new Size(width, height).Inflate(Padding);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = Bounds;
        var contentBounds = bounds.Deflate(Padding);

        const double boxSize = 14;
        const double spacing = 6;

        double boxY = contentBounds.Y + (contentBounds.Height - boxSize) / 2;
        var boxRect = new Rect(contentBounds.X, boxY, boxSize, boxSize);

        var fill = IsEnabled ? theme.ControlBackground : theme.TextBoxDisabledBackground;
        var radius = Math.Max(0, theme.ControlCornerRadius * 0.5);
        if (radius > 0)
            context.FillRoundedRectangle(boxRect, radius, radius, fill);
        else
            context.FillRectangle(boxRect, fill);

        var borderColor = BorderBrush;
        if (IsEnabled)
        {
            if (IsFocused || _isPressed)
                borderColor = theme.Accent;
            else if (IsMouseOver)
                borderColor = BorderBrush.Lerp(theme.Accent, 0.6);
        }
        var stroke = Math.Max(1, BorderThickness);
        if (radius > 0)
            context.DrawRoundedRectangle(boxRect, radius, radius, borderColor, stroke);
        else
            context.DrawRectangle(boxRect, borderColor, stroke);

        if (IsChecked)
        {
            // Check mark
            var p1 = new Point(boxRect.X + 3, boxRect.Y + boxRect.Height * 0.55);
            var p2 = new Point(boxRect.X + boxRect.Width * 0.45, boxRect.Bottom - 3);
            var p3 = new Point(boxRect.Right - 3, boxRect.Y + 3);
            context.DrawLine(p1, p2, theme.Accent, 2);
            context.DrawLine(p2, p3, theme.Accent, 2);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            var font = GetFont();
            var textColor = IsEnabled ? Foreground : theme.DisabledText;
            var textBounds = new Rect(contentBounds.X + boxSize + spacing, contentBounds.Y, contentBounds.Width - boxSize - spacing, contentBounds.Height);
            context.DrawText(Text, textBounds, font, textColor, TextAlignment.Left, TextAlignment.Center, TextWrapping.NoWrap);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!IsEnabled || e.Button != MouseButton.Left)
            return;

        _isPressed = true;
        Focus();

        var root = FindVisualRoot();
        if (root is Window window)
            window.CaptureMouse(this);

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButton.Left || !_isPressed)
            return;

        _isPressed = false;

        var root = FindVisualRoot();
        if (root is Window window)
            window.ReleaseMouseCapture();

        if (IsEnabled && Bounds.Contains(e.Position))
            IsChecked = !IsChecked;

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (!IsEnabled)
            return;

        if (e.Key == Input.Key.Space)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    public void SetIsCheckedBinding(
        Func<bool> get,
        Action<bool> set,
        Action<Action>? subscribe = null,
        Action<Action>? unsubscribe = null)
    {
        if (get == null) throw new ArgumentNullException(nameof(get));
        if (set == null) throw new ArgumentNullException(nameof(set));

        _checkedBinding?.Dispose();
        _checkedBinding = new ValueBinding<bool>(
            get,
            set,
            subscribe,
            unsubscribe,
            onSourceChanged: () =>
            {
                _updatingFromSource = true;
                try { IsChecked = get(); }
                finally { _updatingFromSource = false; }
            });

        var existing = CheckedChanged;
        CheckedChanged = v =>
        {
            existing?.Invoke(v);

            if (_updatingFromSource)
                return;

            _checkedBinding?.Set(v);
        };

        _updatingFromSource = true;
        try { IsChecked = get(); }
        finally { _updatingFromSource = false; }
    }

    protected override void OnDispose()
    {
        _checkedBinding?.Dispose();
        _checkedBinding = null;
    }
}
