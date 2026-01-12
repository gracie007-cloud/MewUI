using Aprillz.MewUI.Core;
using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

/// <summary>
/// A button control that responds to clicks.
/// </summary>
public class Button : Control
{
    private bool _isPressed;
    private ValueBinding<string>? _contentBinding;
    private Func<bool>? _canClick;

    protected override Color DefaultBackground => Theme.Current.ButtonFace;
    protected override Color DefaultBorderBrush => Theme.Current.ControlBorder;

    public Button()
    {
        BorderThickness = 1;
        Padding = new Thickness(12, 4, 12, 4);
		MinHeight = 28;
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

    public Func<bool>? CanClick
    {
        get => _canClick;
        set
        {
            _canClick = value;
            ReevaluateSuggestedIsEnabled();
        }
    }

    public override bool Focusable => true;

    protected override bool ComputeIsEnabledSuggestion() => CanClick?.Invoke() ?? true;

    protected override Size MeasureContent(Size availableSize)
    {
        var borderInset = GetBorderVisualInset();
        var border = borderInset > 0 ? new Thickness(borderInset) : Thickness.Zero;

        if (string.IsNullOrEmpty(Content))
        {
            return new Size(Padding.HorizontalThickness + 20, Padding.VerticalThickness + 10).Inflate(border);
        }

        using var measure = BeginTextMeasurement();
        var textSize = measure.Context.MeasureText(Content, measure.Font);

        return textSize.Inflate(Padding).Inflate(border);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = GetSnappedBorderBounds(Bounds);
        double radius = theme.ControlCornerRadius;
        var state = GetVisualState(isPressed: _isPressed, isActive: _isPressed);

        // Determine visual state
        Color bgColor;
        Color borderColor = PickAccentBorder(theme, BorderBrush, state, hoverMix: 0.6);

        if (!state.IsEnabled)
        {
            bgColor = theme.ButtonDisabledBackground;
        }
        else if (state.IsPressed)
        {
            bgColor = theme.ButtonPressedBackground;
        }
        else if (state.IsHot)
        {
            bgColor = theme.ButtonHoverBackground;
        }
        else
        {
            bgColor = Background;
        }

        DrawBackgroundAndBorder(context, bounds, bgColor, borderColor, radius);

        // Draw text
        if (!string.IsNullOrEmpty(Content))
        {
            var contentBounds = bounds.Deflate(Padding).Deflate(new Thickness(GetBorderVisualInset()));
            var font = GetFont();
            var textColor = state.IsEnabled ? Foreground : theme.DisabledText;

            context.DrawText(Content, contentBounds, font, textColor,
                TextAlignment.Center, TextAlignment.Center, TextWrapping.NoWrap);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButton.Left && IsEffectivelyEnabled)
        {
            _isPressed = true;
            Focus();

            // Capture mouse
            var root = FindVisualRoot();
            if (root is Window window)
            {
                window.CaptureMouse(this);
            }

            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButton.Left && _isPressed)
        {
            _isPressed = false;

            // Release capture
            var root = FindVisualRoot();
            if (root is Window window)
            {
                window.ReleaseMouseCapture();
            }

            // Fire click if still over button
            if (IsEffectivelyEnabled && Bounds.Contains(e.Position))
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Space or Enter triggers click
        if ((e.Key == Key.Space || e.Key == Key.Enter) && IsEffectivelyEnabled)
        {
            _isPressed = true;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if ((e.Key == Key.Space || e.Key == Key.Enter) && _isPressed)
        {
            _isPressed = false;
            if (IsEffectivelyEnabled)
            {
                OnClick();
            }

            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected virtual void OnClick() => Click?.Invoke();

    public void SetContentBinding(Func<string> get, Action<Action>? subscribe = null, Action<Action>? unsubscribe = null)
    {
        if (get == null)
        {
            throw new ArgumentNullException(nameof(get));
        }

        _contentBinding?.Dispose();
        _contentBinding = new ValueBinding<string>(
            get,
            set: null,
            subscribe,
            unsubscribe,
            onSourceChanged: () => Content = get() ?? string.Empty);

        Content = get() ?? string.Empty;
    }

    protected override void OnDispose()
    {
        _contentBinding?.Dispose();
        _contentBinding = null;
    }
}
