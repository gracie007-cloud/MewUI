using Aprillz.MewUI.Core;
using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

/// <summary>
/// A button control that responds to clicks.
/// </summary>
public class Button : Control, IDisposable
{
    private bool _isPressed;
    private ValueBinding<string>? _contentBinding;
    private bool _disposed;

    public Button()
    {
        var theme = Theme.Current;
        Background = theme.ButtonFace;
        BorderBrush = theme.ControlBorder;
        BorderThickness = 1;
        Padding = new Thickness(12, 6, 12, 6);
    }

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        if (Background == oldTheme.ButtonFace)
            Background = newTheme.ButtonFace;
        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;
        base.OnThemeChanged(oldTheme, newTheme);
    }

    /// <summary>
    /// Gets or sets the button content text.
    /// </summary>
    public string Content
    {
        get;
        set { field = value ?? string.Empty; InvalidateMeasure(); }
    } = string.Empty;

    /// <summary>
    /// Click event handler (AOT-compatible).
    /// </summary>
    public Action? Click { get; set; }

    public override bool Focusable => true;

    protected override Size MeasureContent(Size availableSize)
    {
        if (string.IsNullOrEmpty(Content))
            return new Size(Padding.HorizontalThickness + 20, Padding.VerticalThickness + 10);

        using var measure = BeginTextMeasurement();
        var textSize = measure.Context.MeasureText(Content, measure.Font);

        return textSize.Inflate(Padding);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = Bounds;
        double radius = theme.ControlCornerRadius;

        // Determine visual state
        Color bgColor;
        Color borderColor;

        if (!IsEnabled)
        {
            bgColor = theme.ButtonDisabledBackground;
            borderColor = BorderBrush;
        }
        else if (_isPressed)
        {
            bgColor = theme.ButtonPressedBackground;
            borderColor = theme.Accent;
        }
        else if (IsMouseOver)
        {
            bgColor = theme.ButtonHoverBackground;
            borderColor = BorderBrush.Lerp(theme.Accent, 0.6);
        }
        else
        {
            bgColor = Background;
            borderColor = BorderBrush;
        }

        if (IsEnabled && IsFocused)
            borderColor = theme.Accent;

        // Draw background
        if (radius > 0)
        {
            context.FillRoundedRectangle(bounds, radius, radius, bgColor);
            if (BorderThickness > 0)
                context.DrawRoundedRectangle(bounds, radius, radius, borderColor, BorderThickness);
        }
        else
        {
            context.FillRectangle(bounds, bgColor);
            if (BorderThickness > 0)
                context.DrawRectangle(bounds, borderColor, BorderThickness);
        }

        // Draw text
        if (!string.IsNullOrEmpty(Content))
        {
            var contentBounds = bounds.Deflate(Padding);
            var font = GetFont();
            var textColor = IsEnabled ? Foreground : theme.DisabledText;

            context.DrawText(Content, contentBounds, font, textColor,
                TextAlignment.Center, TextAlignment.Center, TextWrapping.NoWrap);
        }
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButton.Left && IsEnabled)
        {
            _isPressed = true;
            Focus();

            // Capture mouse
            var root = FindVisualRoot();
            if (root is Window window)
                window.CaptureMouse(this);

            InvalidateVisual();
            e.Handled = true;
        }
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButton.Left && _isPressed)
        {
            _isPressed = false;

            // Release capture
            var root = FindVisualRoot();
            if (root is Window window)
                window.ReleaseMouseCapture();

            // Fire click if still over button
            if (IsEnabled && Bounds.Contains(e.Position))
            {
                OnClick();
            }

            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnMouseLeave()
    {
        base.OnMouseLeave();
        if (_isPressed)
        {
            _isPressed = false;
            InvalidateVisual();
        }
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Space or Enter triggers click
        if ((e.Key == Input.Key.Space || e.Key == Input.Key.Enter) && IsEnabled)
        {
            _isPressed = true;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    internal override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if ((e.Key == Input.Key.Space || e.Key == Input.Key.Enter) && _isPressed)
        {
            _isPressed = false;
            OnClick();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected virtual void OnClick() => Click?.Invoke();

    public Button BindContent(ObservableValue<string> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        _contentBinding?.Dispose();
        _contentBinding = new ValueBinding<string>(
            get: () => source.Value,
            set: null,
            subscribe: h => source.Changed += h,
            unsubscribe: h => source.Changed -= h,
            onSourceChanged: () => Content = source.Value ?? string.Empty);

        Content = source.Value ?? string.Empty;
        return this;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _contentBinding?.Dispose();
        _contentBinding = null;
        _disposed = true;
    }
}
