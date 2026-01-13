using System.Runtime.CompilerServices;

namespace Aprillz.MewUI.Rendering.Gdi.Simd;

/// <summary>
/// Dispatches SIMD operations to the best available implementation.
/// Automatically selects AVX2, SSE2, or scalar based on CPU capabilities.
/// </summary>
internal static class GdiSimdDispatcher
{
    /// <summary>
    /// Writes a row of premultiplied BGRA pixels from alpha values.
    /// Automatically uses the best available SIMD implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WritePremultipliedBgraRow(
        byte* dstBgra,
        ReadOnlySpan<byte> alphaRow,
        byte srcB,
        byte srcG,
        byte srcR)
    {
        if (dstBgra == null || alphaRow.Length == 0)
        {
            return;
        }

        if (SimdCapabilities.HasAvx2)
        {
            Avx2Processor.WritePremultipliedBgraRow(dstBgra, alphaRow, srcB, srcG, srcR);
        }
        else if (SimdCapabilities.HasSse2)
        {
            Sse2Processor.WritePremultipliedBgraRow(dstBgra, alphaRow, srcB, srcG, srcR);
        }
        else
        {
            WritePremultipliedBgraRowScalar(dstBgra, alphaRow, srcB, srcG, srcR);
        }
    }

    /// <summary>
    /// Fills a row of BGRA pixels with a solid premultiplied color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void FillBgraRow(byte* dst, int count, byte b, byte g, byte r, byte a)
    {
        if (dst == null || count <= 0)
        {
            return;
        }

        if (SimdCapabilities.HasAvx2)
        {
            Avx2Processor.FillBgraRow(dst, count, b, g, r, a);
        }
        else if (SimdCapabilities.HasSse2)
        {
            Sse2Processor.FillBgraRow(dst, count, b, g, r, a);
        }
        else
        {
            FillBgraRowScalar(dst, count, b, g, r, a);
        }
    }

    /// <summary>
    /// Clears a row of pixels to zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ClearRow(byte* dst, int byteCount)
    {
        if (dst == null || byteCount <= 0)
        {
            return;
        }

        if (SimdCapabilities.HasAvx2)
        {
            Avx2Processor.ClearRow(dst, byteCount);
        }
        else if (SimdCapabilities.HasSse2)
        {
            Sse2Processor.ClearRow(dst, byteCount);
        }
        else
        {
            new Span<byte>(dst, byteCount).Clear();
        }
    }

    /// <summary>
    /// Clears an entire 2D region efficiently.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ClearRegion(byte* basePtr, int stride, int width, int height)
    {
        if (basePtr == null || width <= 0 || height <= 0)
        {
            return;
        }

        int rowBytes = width * 4;

        for (int y = 0; y < height; y++)
        {
            ClearRow(basePtr + y * stride, rowBytes);
        }
    }

    #region Scalar Fallbacks

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void WritePremultipliedBgraRowScalar(
        byte* dstBgra,
        ReadOnlySpan<byte> alphaRow,
        byte srcB,
        byte srcG,
        byte srcR)
    {
        int width = alphaRow.Length;
        byte* p = dstBgra;

        for (int i = 0; i < width; i++)
        {
            byte a = alphaRow[i];
            if (a == 0)
            {
                p[0] = 0;
                p[1] = 0;
                p[2] = 0;
                p[3] = 0;
            }
            else
            {
                p[0] = Premultiply8(srcB, a);
                p[1] = Premultiply8(srcG, a);
                p[2] = Premultiply8(srcR, a);
                p[3] = a;
            }
            p += 4;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void FillBgraRowScalar(byte* dst, int count, byte b, byte g, byte r, byte a)
    {
        byte pb = Premultiply8(b, a);
        byte pg = Premultiply8(g, a);
        byte pr = Premultiply8(r, a);

        uint pixel = (uint)(pb | (pg << 8) | (pr << 16) | (a << 24));
        uint* p = (uint*)dst;

        for (int i = 0; i < count; i++)
        {
            *p++ = pixel;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Premultiply8(byte c, byte a)
    {
        int t = c * a + 128;
        t += t >> 8;
        return (byte)(t >> 8);
    }

    #endregion
}
