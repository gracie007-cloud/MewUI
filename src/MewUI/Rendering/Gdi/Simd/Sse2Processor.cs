using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Aprillz.MewUI.Rendering.Gdi.Simd;

/// <summary>
/// SSE2 optimized pixel processing operations.
/// Processes 16 pixels at a time using 128-bit vectors.
/// </summary>
internal static class Sse2Processor
{
    /// <summary>
    /// Writes a row of premultiplied BGRA pixels from alpha values.
    /// Processes 16 pixels per iteration.
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

        int width = alphaRow.Length;
        int i = 0;

        var zero = Vector128<byte>.Zero;
        var bias128 = Vector128.Create((ushort)128);
        var bConst = Vector128.Create((ushort)srcB);
        var gConst = Vector128.Create((ushort)srcG);
        var rConst = Vector128.Create((ushort)srcR);

        fixed (byte* pAlpha0 = alphaRow)
        {
            // Process 16 pixels at a time
            for (; i + 16 <= width; i += 16)
            {
                var a = Sse2.LoadVector128(pAlpha0 + i);

                // Expand alpha bytes to 16-bit lanes (low/high 8)
                var aLo = Sse2.UnpackLow(a, zero).AsUInt16();
                var aHi = Sse2.UnpackHigh(a, zero).AsUInt16();

                var pb = PackPremultiply(aLo, aHi, bConst, bias128);
                var pg = PackPremultiply(aLo, aHi, gConst, bias128);
                var pr = PackPremultiply(aLo, aHi, rConst, bias128);

                // Interleave into BGRA
                var bgLo = Sse2.UnpackLow(pb, pg);
                var bgHi = Sse2.UnpackHigh(pb, pg);
                var raLo = Sse2.UnpackLow(pr, a);
                var raHi = Sse2.UnpackHigh(pr, a);

                var bgLoW = bgLo.AsUInt16();
                var bgHiW = bgHi.AsUInt16();
                var raLoW = raLo.AsUInt16();
                var raHiW = raHi.AsUInt16();

                // 16 pixels -> 64 bytes
                StoreBgra8(dstBgra + i * 4, bgLoW, raLoW);
                StoreBgra8(dstBgra + (i + 8) * 4, bgHiW, raHiW);
            }
        }

        // Process remaining pixels
        ProcessTail(dstBgra, alphaRow, srcB, srcG, srcR, i, width);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> PackPremultiply(
        Vector128<ushort> aLo,
        Vector128<ushort> aHi,
        Vector128<ushort> c,
        Vector128<ushort> bias128)
    {
        // t = a*c + 128
        var tLo = Sse2.Add(Sse2.MultiplyLow(aLo, c), bias128);
        var tHi = Sse2.Add(Sse2.MultiplyLow(aHi, c), bias128);

        // t = t + (t >> 8)
        tLo = Sse2.Add(tLo, Sse2.ShiftRightLogical(tLo, 8));
        tHi = Sse2.Add(tHi, Sse2.ShiftRightLogical(tHi, 8));

        // t = t >> 8
        tLo = Sse2.ShiftRightLogical(tLo, 8);
        tHi = Sse2.ShiftRightLogical(tHi, 8);

        // Pack 16-bit lanes to bytes
        return Sse2.PackUnsignedSaturate(tLo.AsInt16(), tHi.AsInt16());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void StoreBgra8(byte* dst, Vector128<ushort> bg, Vector128<ushort> ra)
    {
        // bg/ra contain 8 16-bit words each. Interleave words => BGRA bytes for 4 pixels in 16 bytes.
        var lo = Sse2.UnpackLow(bg, ra).AsByte();
        var hi = Sse2.UnpackHigh(bg, ra).AsByte();

        Sse2.Store(dst, lo);
        Sse2.Store(dst + 16, hi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessTail(
        byte* dstBgra,
        ReadOnlySpan<byte> alphaRow,
        byte srcB,
        byte srcG,
        byte srcR,
        int start,
        int end)
    {
        byte* p = dstBgra + start * 4;
        for (int i = start; i < end; i++)
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
    private static byte Premultiply8(byte c, byte a)
    {
        // Exact 8-bit premultiply with rounding:
        // (c*a + 128 + ((c*a + 128) >> 8)) >> 8
        int t = c * a + 128;
        t += t >> 8;
        return (byte)(t >> 8);
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

        // Create premultiplied BGRA value
        byte pb = Premultiply8(b, a);
        byte pg = Premultiply8(g, a);
        byte pr = Premultiply8(r, a);

        uint pixel = (uint)(pb | (pg << 8) | (pr << 16) | (a << 24));
        var pixelVec = Vector128.Create(pixel);

        int i = 0;

        // Process 4 pixels (16 bytes) at a time
        for (; i + 4 <= count; i += 4)
        {
            Sse2.Store((uint*)(dst + i * 4), pixelVec);
        }

        // Tail
        uint* p = (uint*)(dst + i * 4);
        for (; i < count; i++)
        {
            *p++ = pixel;
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

        var zero = Vector128<byte>.Zero;
        int i = 0;

        // Process 16 bytes at a time
        for (; i + 16 <= byteCount; i += 16)
        {
            Sse2.Store(dst + i, zero);
        }

        // Tail
        for (; i < byteCount; i++)
        {
            dst[i] = 0;
        }
    }
}
