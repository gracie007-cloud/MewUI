using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Structs;
using Aprillz.MewUI.Resources;
using Aprillz.MewUI.Rendering.Gdi.Simd;

namespace Aprillz.MewUI.Rendering.Gdi;

/// <summary>
/// GDI bitmap image implementation.
/// </summary>
internal sealed class GdiImage : IImage
{
    private nint _bits;
    private bool _disposed;
    private readonly Dictionary<ScaledKey, nint> _scaled = new();
    private readonly IPixelBufferSource? _source;
    private int _sourceVersion = -1;

    public int PixelWidth { get; }
    public int PixelHeight { get; }

    internal nint Handle { get; private set; }
    internal nint Bits => _bits;

    /// <summary>
    /// Creates a 32-bit ARGB bitmap.
    /// </summary>
    public GdiImage(int width, int height)
    {
        PixelWidth = width;
        PixelHeight = height;

        var bmi = BITMAPINFO.Create32bpp(width, height);

        nint screenDc = User32.GetDC(0);
        try
        {
            Handle = Gdi32.CreateDIBSection(screenDc, ref bmi, 0, out _bits, 0, 0);
            if (Handle == 0)
            {
                throw new InvalidOperationException("Failed to create DIB section");
            }
        }
        finally
        {
            User32.ReleaseDC(0, screenDc);
        }
    }

    public GdiImage(IPixelBufferSource source) : this(
        source?.PixelWidth ?? throw new ArgumentNullException(nameof(source)),
        source.PixelHeight)
    {
        if (source.PixelFormat != BitmapPixelFormat.Bgra32)
        {
            throw new NotSupportedException($"Unsupported pixel format: {source.PixelFormat}");
        }

        _source = source;
        _sourceVersion = -1;
        EnsureUpToDate();
    }

    /// <summary>
    /// Creates an image from raw pixel data (BGRA format).
    /// </summary>
    public GdiImage(int width, int height, byte[] pixelData) : this(width, height)
    {
        if (pixelData.Length != width * height * 4)
        {
            throw new ArgumentException("Invalid pixel data size", nameof(pixelData));
        }

        if (pixelData.Length == 0)
        {
            return;
        }

        CopyToDibPremultiplied(pixelData);
    }

    internal void EnsureUpToDate()
    {
        if (_disposed || Handle == 0 || _bits == 0 || _source == null)
        {
            return;
        }

        int v = _source.Version;
        if (_sourceVersion == v)
        {
            return;
        }

        using (var l = _source.Lock())
        {
            v = l.Version;
            if (_sourceVersion == v)
            {
                return;
            }

            if (l.Buffer.Length == 0)
            {
                return;
            }

            CopyToDibPremultiplied(l.Buffer);
            _sourceVersion = v;

            foreach (var kvp in _scaled)
            {
                Gdi32.DeleteObject(kvp.Value);
            }

            _scaled.Clear();
        }
    }

    public void Dispose()
    {
        if (!_disposed && Handle != 0)
        {
            foreach (var kvp in _scaled)
            {
                Gdi32.DeleteObject(kvp.Value);
            }

            _scaled.Clear();

            Gdi32.DeleteObject(Handle);
            Handle = 0;
            _bits = 0;
            _disposed = true;
        }
    }

    private readonly record struct ScaledKey(int SrcX, int SrcY, int SrcW, int SrcH, int DestW, int DestH, ImageScaleQuality Quality);

    public bool TryGetOrCreateScaledBitmap(
        int srcX,
        int srcY,
        int srcW,
        int srcH,
        int destW,
        int destH,
        ImageScaleQuality quality,
        out nint scaledBitmap)
    {
        scaledBitmap = 0;
        if (_disposed || Handle == 0 || _bits == 0)
        {
            return false;
        }

        EnsureUpToDate();

        if (srcW <= 0 || srcH <= 0 || destW <= 0 || destH <= 0)
        {
            return false;
        }

        // This cache is only intended for resampling paths. Nearest-neighbor uses GDI stretch directly.
        if (quality == ImageScaleQuality.Default || quality == ImageScaleQuality.NearestNeighbor)
        {
            quality = ImageScaleQuality.Linear;
        }

        // Clamp to image bounds.
        if (srcX < 0)
        {
            srcX = 0;
        }

        if (srcY < 0)
        {
            srcY = 0;
        }

        if (srcX + srcW > PixelWidth)
        {
            srcW = PixelWidth - srcX;
        }

        if (srcY + srcH > PixelHeight)
        {
            srcH = PixelHeight - srcY;
        }

        if (srcW <= 0 || srcH <= 0)
        {
            return false;
        }

        var key = new ScaledKey(srcX, srcY, srcW, srcH, destW, destH, quality);
        if (_scaled.TryGetValue(key, out var existing) && existing != 0)
        {
            scaledBitmap = existing;
            return true;
        }

        var bmi = BITMAPINFO.Create32bpp(destW, destH);
        nint screenDc = User32.GetDC(0);
        nint dstBits = 0;
        nint dstBitmap = 0;
        try
        {
            dstBitmap = Gdi32.CreateDIBSection(screenDc, ref bmi, 0, out dstBits, 0, 0);
            if (dstBitmap == 0 || dstBits == 0)
            {
                return false;
            }

            unsafe
            {
                byte* srcBase = (byte*)_bits + (srcY * PixelWidth + srcX) * 4;
                byte* dstBase = (byte*)dstBits;

                // Fast path: integer downscale (e.g. 256 -> 32).
                if (srcW % destW == 0 && srcH % destH == 0 && destW <= srcW && destH <= srcH)
                {
                    int fx = srcW / destW;
                    int fy = srcH / destH;
                    int blockCount = fx * fy;

                    // Common case: 2x downsample in both dimensions.
                    if (fx == 2 && fy == 2)
                    {
                        GdiSimdDispatcher.Downsample2xBoxPremultipliedBgra(
                            srcBase,
                            srcStrideBytes: PixelWidth * 4,
                            srcWidth: srcW,
                            srcHeight: srcH,
                            dstBase,
                            dstStrideBytes: destW * 4,
                            dstWidth: destW,
                            dstHeight: destH);
                    }
                    else
                    {
                    for (int dy = 0; dy < destH; dy++)
                    {
                        byte* dstRow = dstBase + dy * destW * 4;
                        byte* srcRow0 = srcBase + (dy * fy) * PixelWidth * 4;

                        for (int dx = 0; dx < destW; dx++)
                        {
                            uint sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                            byte* block = srcRow0 + dx * fx * 4;

                            for (int by = 0; by < fy; by++)
                            {
                                byte* s = block + by * PixelWidth * 4;
                                for (int bx = 0; bx < fx; bx++)
                                {
                                    sumB += s[0];
                                    sumG += s[1];
                                    sumR += s[2];
                                    sumA += s[3];
                                    s += 4;
                                }
                            }

                            dstRow[0] = (byte)(sumB / (uint)blockCount);
                            dstRow[1] = (byte)(sumG / (uint)blockCount);
                            dstRow[2] = (byte)(sumR / (uint)blockCount);
                            dstRow[3] = (byte)(sumA / (uint)blockCount);
                            dstRow += 4;
                        }
                    }
                    }
                }
                else
                {
                    byte* workSrc = srcBase;
                    int workStrideBytes = PixelWidth * 4;
                    int workW = srcW;
                    int workH = srcH;
                    nint workBuffer = 0;

                    try
                    {
                        // HighQuality downscale path: mipmap-like prefiltering (2x box filter)
                        // until the remaining downscale is small enough for bilinear.
                        if (quality == ImageScaleQuality.HighQuality && (destW < srcW || destH < srcH))
                        {
                            for (;;)
                            {
                                int nextW = (workW + 1) / 2;
                                int nextH = (workH + 1) / 2;

                                if (nextW < destW || nextH < destH)
                                {
                                    break;
                                }

                                if (workW < destW * 2 && workH < destH * 2)
                                {
                                    break;
                                }

                                int nextStride = nextW * 4;
                                nint nextBuffer = (nint)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)(nextStride * nextH));
                                if (nextBuffer == 0)
                                {
                                    break;
                                }

                                byte* nextPtr = (byte*)nextBuffer;
                                GdiSimdDispatcher.Downsample2xBoxPremultipliedBgra(
                                    workSrc,
                                    workStrideBytes,
                                    workW,
                                    workH,
                                    nextPtr,
                                    nextStride,
                                    nextW,
                                    nextH);

                                if (workBuffer != 0)
                                {
                                    System.Runtime.InteropServices.NativeMemory.Free((void*)workBuffer);
                                }

                                workBuffer = nextBuffer;
                                workSrc = nextPtr;
                                workStrideBytes = nextStride;
                                workW = nextW;
                                workH = nextH;
                            }
                        }

                        // Bilinear sampling (premultiplied BGRA).
                        float scaleX = (float)workW / destW;
                        float scaleY = (float)workH / destH;

                        int[] x0Arr = System.Buffers.ArrayPool<int>.Shared.Rent(destW);
                        int[] x1Arr = System.Buffers.ArrayPool<int>.Shared.Rent(destW);
                        float[] wxArr = System.Buffers.ArrayPool<float>.Shared.Rent(destW);

                        try
                        {
                            for (int dx = 0; dx < destW; dx++)
                            {
                                float sx = (dx + 0.5f) * scaleX - 0.5f;
                                int x0 = (int)MathF.Floor(sx);
                                float wx = sx - x0;
                                if (x0 < 0) { x0 = 0; wx = 0; }
                                int x1 = Math.Min(workW - 1, x0 + 1);

                                x0Arr[dx] = x0;
                                x1Arr[dx] = x1;
                                wxArr[dx] = wx;
                            }

                            for (int dy = 0; dy < destH; dy++)
                            {
                                float sy = (dy + 0.5f) * scaleY - 0.5f;
                                int y0 = (int)MathF.Floor(sy);
                                float wy = sy - y0;
                                if (y0 < 0) { y0 = 0; wy = 0; }
                                int y1 = Math.Min(workH - 1, y0 + 1);

                                float wy0 = 1 - wy;

                                byte* dstRow = dstBase + dy * destW * 4;
                                byte* row0 = workSrc + y0 * workStrideBytes;
                                byte* row1 = workSrc + y1 * workStrideBytes;

                                for (int dx = 0; dx < destW; dx++)
                                {
                                    int x0 = x0Arr[dx];
                                    int x1 = x1Arr[dx];
                                    float wx = wxArr[dx];
                                    float wx0 = 1 - wx;

                                    byte* p00 = row0 + x0 * 4;
                                    byte* p10 = row0 + x1 * 4;
                                    byte* p01 = row1 + x0 * 4;
                                    byte* p11 = row1 + x1 * 4;

                                    float w00 = wx0 * wy0;
                                    float w10 = wx * wy0;
                                    float w01 = wx0 * wy;
                                    float w11 = wx * wy;

                                    float b = p00[0] * w00 + p10[0] * w10 + p01[0] * w01 + p11[0] * w11;
                                    float g = p00[1] * w00 + p10[1] * w10 + p01[1] * w01 + p11[1] * w11;
                                    float r = p00[2] * w00 + p10[2] * w10 + p01[2] * w01 + p11[2] * w11;
                                    float a = p00[3] * w00 + p10[3] * w10 + p01[3] * w01 + p11[3] * w11;

                                    dstRow[0] = (byte)Math.Clamp((int)MathF.Round(b), 0, 255);
                                    dstRow[1] = (byte)Math.Clamp((int)MathF.Round(g), 0, 255);
                                    dstRow[2] = (byte)Math.Clamp((int)MathF.Round(r), 0, 255);
                                    dstRow[3] = (byte)Math.Clamp((int)MathF.Round(a), 0, 255);
                                    dstRow += 4;
                                }
                            }
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<int>.Shared.Return(x0Arr);
                            System.Buffers.ArrayPool<int>.Shared.Return(x1Arr);
                            System.Buffers.ArrayPool<float>.Shared.Return(wxArr);
                        }
                    }
                    finally
                    {
                        if (workBuffer != 0)
                        {
                            System.Runtime.InteropServices.NativeMemory.Free((void*)workBuffer);
                        }
                    }
                }
            }

            _scaled[key] = dstBitmap;
            scaledBitmap = dstBitmap;
            dstBitmap = 0; // ownership transferred
            return true;
        }
        finally
        {
            if (dstBitmap != 0)
            {
                Gdi32.DeleteObject(dstBitmap);
            }

            User32.ReleaseDC(0, screenDc);
        }
    }

    private void CopyToDibPremultiplied(ReadOnlySpan<byte> pixelData)
    {
        if (_bits == 0 || pixelData.IsEmpty)
        {
            return;
        }

        if (pixelData.Length != PixelWidth * PixelHeight * 4)
        {
            throw new ArgumentException("Invalid pixel data size", nameof(pixelData));
        }

        // GDI AlphaBlend expects the source to be premultiplied alpha (AC_SRC_ALPHA).
        bool needsPremultiply = false;
        for (int i = 3; i < pixelData.Length; i += 4)
        {
            if (pixelData[i] != 0xFF)
            {
                needsPremultiply = true;
                break;
            }
        }

        unsafe
        {
            fixed (byte* src = pixelData)
            {
                if (!needsPremultiply)
                {
                    Buffer.MemoryCopy(src, (void*)_bits, pixelData.Length, pixelData.Length);
                    return;
                }

                byte* dst = (byte*)_bits;
                int count = pixelData.Length;

                for (int i = 0; i < count; i += 4)
                {
                    byte b = src[i + 0];
                    byte g = src[i + 1];
                    byte r = src[i + 2];
                    byte a = src[i + 3];

                    uint alpha = a;
                    dst[i + 3] = a;
                    dst[i + 0] = (byte)((b * alpha + 127) / 255);
                    dst[i + 1] = (byte)((g * alpha + 127) / 255);
                    dst[i + 2] = (byte)((r * alpha + 127) / 255);
                }
            }
        }
    }

    /// <summary>
    /// Saves the bitmap contents to a 32bpp BMP file (BGRA). Intended for debugging/diagnostics.
    /// </summary>
    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var ext = Path.GetExtension(path);
        if (!string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only .bmp is supported for now.");
        }

        if (_disposed || Handle == 0 || _bits == 0)
        {
            throw new ObjectDisposedException(nameof(GdiImage));
        }

        EnsureUpToDate();

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        unsafe
        {
            var bytes = PixelWidth * PixelHeight * 4;
            var span = new ReadOnlySpan<byte>((void*)_bits, bytes);
            WriteBmp32(path, PixelWidth, PixelHeight, span);
        }
    }

    /// <summary>
    /// Saves a cached (or newly generated) scaled bitmap to a 32bpp BMP file (BGRA).
    /// </summary>
    public bool SaveScaled(string path, int srcX, int srcY, int srcW, int srcH, int destW, int destH)
    {
        if (!TryGetOrCreateScaledBitmap(srcX, srcY, srcW, srcH, destW, destH, ImageScaleQuality.HighQuality, out var scaledBmp) || scaledBmp == 0)
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        if (!string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only .bmp is supported for now.");
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        nint screenDc = User32.GetDC(0);
        try
        {
            // Request a top-down 32bpp BGRA DIB into a managed buffer.
            var bmi = BITMAPINFO.Create32bpp(destW, destH);
            var buf = new byte[destW * destH * 4];

            unsafe
            {
                fixed (byte* p = buf)
                {
                    int got = Gdi32.GetDIBits(screenDc, scaledBmp, 0, (uint)destH, (nint)p, ref bmi, 0);
                    if (got == 0)
                    {
                        return false;
                    }
                }
            }

            WriteBmp32(path, destW, destH, buf);
            return true;
        }
        finally
        {
            User32.ReleaseDC(0, screenDc);
        }
    }

    private static void WriteBmp32(string path, int width, int height, ReadOnlySpan<byte> bgraTopDown)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Invalid bitmap size.");
        }

        int expected = width * height * 4;
        if (bgraTopDown.Length < expected)
        {
            throw new ArgumentException("Pixel buffer too small.", nameof(bgraTopDown));
        }

        // BITMAPFILEHEADER (14 bytes) + BITMAPINFOHEADER (40 bytes) + pixel data.
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        int pixelBytes = expected;
        int fileSize = fileHeaderSize + infoHeaderSize + pixelBytes;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var bw = new BinaryWriter(fs);

        // BITMAPFILEHEADER
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write((short)0);
        bw.Write((short)0);
        bw.Write(fileHeaderSize + infoHeaderSize);

        // BITMAPINFOHEADER
        bw.Write(infoHeaderSize);
        bw.Write(width);
        bw.Write(-height); // top-down
        bw.Write((short)1); // planes
        bw.Write((short)32); // bpp
        bw.Write(0); // BI_RGB
        bw.Write(pixelBytes);
        bw.Write(0); // x ppm
        bw.Write(0); // y ppm
        bw.Write(0); // clr used
        bw.Write(0); // clr important

        bw.Write(bgraTopDown.Slice(0, pixelBytes));
    }
}
