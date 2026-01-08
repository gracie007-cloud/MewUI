using Aprillz.MewUI.Core;
using Aprillz.MewUI.Elements;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

/// <summary>
/// Base class for all controls.
/// </summary>
public abstract class Control : FrameworkElement, IDisposable
{
    private IFont? _font;
    private bool _disposed;
    private Color? _background;
    private Color? _foreground;
    private Color? _borderBrush;
    private string? _fontFamily;
    private double? _fontSize;
    private FontWeight? _fontWeight;

    protected virtual Color DefaultBackground => Color.Transparent;
    protected virtual Color DefaultForeground => Theme.Current.WindowText;
    protected virtual Color DefaultBorderBrush => Color.Transparent;
    protected virtual string DefaultFontFamily => Theme.Current.FontFamily;
    protected virtual double DefaultFontSize => Theme.Current.FontSize;
    protected virtual FontWeight DefaultFontWeight => Theme.Current.FontWeight;

    protected readonly struct TextMeasurementScope : IDisposable
    {
        public IGraphicsFactory Factory { get; }
        public IGraphicsContext Context { get; }
        public IFont Font { get; }

        public TextMeasurementScope(IGraphicsFactory factory, IGraphicsContext context, IFont font)
        {
            Factory = factory;
            Context = context;
            Font = font;
        }

        public void Dispose() => Context.Dispose();
    }

    protected TextMeasurementScope BeginTextMeasurement()
    {
        var factory = GetGraphicsFactory();
        var context = factory.CreateMeasurementContext(GetDpi());
        var font = GetFont(factory);
        return new TextMeasurementScope(factory, context, font);
    }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public Color Background
    {
        get => _background ?? DefaultBackground;
        set
        {
            _background = value;
            InvalidateVisual();
        }
    }

    public void ClearBackground()
    {
        if (_background == null)
            return;
        _background = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Gets or sets the foreground (text) color.
    /// </summary>
    public Color Foreground
    {
        get => _foreground ?? DefaultForeground;
        set
        {
            _foreground = value;
            InvalidateVisual();
        }
    }

    public void ClearForeground()
    {
        if (_foreground == null)
            return;
        _foreground = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Color BorderBrush
    {
        get => _borderBrush ?? DefaultBorderBrush;
        set
        {
            _borderBrush = value;
            InvalidateVisual();
        }
    }

    public void ClearBorderBrush()
    {
        if (_borderBrush == null)
            return;
        _borderBrush = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Gets or sets the border thickness.
    /// </summary>
    public double BorderThickness
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily
    {
        get => _fontFamily ?? DefaultFontFamily;
        set
        {
            _fontFamily = value ?? string.Empty;
            _font?.Dispose();
            _font = null;
            InvalidateMeasure();
        }
    }

    public void ClearFontFamily()
    {
        if (_fontFamily == null)
            return;
        _fontFamily = null;
        _font?.Dispose();
        _font = null;
        InvalidateMeasure();
    }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public double FontSize
    {
        get => _fontSize ?? DefaultFontSize;
        set
        {
            _fontSize = value;
            _font?.Dispose();
            _font = null;
            InvalidateMeasure();
        }
    }

    public void ClearFontSize()
    {
        if (_fontSize == null)
            return;
        _fontSize = null;
        _font?.Dispose();
        _font = null;
        InvalidateMeasure();
    }

    /// <summary>
    /// Gets or sets the font weight.
    /// </summary>
    public FontWeight FontWeight
    {
        get => _fontWeight ?? DefaultFontWeight;
        set
        {
            _fontWeight = value;
            _font?.Dispose();
            _font = null;
            InvalidateMeasure();
        }
    }

    public void ClearFontWeight()
    {
        if (_fontWeight == null)
            return;
        _fontWeight = null;
        _font?.Dispose();
        _font = null;
        InvalidateMeasure();
    }

    /// <summary>
    /// Gets or creates the font for this control.
    /// </summary>
    protected IFont GetFont(IGraphicsFactory factory)
    {
        if (_font == null)
        {
            _font = factory.CreateFont(FontFamily, FontSize, GetDpi(), FontWeight);
        }

        return _font;
    }

    internal void NotifyDpiChanged(uint oldDpi, uint newDpi) => OnDpiChanged(oldDpi, newDpi);

    protected virtual void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        _font?.Dispose();
        _font = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    internal void NotifyThemeChanged(Theme oldTheme, Theme newTheme) => OnThemeChanged(oldTheme, newTheme);

    protected virtual void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        _font?.Dispose();
        _font = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected Theme GetTheme()
    {
        return Theme.Current;
    }

    /// <summary>
    /// Gets the graphics factory from the owning window, or the default factory.
    /// </summary>
    protected IGraphicsFactory GetGraphicsFactory()
    {
        var root = FindVisualRoot();
        if (root is Window window)
            return window.GraphicsFactory;

        return Application.DefaultGraphicsFactory;
    }

    /// <summary>
    /// Gets the font using the control's graphics factory.
    /// </summary>
    protected IFont GetFont() => GetFont(GetGraphicsFactory());

    protected uint GetDpi()
    {
        var root = FindVisualRoot();
        if (root is Window window)
            return window.Dpi;
        return DpiHelper.GetSystemDpi();
    }

    protected double GetBorderVisualInset()
    {
        if (BorderThickness <= 0)
            return 0;

        // For centered strokes (Direct2D default), half of the stroke is inside the bounds.
        // Round to device pixels so 1px borders don't collapse into a "no padding" look.
        var dpiScale = GetDpi() / 96.0;
        return LayoutRounding.RoundToPixel(BorderThickness / 2.0, dpiScale);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        base.OnRender(context);

        var bounds = Bounds;

        // Draw background
        if (Background.A > 0)
        {
            context.FillRectangle(bounds, Background);
        }

        // Draw border
        if (BorderThickness > 0 && BorderBrush.A > 0)
        {
            context.DrawRectangle(bounds, BorderBrush, BorderThickness);
        }
    }

    protected virtual void OnDispose() { }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Release extension-managed bindings (and any other UIElement-registered disposables).
        DisposeBindings();

        // Release cached font resources.
        _font?.Dispose();
        _font = null;

        OnDispose();
    }
}
