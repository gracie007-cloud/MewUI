using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Structs;

namespace Aprillz.MewUI.Rendering.Gdi.Core;

/// <summary>
/// Represents an off-screen DIB section used for anti-aliased rendering.
/// Wraps the memory DC, bitmap, and pixel buffer for easy management.
/// </summary>
internal sealed class AaSurface : IDisposable
{
    private nint _memDc;
    private nint _bitmap;
    private nint _oldBitmap;
    private nint _bits;
    private int _width;
    private int _height;
    private bool _disposed;

    /// <summary>
    /// Gets the memory device context handle.
    /// </summary>
    public nint MemDc => _memDc;

    /// <summary>
    /// Gets the pointer to the raw pixel data (32-bit BGRA, top-down).
    /// </summary>
    public nint Bits => _bits;

    /// <summary>
    /// Gets the width of the surface in pixels.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Gets the height of the surface in pixels.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Gets the stride (bytes per row) of the surface.
    /// </summary>
    public int Stride => _width * 4;

    /// <summary>
    /// Gets a value indicating whether this surface is valid and usable.
    /// </summary>
    public bool IsValid => _memDc != 0 && _bitmap != 0 && _bits != 0;

    /// <summary>
    /// Creates a new AaSurface with the specified dimensions.
    /// </summary>
    /// <param name="sourceDc">Source DC for creating compatible resources.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public AaSurface(nint sourceDc, int width, int height)
    {
        Create(sourceDc, width, height);
    }

    /// <summary>
    /// Creates an uninitialized surface (for pooling).
    /// </summary>
    internal AaSurface()
    {
    }

    private void Create(nint sourceDc, int width, int height)
    {
        _width = Math.Max(1, Math.Min(width, GdiRenderingConstants.MaxAaSurfaceSize));
        _height = Math.Max(1, Math.Min(height, GdiRenderingConstants.MaxAaSurfaceSize));

        _memDc = Gdi32.CreateCompatibleDC(sourceDc);
        if (_memDc == 0)
        {
            return;
        }

        var bmi = BITMAPINFO.Create32bpp(_width, _height);
        _bitmap = Gdi32.CreateDIBSection(sourceDc, ref bmi, 0, out _bits, 0, 0);

        if (_bitmap == 0 || _bits == 0)
        {
            Gdi32.DeleteDC(_memDc);
            _memDc = 0;
            return;
        }

        _oldBitmap = Gdi32.SelectObject(_memDc, _bitmap);
    }

    /// <summary>
    /// Resizes the surface if the requested size is different.
    /// </summary>
    /// <param name="sourceDc">Source DC for creating compatible resources.</param>
    /// <param name="width">New width in pixels.</param>
    /// <param name="height">New height in pixels.</param>
    /// <returns>True if resize was successful or not needed.</returns>
    public bool EnsureSize(nint sourceDc, int width, int height)
    {
        width = Math.Max(1, Math.Min(width, GdiRenderingConstants.MaxAaSurfaceSize));
        height = Math.Max(1, Math.Min(height, GdiRenderingConstants.MaxAaSurfaceSize));

        if (_width == width && _height == height && IsValid)
        {
            return true;
        }

        Release();
        Create(sourceDc, width, height);
        return IsValid;
    }

    /// <summary>
    /// Clears the pixel buffer to zero (transparent).
    /// </summary>
    public unsafe void Clear()
    {
        if (_bits == 0 || _width <= 0 || _height <= 0)
        {
            return;
        }

        var count = _width * _height * 4;
        new Span<byte>((void*)_bits, count).Clear();
    }

    /// <summary>
    /// Clears a specific region of the pixel buffer to zero.
    /// </summary>
    public unsafe void ClearRegion(int x, int y, int width, int height)
    {
        if (_bits == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        // Clamp to surface bounds
        if (x < 0) { width += x; x = 0; }
        if (y < 0) { height += y; y = 0; }
        if (x + width > _width) width = _width - x;
        if (y + height > _height) height = _height - y;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        int stride = Stride;
        byte* basePtr = (byte*)_bits;

        for (int py = 0; py < height; py++)
        {
            byte* row = basePtr + (y + py) * stride + x * 4;
            new Span<byte>(row, width * 4).Clear();
        }
    }

    /// <summary>
    /// Alpha blends this surface onto the target DC.
    /// </summary>
    public void AlphaBlendTo(nint targetDc, int destX, int destY)
    {
        AlphaBlendTo(targetDc, destX, destY, _width, _height, 0, 0);
    }

    /// <summary>
    /// Alpha blends a region of this surface onto the target DC.
    /// </summary>
    public void AlphaBlendTo(nint targetDc, int destX, int destY, int width, int height, int srcX, int srcY)
    {
        if (!IsValid || targetDc == 0)
        {
            return;
        }

        var blend = BLENDFUNCTION.SourceOver(255);
        Gdi32.AlphaBlend(targetDc, destX, destY, width, height, _memDc, srcX, srcY, width, height, blend);
    }

    /// <summary>
    /// Gets a span over the pixel data for direct manipulation.
    /// </summary>
    public unsafe Span<byte> GetPixelSpan()
    {
        if (_bits == 0)
        {
            return Span<byte>.Empty;
        }

        return new Span<byte>((void*)_bits, _width * _height * 4);
    }

    /// <summary>
    /// Gets a span over a single row of pixel data.
    /// </summary>
    public unsafe Span<byte> GetRowSpan(int y)
    {
        if (_bits == 0 || y < 0 || y >= _height)
        {
            return Span<byte>.Empty;
        }

        byte* rowPtr = (byte*)_bits + y * Stride;
        return new Span<byte>(rowPtr, _width * 4);
    }

    /// <summary>
    /// Gets a pointer to the start of a row.
    /// </summary>
    public unsafe byte* GetRowPointer(int y)
    {
        if (_bits == 0 || y < 0 || y >= _height)
        {
            return null;
        }

        return (byte*)_bits + y * Stride;
    }

    private void Release()
    {
        if (_memDc != 0)
        {
            if (_oldBitmap != 0)
            {
                Gdi32.SelectObject(_memDc, _oldBitmap);
                _oldBitmap = 0;
            }

            if (_bitmap != 0)
            {
                Gdi32.DeleteObject(_bitmap);
                _bitmap = 0;
            }

            Gdi32.DeleteDC(_memDc);
            _memDc = 0;
        }

        _bits = 0;
        _width = 0;
        _height = 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Release();
            _disposed = true;
        }
    }
}
