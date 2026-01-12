using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Core;

public record class Theme
{
    public static Theme Light { get; } = CreateLight();
    public static Theme Dark { get; } = CreateDark();

    public static Theme Current
    {
        get => _current;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (_current == value)
            {
                return;
            }

            var old = _current;
            _current = value;
            CurrentChanged?.Invoke(old, value);
        }
    }

    private static Theme _current = Light;

    public static Action<Theme, Theme>? CurrentChanged { get; set; }

    public string Name { get; init; }

    public Palette Palette { get; }

    public Color WindowBackground => Palette.WindowBackground;
    public Color WindowText => Palette.WindowText;
    public Color ControlBackground => Palette.ControlBackground;
    public Color ControlBorder => Palette.ControlBorder;

    public Color ButtonFace => Palette.ButtonFace;
    public Color ButtonHoverBackground => Palette.ButtonHoverBackground;
    public Color ButtonPressedBackground => Palette.ButtonPressedBackground;
    public Color ButtonDisabledBackground => Palette.ButtonDisabledBackground;

    public Color Accent => Palette.Accent;
    public Color AccentText => Palette.AccentText;
    public Color SelectionBackground => Palette.SelectionBackground;
    public Color SelectionText => Palette.SelectionText;

    public Color DisabledText => Palette.DisabledText;
    public Color PlaceholderText => Palette.PlaceholderText;
    public Color TextBoxDisabledBackground => Palette.TextBoxDisabledBackground;
    public Color FocusRect => Palette.FocusRect;

    public double BaseControlHeight { get; init; }

    public double ControlCornerRadius { get; init; }

    public string FontFamily { get; init; }
    public double FontSize { get; init; }
    public FontWeight FontWeight { get; init; }

    // Scroll (thin style defaults)
    public double ScrollBarThickness { get; init; }
    public double ScrollBarHitThickness { get; init; }
    public double ScrollBarMinThumbLength { get; init; }
    public double ScrollWheelStep { get; init; }
    public double ScrollBarSmallChange { get; init; }
    public double ScrollBarLargeChange { get; init; }
    public Color ScrollBarThumb => Palette.ScrollBarThumb;
    public Color ScrollBarThumbHover => Palette.ScrollBarThumbHover;
    public Color ScrollBarThumbActive => Palette.ScrollBarThumbActive;

    public Theme WithAccent(Color accent, Color? accentText = null)
    {
        return WithPalette(Palette.WithAccent(accent, accentText));
    }

    private Theme(
        string name,
        Palette palette,
        double baseControlHeight,
        double controlCornerRadius,
        string fontFamily,
        double fontSize,
        FontWeight fontWeight,
        double scrollBarThickness,
        double scrollBarHitThickness,
        double scrollBarMinThumbLength,
        double scrollWheelStep,
        double scrollBarSmallChange,
        double scrollBarLargeChange)
    {
        Name = name;
        Palette = palette;
        BaseControlHeight = baseControlHeight;
        ControlCornerRadius = controlCornerRadius;
        FontFamily = fontFamily;
        FontSize = fontSize;
        FontWeight = fontWeight;

        ScrollBarThickness = scrollBarThickness;
        ScrollBarHitThickness = scrollBarHitThickness;
        ScrollBarMinThumbLength = scrollBarMinThumbLength;
        ScrollWheelStep = scrollWheelStep;
        ScrollBarSmallChange = scrollBarSmallChange;
        ScrollBarLargeChange = scrollBarLargeChange;
    }

    public Theme WithPalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        return new Theme(
            name: Name,
            palette: palette,
            baseControlHeight: BaseControlHeight,
            controlCornerRadius: ControlCornerRadius,
            fontFamily: FontFamily,
            fontSize: FontSize,
            fontWeight: FontWeight,
            scrollBarThickness: ScrollBarThickness,
            scrollBarHitThickness: ScrollBarHitThickness,
            scrollBarMinThumbLength: ScrollBarMinThumbLength,
            scrollWheelStep: ScrollWheelStep,
            scrollBarSmallChange: ScrollBarSmallChange,
            scrollBarLargeChange: ScrollBarLargeChange);
    }

    private static Theme CreateLight()
    {
        var palette = new Palette(
            name: "Light",
            windowBackground: Color.FromRgb(244, 244, 244),
            windowText: Color.FromRgb(30, 30, 30),
            controlBackground: Color.White,
            buttonFace: Color.FromRgb(232, 232, 232),
            buttonDisabledBackground: Color.FromRgb(204, 204, 204),
            accent: Color.FromRgb(214, 176, 82));

        return new Theme(
            name: "Light",
            palette: palette,
            baseControlHeight: 28,
            controlCornerRadius: 4,
            fontFamily: "Segoe UI",
            fontSize: 12,
            fontWeight: FontWeight.Normal,
            scrollBarThickness: 4,
            scrollBarHitThickness: 10,
            scrollBarMinThumbLength: 14,
            scrollWheelStep: 32,
            scrollBarSmallChange: 24,
            scrollBarLargeChange: 120);
    }

    private static Theme CreateDark()
    {
        var palette = new Palette(
            name: "Dark",
            windowBackground: Color.FromRgb(28, 28, 28),
            windowText: Color.FromRgb(230, 230, 232),
            controlBackground: Color.FromRgb(26, 26, 27),
            buttonFace: Color.FromRgb(48, 48, 50),
            buttonDisabledBackground: Color.FromRgb(60, 60, 64),
            accent: Color.FromRgb(214, 165, 94));

        return new Theme(
            name: "Dark",
            palette: palette,
            baseControlHeight: 28,
            controlCornerRadius: 4,
            fontFamily: "Segoe UI",
            fontSize: 12,
            fontWeight: FontWeight.Normal,
            scrollBarThickness: 4,
            scrollBarHitThickness: 10,
            scrollBarMinThumbLength: 14,
            scrollWheelStep: 32,
            scrollBarSmallChange: 24,
            scrollBarLargeChange: 120);
    }
}
