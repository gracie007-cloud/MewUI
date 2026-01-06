using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Constants;
using Aprillz.MewUI.Native.Structs;
using Aprillz.MewUI.Core;
using Aprillz.MewUI.Primitives;
using System.Diagnostics;
using System.IO;

namespace Aprillz.MewUI.Rendering.Gdi;

/// <summary>
/// GDI graphics context implementation.
/// </summary>
internal sealed class GdiGraphicsContext : IGraphicsContext
{
    private readonly nint _hwnd;
    private readonly bool _ownsDc;
    private readonly GdiCurveQuality _curveQuality;
    private readonly int _supersampleFactor;
    private readonly Stack<int> _savedStates = new();
    private double _translateX;
    private double _translateY;
    private nint _aaMemDc;
    private nint _aaBitmap;
    private nint _aaOldBitmap;
    private nint _aaBits;
    private int _aaWidth;
    private int _aaHeight;
    private bool _disposed;

    public double DpiScale { get; }

    internal nint Hdc { get; }

    public GdiGraphicsContext(nint hwnd, nint hdc, double dpiScale, GdiCurveQuality curveQuality, bool ownsDc = false)
    {
        _hwnd = hwnd;
        Hdc = hdc;
        _ownsDc = ownsDc;
        DpiScale = dpiScale;
        _curveQuality = curveQuality;
        _supersampleFactor = (int)curveQuality switch { 2 => 2, 3 => 3, _ => 1 };

        // Set default modes
        Gdi32.SetBkMode(Hdc, GdiConstants.TRANSPARENT);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsDc && Hdc != 0)
            {
                User32.ReleaseDC(_hwnd, Hdc);
            }

            if (_aaMemDc != 0)
            {
                if (_aaOldBitmap != 0)
                    Gdi32.SelectObject(_aaMemDc, _aaOldBitmap);
                if (_aaBitmap != 0)
                    Gdi32.DeleteObject(_aaBitmap);
                Gdi32.DeleteDC(_aaMemDc);

                _aaMemDc = 0;
                _aaBitmap = 0;
                _aaOldBitmap = 0;
                _aaBits = 0;
                _aaWidth = 0;
                _aaHeight = 0;
            }
            _disposed = true;
        }
    }

    #region State Management

    public void Save()
    {
        int state = Gdi32.SaveDC(Hdc);
        _savedStates.Push(state);
    }

    public void Restore()
    {
        if (_savedStates.Count > 0)
        {
            int state = _savedStates.Pop();
            Gdi32.RestoreDC(Hdc, state);
        }
    }

    public void SetClip(Rect rect)
    {
        var r = ToDeviceRect(rect);
        Gdi32.IntersectClipRect(Hdc, r.left, r.top, r.right, r.bottom);
    }

    public void Translate(double dx, double dy)
    {
        _translateX += dx;
        _translateY += dy;
    }

    #endregion

    #region Drawing Primitives

    public void Clear(Color color)
    {
        var brush = Gdi32.CreateSolidBrush(color.ToCOLORREF());
        try
        {
            User32.GetClientRect(_hwnd, out var rect);
            Gdi32.FillRect(Hdc, ref rect, brush);
        }
        finally
        {
            Gdi32.DeleteObject(brush);
        }
    }

    public void DrawLine(Point start, Point end, Color color, double thickness = 1)
    {
        var pen = Gdi32.CreatePen(GdiConstants.PS_SOLID, QuantizePenWidthPx(thickness), color.ToCOLORREF());
        var oldPen = Gdi32.SelectObject(Hdc, pen);
        try
        {
            var p1 = ToDevicePoint(start);
            var p2 = ToDevicePoint(end);
            Gdi32.MoveToEx(Hdc, p1.x, p1.y, out _);
            Gdi32.LineTo(Hdc, p2.x, p2.y);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldPen);
            Gdi32.DeleteObject(pen);
        }
    }

    public void DrawRectangle(Rect rect, Color color, double thickness = 1)
    {
        var pen = Gdi32.CreatePen(GdiConstants.PS_SOLID, QuantizePenWidthPx(thickness), color.ToCOLORREF());
        var nullBrush = Gdi32.GetStockObject(GdiConstants.NULL_BRUSH);
        var oldPen = Gdi32.SelectObject(Hdc, pen);
        var oldBrush = Gdi32.SelectObject(Hdc, nullBrush);
        try
        {
            var r = ToDeviceRect(rect);
            Gdi32.Rectangle(Hdc, r.left, r.top, r.right, r.bottom);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldPen);
            Gdi32.SelectObject(Hdc, oldBrush);
            Gdi32.DeleteObject(pen);
        }
    }

    public void FillRectangle(Rect rect, Color color)
    {
        var brush = Gdi32.CreateSolidBrush(color.ToCOLORREF());
        try
        {
            var r = ToDeviceRect(rect);
            Gdi32.FillRect(Hdc, ref r, brush);
        }
        finally
        {
            Gdi32.DeleteObject(brush);
        }
    }

    public void DrawRoundedRectangle(Rect rect, double radiusX, double radiusY, Color color, double thickness = 1)
    {
        if (_curveQuality != GdiCurveQuality.Fast && color.A > 0 && thickness > 0)
        {
            DrawRoundedRectangleSmooth(rect, radiusX, radiusY, color, thickness);
            return;
        }

        var pen = Gdi32.CreatePen(GdiConstants.PS_SOLID, QuantizePenWidthPx(thickness), color.ToCOLORREF());
        var nullBrush = Gdi32.GetStockObject(GdiConstants.NULL_BRUSH);
        var oldPen = Gdi32.SelectObject(Hdc, pen);
        var oldBrush = Gdi32.SelectObject(Hdc, nullBrush);
        try
        {
            var r = ToDeviceRect(rect);
            int rx = QuantizeLengthPx(radiusX);
            int ry = QuantizeLengthPx(radiusY);
            Gdi32.RoundRect(Hdc, r.left, r.top, r.right, r.bottom, rx * 2, ry * 2);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldPen);
            Gdi32.SelectObject(Hdc, oldBrush);
            Gdi32.DeleteObject(pen);
        }
    }

    public void FillRoundedRectangle(Rect rect, double radiusX, double radiusY, Color color)
    {
        if (_curveQuality != GdiCurveQuality.Fast && color.A > 0)
        {
            FillRoundedRectangleSmooth(rect, radiusX, radiusY, color);
            return;
        }

        var brush = Gdi32.CreateSolidBrush(color.ToCOLORREF());
        var nullPen = Gdi32.GetStockObject(GdiConstants.NULL_PEN);
        var oldBrush = Gdi32.SelectObject(Hdc, brush);
        var oldPen = Gdi32.SelectObject(Hdc, nullPen);
        try
        {
            var r = ToDeviceRect(rect);
            int rx = QuantizeLengthPx(radiusX);
            int ry = QuantizeLengthPx(radiusY);
            // Add 1 to compensate for NULL_PEN
            Gdi32.RoundRect(Hdc, r.left, r.top, r.right + 1, r.bottom + 1, rx * 2, ry * 2);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldPen);
            Gdi32.SelectObject(Hdc, oldBrush);
            Gdi32.DeleteObject(brush);
        }
    }

    public void DrawEllipse(Rect bounds, Color color, double thickness = 1)
    {
        if (_curveQuality != GdiCurveQuality.Fast && color.A > 0 && thickness > 0)
        {
            DrawEllipseSmooth(bounds, color, thickness);
            return;
        }

        var pen = Gdi32.CreatePen(GdiConstants.PS_SOLID, QuantizePenWidthPx(thickness), color.ToCOLORREF());
        var nullBrush = Gdi32.GetStockObject(GdiConstants.NULL_BRUSH);
        var oldPen = Gdi32.SelectObject(Hdc, pen);
        var oldBrush = Gdi32.SelectObject(Hdc, nullBrush);
        try
        {
            var r = ToDeviceRect(bounds);
            Gdi32.Ellipse(Hdc, r.left, r.top, r.right, r.bottom);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldPen);
            Gdi32.SelectObject(Hdc, oldBrush);
            Gdi32.DeleteObject(pen);
        }
    }

    public void FillEllipse(Rect bounds, Color color)
    {
        if (_curveQuality != GdiCurveQuality.Fast && color.A > 0)
        {
            FillEllipseSmooth(bounds, color);
            return;
        }

        var brush = Gdi32.CreateSolidBrush(color.ToCOLORREF());
        var nullPen = Gdi32.GetStockObject(GdiConstants.NULL_PEN);
        var oldBrush = Gdi32.SelectObject(Hdc, brush);
        var oldPen = Gdi32.SelectObject(Hdc, nullPen);
        try
        {
            var r = ToDeviceRect(bounds);
            Gdi32.Ellipse(Hdc, r.left, r.top, r.right + 1, r.bottom + 1);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldPen);
            Gdi32.SelectObject(Hdc, oldBrush);
            Gdi32.DeleteObject(brush);
        }
    }

    #endregion

    #region Text Rendering

    public void DrawText(string text, Point location, IFont font, Color color)
    {
        if (font is not GdiFont gdiFont)
            throw new ArgumentException("Font must be a GdiFont", nameof(font));

        var oldFont = Gdi32.SelectObject(Hdc, gdiFont.Handle);
        var oldColor = Gdi32.SetTextColor(Hdc, color.ToCOLORREF());
        try
        {
            var pt = ToDevicePoint(location);
            Gdi32.TextOut(Hdc, pt.x, pt.y, text, text.Length);
        }
        finally
        {
            Gdi32.SetTextColor(Hdc, oldColor);
            Gdi32.SelectObject(Hdc, oldFont);
        }
    }

    public void DrawText(string text, Rect bounds, IFont font, Color color,
        TextAlignment horizontalAlignment = TextAlignment.Left,
        TextAlignment verticalAlignment = TextAlignment.Top,
        TextWrapping wrapping = TextWrapping.NoWrap)
    {
        if (font is not GdiFont gdiFont)
            throw new ArgumentException("Font must be a GdiFont", nameof(font));

        var oldFont = Gdi32.SelectObject(Hdc, gdiFont.Handle);
        var oldColor = Gdi32.SetTextColor(Hdc, color.ToCOLORREF());
        try
        {
            var r = ToDeviceRect(bounds);
            uint format = GdiConstants.DT_NOPREFIX;

            format |= horizontalAlignment switch
            {
                TextAlignment.Left => GdiConstants.DT_LEFT,
                TextAlignment.Center => GdiConstants.DT_CENTER,
                TextAlignment.Right => GdiConstants.DT_RIGHT,
                _ => GdiConstants.DT_LEFT
            };

            if (wrapping == TextWrapping.NoWrap)
            {
                format |= GdiConstants.DT_SINGLELINE;

                format |= verticalAlignment switch
                {
                    TextAlignment.Top => GdiConstants.DT_TOP,
                    TextAlignment.Center => GdiConstants.DT_VCENTER,
                    TextAlignment.Bottom => GdiConstants.DT_BOTTOM,
                    _ => GdiConstants.DT_TOP
                };
            }
            else
            {
                format |= GdiConstants.DT_WORDBREAK;
            }

            Gdi32.DrawText(Hdc, text, text.Length, ref r, format);
        }
        finally
        {
            Gdi32.SetTextColor(Hdc, oldColor);
            Gdi32.SelectObject(Hdc, oldFont);
        }
    }

    public Size MeasureText(string text, IFont font)
    {
        if (font is not GdiFont gdiFont)
            throw new ArgumentException("Font must be a GdiFont", nameof(font));

        var oldFont = Gdi32.SelectObject(Hdc, gdiFont.Handle);
        try
        {
            if (string.IsNullOrEmpty(text))
                return Size.Empty;

            var hasLineBreaks = text.AsSpan().IndexOfAny('\r', '\n') >= 0;
            var rect = hasLineBreaks
                ? new RECT(0, 0, QuantizeLengthPx(1_000_000), 0)
                : new RECT(0, 0, 0, 0);

            uint format = hasLineBreaks
                ? GdiConstants.DT_CALCRECT | GdiConstants.DT_WORDBREAK | GdiConstants.DT_NOPREFIX
                : GdiConstants.DT_CALCRECT | GdiConstants.DT_SINGLELINE | GdiConstants.DT_NOPREFIX;

            Gdi32.DrawText(Hdc, text, text.Length, ref rect, format);
            return new Size(rect.Width / DpiScale, rect.Height / DpiScale);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldFont);
        }
    }

    public Size MeasureText(string text, IFont font, double maxWidth)
    {
        if (font is not GdiFont gdiFont)
            throw new ArgumentException("Font must be a GdiFont", nameof(font));

        var oldFont = Gdi32.SelectObject(Hdc, gdiFont.Handle);
        try
        {
            if (double.IsNaN(maxWidth) || maxWidth <= 0 || double.IsInfinity(maxWidth))
                maxWidth = 1_000_000;

            var rect = new RECT(0, 0, QuantizeLengthPx(maxWidth), 0);
            Gdi32.DrawText(Hdc, text, text.Length, ref rect,
                GdiConstants.DT_CALCRECT | GdiConstants.DT_WORDBREAK | GdiConstants.DT_NOPREFIX);
            return new Size(rect.Width / DpiScale, rect.Height / DpiScale);
        }
        finally
        {
            Gdi32.SelectObject(Hdc, oldFont);
        }
    }

    #endregion

    #region Image Rendering

    public void DrawImage(IImage image, Point location)
    {
        if (image is not GdiImage gdiImage)
            throw new ArgumentException("Image must be a GdiImage", nameof(image));

        DrawImage(gdiImage, new Rect(location.X, location.Y, image.PixelWidth, image.PixelHeight));
    }

    public void DrawImage(IImage image, Rect destRect)
    {
        if (image is not GdiImage gdiImage)
            throw new ArgumentException("Image must be a GdiImage", nameof(image));

        DrawImage(gdiImage, destRect, new Rect(0, 0, image.PixelWidth, image.PixelHeight));
    }

    public void DrawImage(IImage image, Rect destRect, Rect sourceRect)
    {
        if (image is not GdiImage gdiImage)
            throw new ArgumentException("Image must be a GdiImage", nameof(image));

        var memDc = Gdi32.CreateCompatibleDC(Hdc);
        var oldBitmap = Gdi32.SelectObject(memDc, gdiImage.Handle);
        try
        {
            var dest = ToDeviceRect(destRect);
            int srcX = (int)sourceRect.X;
            int srcY = (int)sourceRect.Y;
            int srcW = (int)sourceRect.Width;
            int srcH = (int)sourceRect.Height;

            // Use alpha blending for 32-bit images
            var blend = BLENDFUNCTION.SourceOver(255);
            Gdi32.AlphaBlend(
                Hdc, dest.left, dest.top, dest.Width, dest.Height,
                memDc, srcX, srcY, srcW, srcH,
                blend);
        }
        finally
        {
            Gdi32.SelectObject(memDc, oldBitmap);
            Gdi32.DeleteDC(memDc);
        }
    }

    #endregion

    #region Helper Methods

    private int QuantizePenWidthPx(double thicknessDip)
    {
        if (thicknessDip <= 0 || double.IsNaN(thicknessDip) || double.IsInfinity(thicknessDip))
            return 0;

        var px = thicknessDip * DpiScale;
        var snapped = (int)Math.Round(px, MidpointRounding.AwayFromZero);
        return Math.Max(1, snapped);
    }

    private int QuantizeLengthPx(double lengthDip)
    {
        if (lengthDip <= 0 || double.IsNaN(lengthDip) || double.IsInfinity(lengthDip))
            return 0;

        return LayoutRounding.RoundToPixelInt(lengthDip, DpiScale);
    }

    private POINT ToDevicePoint(Point pt) => new POINT(
            LayoutRounding.RoundToPixelInt(pt.X + _translateX, DpiScale),
            LayoutRounding.RoundToPixelInt(pt.Y + _translateY, DpiScale)
        );

    private RECT ToDeviceRect(Rect rect) => RECT.FromLTRB(
            LayoutRounding.RoundToPixelInt(rect.X + _translateX, DpiScale),
            LayoutRounding.RoundToPixelInt(rect.Y + _translateY, DpiScale),
            LayoutRounding.RoundToPixelInt(rect.Right + _translateX, DpiScale),
            LayoutRounding.RoundToPixelInt(rect.Bottom + _translateY, DpiScale)
        );

    private void EnsureAaSurface(int width, int height)
    {
        if (width <= 0) width = 1;
        if (height <= 0) height = 1;

        if (_aaMemDc != 0 && _aaBitmap != 0 && _aaWidth == width && _aaHeight == height && _aaBits != 0)
            return;

        if (_aaMemDc != 0)
        {
            if (_aaOldBitmap != 0)
                Gdi32.SelectObject(_aaMemDc, _aaOldBitmap);
            if (_aaBitmap != 0)
                Gdi32.DeleteObject(_aaBitmap);
            Gdi32.DeleteDC(_aaMemDc);
        }

        _aaMemDc = Gdi32.CreateCompatibleDC(Hdc);
        var bmi = BITMAPINFO.Create32bpp(width, height);
        _aaBitmap = Gdi32.CreateDIBSection(Hdc, ref bmi, 0, out _aaBits, 0, 0);
        _aaOldBitmap = Gdi32.SelectObject(_aaMemDc, _aaBitmap);
        _aaWidth = width;
        _aaHeight = height;
    }

    private void AlphaBlendBitmap(int destX, int destY, int width, int height)
    {
        var blend = BLENDFUNCTION.SourceOver(255);
        Gdi32.AlphaBlend(Hdc, destX, destY, width, height, _aaMemDc, 0, 0, width, height, blend);
    }

    private unsafe void ClearAaBits(int width, int height)
    {
        if (_aaBits == 0)
            return;

        var count = (nuint)(width * height * 4);
        new Span<byte>((void*)_aaBits, checked((int)count)).Clear();
    }

    private unsafe void FillRoundedRectangleSmooth(Rect rect, double radiusX, double radiusY, Color color)
    {
        var dst = ToDeviceRect(rect);
        if (dst.Width <= 0 || dst.Height <= 0)
            return;

        EnsureAaSurface(dst.Width, dst.Height);
        ClearAaBits(dst.Width, dst.Height);

        int s = _supersampleFactor;
        double w = dst.Width;
        double h = dst.Height;
        double rx = Math.Max(0, radiusX * DpiScale);
        double ry = Math.Max(0, radiusY * DpiScale);
        double rx2 = rx * rx;
        double ry2 = ry * ry;

        byte srcA = color.A;
        byte srcR = color.R;
        byte srcG = color.G;
        byte srcB = color.B;

        int samples = s * s;
        byte* row = (byte*)_aaBits;
        int stride = dst.Width * 4;

        for (int py = 0; py < dst.Height; py++)
        {
            byte* p = row + py * stride;
            for (int px = 0; px < dst.Width; px++)
            {
                int covered = 0;
                for (int sy = 0; sy < s; sy++)
                {
                    double y = py + (sy + 0.5) / s;
                    for (int sx = 0; sx < s; sx++)
                    {
                        double x = px + (sx + 0.5) / s;

                        if (rx <= 0 || ry <= 0)
                        {
                            covered++;
                            continue;
                        }

                        double cx = x < rx ? rx : (x > w - rx ? w - rx : x);
                        double cy = y < ry ? ry : (y > h - ry ? h - ry : y);
                        double dx = x - cx;
                        double dy = y - cy;
                        if ((dx * dx) / rx2 + (dy * dy) / ry2 <= 1.0)
                            covered++;
                    }
                }

                if (covered == 0)
                {
                    p += 4;
                    continue;
                }

                double cov = (double)covered / samples;
                int a = (int)Math.Round(srcA * cov, MidpointRounding.AwayFromZero);
                if (a <= 0)
                {
                    p += 4;
                    continue;
                }

                int pr = (srcR * a + 127) / 255;
                int pg = (srcG * a + 127) / 255;
                int pb = (srcB * a + 127) / 255;

                p[0] = (byte)pb;
                p[1] = (byte)pg;
                p[2] = (byte)pr;
                p[3] = (byte)a;
                p += 4;
            }
        }

        DumpBeforeBlendIfEnabled("FillRoundRect", dst.left, dst.top, dst.Width, dst.Height, dst.Width, dst.Height);
        AlphaBlendBitmap(dst.left, dst.top, dst.Width, dst.Height);
        DumpAfterBlendIfEnabled("FillRoundRect", dst.left, dst.top, dst.Width, dst.Height);
    }

    private unsafe void DrawRoundedRectangleSmooth(Rect rect, double radiusX, double radiusY, Color color, double thicknessDip)
    {
        var strokePx = QuantizePenWidthPx(thicknessDip);
        if (strokePx <= 0)
            return;

        var dst = ToDeviceRect(rect);
        if (dst.Width <= 0 || dst.Height <= 0)
            return;

        int pad = (int)Math.Ceiling(strokePx / 2.0);
        int outW = dst.Width + pad * 2;
        int outH = dst.Height + pad * 2;

        EnsureAaSurface(outW, outH);
        ClearAaBits(outW, outH);

        int s = _supersampleFactor;
        double w = dst.Width;
        double h = dst.Height;
        double rx = Math.Max(0, radiusX * DpiScale);
        double ry = Math.Max(0, radiusY * DpiScale);
        double half = strokePx / 2.0;

        // Outer shape
        double wOut = w + strokePx;
        double hOut = h + strokePx;
        double rxOut = Math.Max(0, rx + half);
        double ryOut = Math.Max(0, ry + half);
        double rxOut2 = rxOut * rxOut;
        double ryOut2 = ryOut * ryOut;

        // Inner shape
        double wIn = Math.Max(0, w - strokePx);
        double hIn = Math.Max(0, h - strokePx);
        double rxIn = Math.Max(0, rx - half);
        double ryIn = Math.Max(0, ry - half);
        double rxIn2 = rxIn * rxIn;
        double ryIn2 = ryIn * ryIn;

        byte srcA = color.A;
        byte srcR = color.R;
        byte srcG = color.G;
        byte srcB = color.B;

        int samples = s * s;
        byte* row = (byte*)_aaBits;
        int stride = outW * 4;

        for (int py = 0; py < outH; py++)
        {
            byte* p = row + py * stride;
            for (int px = 0; px < outW; px++)
            {
                int covered = 0;
                for (int sy = 0; sy < s; sy++)
                {
                    double y = (py + (sy + 0.5) / s) - pad;
                    for (int sx = 0; sx < s; sx++)
                    {
                        double x = (px + (sx + 0.5) / s) - pad;

                        bool inOuter = InsideRoundedRect(x + half, y + half, wOut, hOut, rxOut, ryOut, rxOut2, ryOut2);
                        if (!inOuter)
                            continue;

                        bool inInner = wIn > 0 && hIn > 0
                            ? InsideRoundedRect(x - half, y - half, wIn, hIn, rxIn, ryIn, rxIn2, ryIn2)
                            : false;

                        if (inOuter && !inInner)
                            covered++;
                    }
                }

                if (covered == 0)
                {
                    p += 4;
                    continue;
                }

                double cov = (double)covered / samples;
                int a = (int)Math.Round(srcA * cov, MidpointRounding.AwayFromZero);
                if (a <= 0)
                {
                    p += 4;
                    continue;
                }

                int pr = (srcR * a + 127) / 255;
                int pg = (srcG * a + 127) / 255;
                int pb = (srcB * a + 127) / 255;

                p[0] = (byte)pb;
                p[1] = (byte)pg;
                p[2] = (byte)pr;
                p[3] = (byte)a;
                p += 4;
            }
        }

        DumpBeforeBlendIfEnabled("DrawRoundRect", dst.left - pad, dst.top - pad, outW, outH, outW, outH);
        AlphaBlendBitmap(dst.left - pad, dst.top - pad, outW, outH);
        DumpAfterBlendIfEnabled("DrawRoundRect", dst.left - pad, dst.top - pad, outW, outH);
    }

    private static bool InsideRoundedRect(double x, double y, double w, double h, double rx, double ry, double rx2, double ry2)
    {
        if (w <= 0 || h <= 0)
            return false;

        if (x < 0 || y < 0 || x > w || y > h)
            return false;

        if (rx <= 0 || ry <= 0)
            return true;

        double cx = x < rx ? rx : (x > w - rx ? w - rx : x);
        double cy = y < ry ? ry : (y > h - ry ? h - ry : y);
        double dx = x - cx;
        double dy = y - cy;
        return (dx * dx) / rx2 + (dy * dy) / ry2 <= 1.0;
    }

    private unsafe void FillEllipseSmooth(Rect bounds, Color color)
    {
        var dst = ToDeviceRect(bounds);
        if (dst.Width <= 0 || dst.Height <= 0)
            return;

        EnsureAaSurface(dst.Width, dst.Height);
        ClearAaBits(dst.Width, dst.Height);

        int s = _supersampleFactor;
        double w = dst.Width;
        double h = dst.Height;
        double rx = w / 2.0;
        double ry = h / 2.0;
        double rx2 = rx * rx;
        double ry2 = ry * ry;
        double cx = rx;
        double cy = ry;

        byte srcA = color.A;
        byte srcR = color.R;
        byte srcG = color.G;
        byte srcB = color.B;

        int samples = s * s;
        byte* row = (byte*)_aaBits;
        int stride = dst.Width * 4;

        for (int py = 0; py < dst.Height; py++)
        {
            byte* p = row + py * stride;
            for (int px = 0; px < dst.Width; px++)
            {
                int covered = 0;
                for (int sy = 0; sy < s; sy++)
                {
                    double y = py + (sy + 0.5) / s;
                    double dy = y - cy;
                    for (int sx = 0; sx < s; sx++)
                    {
                        double x = px + (sx + 0.5) / s;
                        double dx = x - cx;
                        if ((dx * dx) / rx2 + (dy * dy) / ry2 <= 1.0)
                            covered++;
                    }
                }

                if (covered == 0)
                {
                    p += 4;
                    continue;
                }

                double cov = (double)covered / samples;
                int a = (int)Math.Round(srcA * cov, MidpointRounding.AwayFromZero);
                if (a <= 0)
                {
                    p += 4;
                    continue;
                }

                int pr = (srcR * a + 127) / 255;
                int pg = (srcG * a + 127) / 255;
                int pb = (srcB * a + 127) / 255;

                p[0] = (byte)pb;
                p[1] = (byte)pg;
                p[2] = (byte)pr;
                p[3] = (byte)a;
                p += 4;
            }
        }

        DumpBeforeBlendIfEnabled("FillEllipse", dst.left, dst.top, dst.Width, dst.Height, dst.Width, dst.Height);
        AlphaBlendBitmap(dst.left, dst.top, dst.Width, dst.Height);
        DumpAfterBlendIfEnabled("FillEllipse", dst.left, dst.top, dst.Width, dst.Height);
    }

    private unsafe void DrawEllipseSmooth(Rect bounds, Color color, double thicknessDip)
    {
        var strokePx = QuantizePenWidthPx(thicknessDip);
        if (strokePx <= 0)
            return;

        var dst = ToDeviceRect(bounds);
        if (dst.Width <= 0 || dst.Height <= 0)
            return;

        int pad = (int)Math.Ceiling(strokePx / 2.0);
        int outW = dst.Width + pad * 2;
        int outH = dst.Height + pad * 2;

        EnsureAaSurface(outW, outH);
        ClearAaBits(outW, outH);

        int s = _supersampleFactor;
        double w = dst.Width;
        double h = dst.Height;

        double half = strokePx / 2.0;
        double rxOut = (w + strokePx) / 2.0;
        double ryOut = (h + strokePx) / 2.0;
        double rxOut2 = rxOut * rxOut;
        double ryOut2 = ryOut * ryOut;

        double rxIn = Math.Max(0, (w - strokePx) / 2.0);
        double ryIn = Math.Max(0, (h - strokePx) / 2.0);
        double rxIn2 = rxIn * rxIn;
        double ryIn2 = ryIn * ryIn;

        double cx = w / 2.0;
        double cy = h / 2.0;

        byte srcA = color.A;
        byte srcR = color.R;
        byte srcG = color.G;
        byte srcB = color.B;

        int samples = s * s;
        byte* row = (byte*)_aaBits;
        int stride = outW * 4;

        for (int py = 0; py < outH; py++)
        {
            byte* p = row + py * stride;
            for (int px = 0; px < outW; px++)
            {
                int covered = 0;
                for (int sy = 0; sy < s; sy++)
                {
                    double y = (py + (sy + 0.5) / s) - pad;
                    double dyOut = (y - cy) + 0;
                    for (int sx = 0; sx < s; sx++)
                    {
                        double x = (px + (sx + 0.5) / s) - pad;
                        double dxOut = (x - cx) + 0;

                        // Outer ellipse centered around original bounds, but expanded by half stroke.
                        double xo = dxOut;
                        double yo = dyOut;
                        bool inOuter = ((xo * xo) / rxOut2 + (yo * yo) / ryOut2) <= 1.0;
                        if (!inOuter)
                            continue;

                        bool inInner = rxIn > 0 && ryIn > 0 && ((xo * xo) / rxIn2 + (yo * yo) / ryIn2) <= 1.0;
                        if (inOuter && !inInner)
                            covered++;
                    }
                }

                if (covered == 0)
                {
                    p += 4;
                    continue;
                }

                double cov = (double)covered / samples;
                int a = (int)Math.Round(srcA * cov, MidpointRounding.AwayFromZero);
                if (a <= 0)
                {
                    p += 4;
                    continue;
                }

                int pr = (srcR * a + 127) / 255;
                int pg = (srcG * a + 127) / 255;
                int pb = (srcB * a + 127) / 255;

                p[0] = (byte)pb;
                p[1] = (byte)pg;
                p[2] = (byte)pr;
                p[3] = (byte)a;
                p += 4;
            }
        }

        DumpBeforeBlendIfEnabled("DrawEllipse", dst.left - pad, dst.top - pad, outW, outH, outW, outH);
        AlphaBlendBitmap(dst.left - pad, dst.top - pad, outW, outH);
        DumpAfterBlendIfEnabled("DrawEllipse", dst.left - pad, dst.top - pad, outW, outH);
    }

    [Conditional("DEBUG")]
    private unsafe void DumpBeforeBlendIfEnabled(string tag, int destX, int destY, int destW, int destH, int aaW, int aaH)
    {
        var stages = GdiDebug.DumpStages;
        if (stages == GdiDumpStages.None)
            return;

        if ((stages & GdiDumpStages.DestBeforeBlend) != 0)
            CaptureHdcRegionIfEnabled(tag, "dest_before", destX, destY, destW, destH);

        if (_aaBits == 0 || aaW <= 0 || aaH <= 0)
            return;

        if ((stages & GdiDumpStages.AaSurface) != 0)
            DumpAaSurfaceIfEnabled(tag, "aa", aaW, aaH);

        if ((stages & GdiDumpStages.AaSurfaceAlpha) != 0)
            DumpAaSurfaceAlphaIfEnabled(tag, "aa_alpha", aaW, aaH);
    }

    [Conditional("DEBUG")]
    private void DumpAfterBlendIfEnabled(string tag, int destX, int destY, int destW, int destH)
    {
        if ((GdiDebug.DumpStages & GdiDumpStages.DestAfterBlend) == 0)
            return;

        CaptureHdcRegionIfEnabled(tag, "dest_after", destX, destY, destW, destH);
    }

    private static unsafe void WriteBmpBgra32(string filePath, int width, int height, byte* bgraTopDown)
    {
        int stride = width * 4;
        int imageSize = stride * height;
        const int headerSize = 14 + 40;
        int fileSize = headerSize + imageSize;

        Span<byte> header = stackalloc byte[headerSize];
        header.Clear();

        // BITMAPFILEHEADER
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        WriteInt32LE(header, 2, fileSize);
        WriteInt32LE(header, 10, headerSize);

        // BITMAPINFOHEADER
        WriteInt32LE(header, 14, 40);
        WriteInt32LE(header, 18, width);
        WriteInt32LE(header, 22, height); // bottom-up for broad viewer support
        WriteInt16LE(header, 26, 1);
        WriteInt16LE(header, 28, 32);
        WriteInt32LE(header, 34, imageSize);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        stream.Write(header);

        // Convert top-down BGRA to bottom-up rows.
        for (int y = height - 1; y >= 0; y--)
        {
            var row = new ReadOnlySpan<byte>(bgraTopDown + y * stride, stride);
            stream.Write(row);
        }
    }

    private static void WriteInt16LE(Span<byte> buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteInt32LE(Span<byte> buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    [Conditional("DEBUG")]
    private unsafe void DumpAaSurfaceIfEnabled(string tag, string stage, int width, int height)
    {
        if (_aaBits == 0 || width <= 0 || height <= 0)
            return;

        if (!GdiDebug.TryGetNextAaDumpIndex(out var index))
            return;

        var dir = GdiDebug.DumpAaDirectory!;
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, $"gdi_{index:0000}_{tag}_{stage}_{width}x{height}.bmp");
        WriteBmpBgra32(filePath, width, height, (byte*)_aaBits);
    }

    [Conditional("DEBUG")]
    private unsafe void DumpAaSurfaceAlphaIfEnabled(string tag, string stage, int width, int height)
    {
        if (_aaBits == 0 || width <= 0 || height <= 0)
            return;

        if (!GdiDebug.TryGetNextAaDumpIndex(out var index))
            return;

        int len = checked(width * height * 4);
        var tmp = new byte[len];
        byte* src = (byte*)_aaBits;

        for (int i = 0; i < len; i += 4)
        {
            byte a = src[i + 3];
            tmp[i + 0] = a;
            tmp[i + 1] = a;
            tmp[i + 2] = a;
            tmp[i + 3] = 255;
        }

        var dir = GdiDebug.DumpAaDirectory!;
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"gdi_{index:0000}_{tag}_{stage}_{width}x{height}.bmp");

        fixed (byte* p = tmp)
        {
            WriteBmpBgra32(filePath, width, height, p);
        }
    }

    [Conditional("DEBUG")]
    private unsafe void CaptureHdcRegionIfEnabled(string tag, string stage, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0)
            return;

        if (!GdiDebug.TryGetNextAaDumpIndex(out var index))
            return;

        var dir = GdiDebug.DumpAaDirectory!;
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"gdi_{index:0000}_{tag}_{stage}_{w}x{h}.bmp");

        nint memDc = 0;
        nint bmp = 0;
        nint old = 0;
        nint bits = 0;

        try
        {
            memDc = Gdi32.CreateCompatibleDC(Hdc);
            var bmi = BITMAPINFO.Create32bpp(w, h);
            bmp = Gdi32.CreateDIBSection(Hdc, ref bmi, 0, out bits, 0, 0);
            if (bmp == 0 || bits == 0)
                return;

            old = Gdi32.SelectObject(memDc, bmp);
            Gdi32.BitBlt(memDc, 0, 0, w, h, Hdc, x, y, 0x00CC0020); // SRCCOPY

            WriteBmpBgra32(filePath, w, h, (byte*)bits);
        }
        finally
        {
            if (memDc != 0)
            {
                if (old != 0)
                    Gdi32.SelectObject(memDc, old);
                if (bmp != 0)
                    Gdi32.DeleteObject(bmp);
                Gdi32.DeleteDC(memDc);
            }
        }
    }

    #endregion
}
