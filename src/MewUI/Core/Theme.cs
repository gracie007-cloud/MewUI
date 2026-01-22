namespace Aprillz.MewUI;

public record class Theme
{
    public static Accent DefaultAccent { get; } = Accent.Blue;

    public static Theme Light => field ??= CreateLight();

    public static Theme Dark => field ??= CreateDark();


    internal static Theme DefaultMericTheme { get; } = new Theme
    {
        Name = null!,
        Palette = null!,
        BaseControlHeight = 28,
        ControlCornerRadius = 4,
        ListItemPadding = new Thickness(8, 2, 8, 2),
        FontFamily = "Segoe UI",
        FontSize = 12,
        FontWeight = FontWeight.Normal,
        ScrollBarThickness = 4,
        ScrollBarHitThickness = 10,
        ScrollBarMinThumbLength = 14,
        ScrollWheelStep = 32,
        ScrollBarSmallChange = 24,
        ScrollBarLargeChange = 120
    };

    internal static ThemeSeed LightSeed { get; } = new ThemeSeed
    {
        WindowBackground = Color.FromRgb(250, 250, 250),
        WindowText = Color.FromRgb(30, 30, 30),
        ControlBackground = Color.White,
        ButtonFace = Color.FromRgb(232, 232, 232),
        ButtonDisabledBackground = Color.FromRgb(204, 204, 204)
    };

    internal static ThemeSeed DarkSeed { get; } = new ThemeSeed
    {
        WindowBackground = Color.FromRgb(30, 30, 30),
        WindowText = Color.FromRgb(230, 230, 232),
        ControlBackground = Color.FromRgb(26, 26, 27),
        ButtonFace = Color.FromRgb(48, 48, 50),
        ButtonDisabledBackground = Color.FromRgb(60, 60, 64)
    };

    public required string Name { get; init; }

    public required Palette Palette { get; init; }

    public required double BaseControlHeight { get; init; }

    public required double ControlCornerRadius { get; init; }

    public required Thickness ListItemPadding { get; init; }

    public required string FontFamily { get; init; }

    public required double FontSize { get; init; }

    public required FontWeight FontWeight { get; init; }

    public required double ScrollBarThickness { get; init; }

    public required double ScrollBarHitThickness { get; init; }

    public required double ScrollBarMinThumbLength { get; init; }

    public required double ScrollWheelStep { get; init; }

    public required double ScrollBarSmallChange { get; init; }

    public required double ScrollBarLargeChange { get; init; }

    public Theme WithPalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        return this with
        {
            Palette = palette
        };
    }

    private static Theme CreateLight()
    {
        var palette = new Palette(LightSeed, DefaultAccent.GetColor(false));

        return DefaultMericTheme with
        {
            Name = "Light",
            Palette = palette,
        };
    }

    private static Theme CreateDark()
    {
        var palette = new Palette(DarkSeed, DefaultAccent.GetColor(true));

        return DefaultMericTheme with
        {
            Name = "Dark",
            Palette = palette,
        };
    }
}