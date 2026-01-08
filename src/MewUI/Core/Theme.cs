using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Core;

public sealed class Theme
{
    public static Theme Light { get; } = CreateLight();
    public static Theme Dark { get; } = CreateDark();

    public static Theme Current
    {
        get => _current;
        set
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (_current == value) return;
            var old = _current;
            _current = value;
            CurrentChanged?.Invoke(old, value);
        }
    }

    private static Theme _current = Light;

    public static Action<Theme, Theme>? CurrentChanged { get; set; }

    public string Name { get; }

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

    public double ControlCornerRadius { get; }

    public string FontFamily { get; }
    public double FontSize { get; }
    public FontWeight FontWeight { get; }

    public Theme WithAccent(Color accent, Color? accentText = null)
    {
        return WithPalette(Palette.WithAccent(accent, accentText));
    }

    private Theme(
        string name,
        Palette palette,
        double controlCornerRadius,
        string fontFamily,
        double fontSize,
        FontWeight fontWeight)
    {
        Name = name;
        Palette = palette;
        ControlCornerRadius = controlCornerRadius;
        FontFamily = fontFamily;
        FontSize = fontSize;
        FontWeight = fontWeight;
    }

    public Theme WithPalette(Palette palette)
    {
        if (palette == null) throw new ArgumentNullException(nameof(palette));
        return new Theme(
            name: Name,
            palette: palette,
            controlCornerRadius: ControlCornerRadius,
            fontFamily: FontFamily,
            fontSize: FontSize,
            fontWeight: FontWeight);
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
            controlCornerRadius: 3,
            fontFamily: "Segoe UI",
            fontSize: 12,
            fontWeight: FontWeight.Normal);
    }

    private static Theme CreateDark()
    {
        var palette = new Palette(
            name: "Dark",
            windowBackground: Color.FromRgb(24, 24, 24),
            windowText: Color.FromRgb(230, 230, 232),
            controlBackground: Color.FromRgb(38, 38, 40),
            buttonFace: Color.FromRgb(48, 48, 50),
            buttonDisabledBackground: Color.FromRgb(60, 60, 64),
            accent: Color.FromRgb(214, 165, 94));

        return new Theme(
            name: "Dark",
            palette: palette,
            controlCornerRadius: 3,
            fontFamily: "Segoe UI",
            fontSize: 12,
            fontWeight: FontWeight.Normal);
    }
}
