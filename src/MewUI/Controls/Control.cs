using Aprillz.MewUI.Core;
using Aprillz.MewUI.Elements;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

/// <summary>
/// Base class for all controls.
/// </summary>
public abstract class Control : FrameworkElement
{
    private IFont? _font;

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
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = Color.Transparent;

    /// <summary>
    /// Gets or sets the foreground (text) color.
    /// </summary>
    public Color Foreground
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = Theme.Current.WindowText;

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Color BorderBrush
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = Color.Transparent;

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
        get;
        set
        {
            field = value;
            _font?.Dispose();
            _font = null;
            InvalidateMeasure();
        }
    } = Theme.Current.FontFamily;

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public double FontSize
    {
        get;
        set
        {
            field = value;
            _font?.Dispose();
            _font = null;
            InvalidateMeasure();
        }
    } = Theme.Current.FontSize;

    /// <summary>
    /// Gets or sets the font weight.
    /// </summary>
    public FontWeight FontWeight
    {
        get;
        set
        {
            field = value;
            _font?.Dispose();
            _font = null;
            InvalidateMeasure();
        }
    } = Theme.Current.FontWeight;

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
        if (Foreground == oldTheme.WindowText)
            Foreground = newTheme.WindowText;

        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;

        if (FontFamily == oldTheme.FontFamily)
            FontFamily = newTheme.FontFamily;

        if (FontSize.Equals(oldTheme.FontSize))
            FontSize = newTheme.FontSize;

        if (FontWeight == oldTheme.FontWeight)
            FontWeight = newTheme.FontWeight;

        InvalidateVisual();
    }

    protected Theme GetTheme()
    {
        var root = FindVisualRoot();
        if (root is Window window)
            return window.Theme;
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
}
