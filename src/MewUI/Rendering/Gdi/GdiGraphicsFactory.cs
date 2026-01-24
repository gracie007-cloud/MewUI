using Aprillz.MewUI;
using Aprillz.MewUI.Native;
using Aprillz.MewUI.Resources;

namespace Aprillz.MewUI.Rendering.Gdi;

/// <summary>
/// GDI graphics factory implementation.
/// </summary>
public sealed class GdiGraphicsFactory : IGraphicsFactory, IWindowResourceReleaser
{
    public GraphicsBackend Backend => GraphicsBackend.Gdi;

    /// <summary>
    /// Gets the singleton instance of the GDI graphics factory.
    /// </summary>
    public static GdiGraphicsFactory Instance => field ??= new GdiGraphicsFactory();

    private GdiGraphicsFactory() { }

    public bool IsDoubleBuffered { get; set; } = true;

    public GdiCurveQuality CurveQuality { get; set; } = GdiCurveQuality.Supersample2x;

    // Keep backend default aligned with other backends: Default => Linear unless the app explicitly overrides.
    public ImageScaleQuality ImageScaleQuality { get; set; } = ImageScaleQuality.Linear;

    public IFont CreateFont(string family, double size, FontWeight weight = FontWeight.Normal,
        bool italic = false, bool underline = false, bool strikethrough = false)
    {
        uint dpi = DpiHelper.GetSystemDpi();
        family = ResolveFontFamilyOrFile(family);
        return new GdiFont(family, size, weight, italic, underline, strikethrough, dpi);
    }

    /// <summary>
    /// Creates a font with a specific DPI.
    /// </summary>
    public IFont CreateFont(string family, double size, uint dpi, FontWeight weight = FontWeight.Normal,
        bool italic = false, bool underline = false, bool strikethrough = false)
    {
        family = ResolveFontFamilyOrFile(family);
        return new GdiFont(family, size, weight, italic, underline, strikethrough, dpi);
    }

    private static string ResolveFontFamilyOrFile(string familyOrPath)
    {
        if (!FontResources.LooksLikeFontFilePath(familyOrPath))
        {
            return familyOrPath;
        }

        var path = Path.GetFullPath(familyOrPath);
        _ = Win32Fonts.EnsurePrivateFont(path);

        return FontResources.TryGetParsedFamilyName(path, out var parsed) && !string.IsNullOrWhiteSpace(parsed)
            ? parsed
            : "Segoe UI";
    }

    public IImage CreateImageFromFile(string path) =>
        CreateImageFromBytes(File.ReadAllBytes(path));

    public IImage CreateImageFromBytes(byte[] data) =>
        ImageDecoders.TryDecode(data, out var bmp)
            ? CreateImage(bmp.WidthPx, bmp.HeightPx, bmp.Data)
            : throw new NotSupportedException(
                $"Unsupported image format. Built-in decoders: BMP/PNG/JPEG. Detected: {ImageDecoders.DetectFormatId(data) ?? "unknown"}.");

    public IImage CreateImageFromPixelSource(IPixelBufferSource source) => new GdiImage(source);

    /// <summary>
    /// Creates an empty 32-bit ARGB image.
    /// </summary>
    public IImage CreateImage(int width, int height) => new GdiImage(width, height);

    /// <summary>
    /// Creates a 32-bit ARGB image from raw pixel data.
    /// </summary>
    public IImage CreateImage(int width, int height, byte[] pixelData) => new GdiImage(width, height, pixelData);

    public IGraphicsContext CreateContext(nint hwnd, nint hdc, double dpiScale)
        => IsDoubleBuffered
        ? new GdiDoubleBufferedContext(hwnd, hdc, dpiScale, CurveQuality, ImageScaleQuality)
        : new GdiGraphicsContext(hwnd, hdc, dpiScale, CurveQuality, ImageScaleQuality);


    public IGraphicsContext CreateMeasurementContext(uint dpi)
    {
        var hdc = Native.User32.GetDC(0);
        return new GdiMeasurementContext(hdc, dpi);
    }

    public void ReleaseWindowResources(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        GdiDoubleBufferedContext.ReleaseForWindow(hwnd);
    }
}
