using Aprillz.MewUI.Core;

namespace Aprillz.MewUI.Rendering.Gdi;

/// <summary>
/// GDI graphics factory implementation.
/// </summary>
public sealed class GdiGraphicsFactory : IGraphicsFactory
{
    /// <summary>
    /// Gets the singleton instance of the GDI graphics factory.
    /// </summary>
    public static GdiGraphicsFactory Instance => field ??= new GdiGraphicsFactory();

    private GdiGraphicsFactory() { }

    public bool IsDoubleBuffered { get; set; } = true;

    public GdiCurveQuality CurveQuality { get; set; } = GdiCurveQuality.Fast;

    public IFont CreateFont(string family, double size, FontWeight weight = FontWeight.Normal,
        bool italic = false, bool underline = false, bool strikethrough = false)
    {
        uint dpi = DpiHelper.GetSystemDpi();
        return new GdiFont(family, size, weight, italic, underline, strikethrough, dpi);
    }

    /// <summary>
    /// Creates a font with a specific DPI.
    /// </summary>
    public IFont CreateFont(string family, double size, uint dpi, FontWeight weight = FontWeight.Normal,
        bool italic = false, bool underline = false, bool strikethrough = false) => new GdiFont(family, size, weight, italic, underline, strikethrough, dpi);

    public IImage CreateImageFromFile(string path) =>
        // For simplicity, we'll use a basic implementation
        // In a full implementation, you'd use WIC or another library to load images
        throw new NotImplementedException("Image loading from file is not yet implemented. Use CreateImageFromBytes instead.");

    public IImage CreateImageFromBytes(byte[] data) =>
        // This expects raw BGRA pixel data
        // In a full implementation, you'd parse the image format
        throw new NotImplementedException("Image loading from bytes is not yet implemented.");

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
        ? new GdiDoubleBufferedContext(hwnd, hdc, dpiScale, CurveQuality)
        : new GdiGraphicsContext(hwnd, hdc, dpiScale, CurveQuality);


    public IGraphicsContext CreateMeasurementContext(uint dpi)
    {
        var hdc = Native.User32.GetDC(0);
        return new GdiMeasurementContext(hdc, dpi);
    }

    public void ReleaseWindowResources(nint hwnd)
    {
        if (hwnd == 0)
            return;

        GdiDoubleBufferedContext.ReleaseForWindow(hwnd);
    }
}
