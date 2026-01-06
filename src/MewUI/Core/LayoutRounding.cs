using Aprillz.MewUI.Primitives;

namespace Aprillz.MewUI.Core;

internal static class LayoutRounding
{
    public static Size RoundSizeToPixels(Size size, double dpiScale)
    {
        if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
            return size;

        if (size.IsEmpty)
            return size;

        var w = RoundToPixel(size.Width, dpiScale);
        var h = RoundToPixel(size.Height, dpiScale);
        return new Size(Math.Max(0, w), Math.Max(0, h));
    }

    public static Rect RoundRectToPixels(Rect rect, double dpiScale)
    {
        if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
            return rect;

        if (rect.IsEmpty)
            return rect;

        // Round position and size independently (WPF-style) to avoid jitter introduced by
        // rounding both edges separately (left/right), which can change size by ±1px.
        int leftPx = RoundToPixelInt(rect.X, dpiScale);
        int topPx = RoundToPixelInt(rect.Y, dpiScale);
        int widthPx = RoundToPixelInt(rect.Width, dpiScale);
        int heightPx = RoundToPixelInt(rect.Height, dpiScale);

        double x = leftPx / dpiScale;
        double y = topPx / dpiScale;
        double w = Math.Max(0, widthPx / dpiScale);
        double h = Math.Max(0, heightPx / dpiScale);

        return new Rect(x, y, w, h);
    }

    public static int RoundToPixelInt(double value, double dpiScale)
    {
        if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);

        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0;

        return (int)Math.Round(value * dpiScale, MidpointRounding.AwayFromZero);
    }

    public static double RoundToPixel(double value, double dpiScale)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value;

        // WPF-style: avoid banker's rounding to reduce jitter at .5 boundaries (e.g. 150% DPI).
        return Math.Round(value * dpiScale, MidpointRounding.AwayFromZero) / dpiScale;
    }
}
