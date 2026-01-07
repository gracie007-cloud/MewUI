using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Com;
using Aprillz.MewUI.Native.Direct2D;
using Aprillz.MewUI.Native.DirectWrite;

namespace Aprillz.MewUI.Rendering.Direct2D;

public sealed class Direct2DGraphicsFactory : IGraphicsFactory, IDisposable
{
    public static Direct2DGraphicsFactory Instance => field ??= new Direct2DGraphicsFactory();

    public GraphicsBackend Backend => GraphicsBackend.Direct2D;

    private nint _d2dFactory;
    private nint _dwriteFactory;
    private bool _initialized;

    private Direct2DGraphicsFactory() { }

    public void Dispose()
    {
        ComHelpers.Release(_dwriteFactory);
        _dwriteFactory = 0;
        ComHelpers.Release(_d2dFactory);
        _d2dFactory = 0;
        _initialized = false;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        Ole32.CoInitializeEx(0, Ole32.COINIT_APARTMENTTHREADED);

        int hr = D2D1.D2D1CreateFactory(D2D1_FACTORY_TYPE.SINGLE_THREADED, D2D1.IID_ID2D1Factory, 0, out _d2dFactory);
        if (hr < 0 || _d2dFactory == 0)
            throw new InvalidOperationException($"D2D1CreateFactory failed: 0x{hr:X8}");

        hr = DWrite.DWriteCreateFactory(DWRITE_FACTORY_TYPE.SHARED, DWrite.IID_IDWriteFactory, out _dwriteFactory);
        if (hr < 0 || _dwriteFactory == 0)
            throw new InvalidOperationException($"DWriteCreateFactory failed: 0x{hr:X8}");

        _initialized = true;
    }

    public IFont CreateFont(string family, double size, FontWeight weight = FontWeight.Normal, bool italic = false, bool underline = false, bool strikethrough = false) =>
        new DirectWriteFont(family, size, weight, italic, underline, strikethrough);

    public IFont CreateFont(string family, double size, uint dpi, FontWeight weight = FontWeight.Normal, bool italic = false, bool underline = false, bool strikethrough = false) =>
        new DirectWriteFont(family, size, weight, italic, underline, strikethrough);

    public IImage CreateImageFromFile(string path) =>
        throw new NotImplementedException("Direct2D image loading is not implemented yet (WIC required).");

    public IImage CreateImageFromBytes(byte[] data) =>
        throw new NotImplementedException("Direct2D image loading is not implemented yet (WIC required).");

    public IGraphicsContext CreateContext(nint hwnd, nint hdc, double dpiScale)
    {
        EnsureInitialized();
        return new Direct2DGraphicsContext(hwnd, dpiScale, _d2dFactory, _dwriteFactory);
    }

    public IGraphicsContext CreateMeasurementContext(uint dpi)
    {
        EnsureInitialized();
        return new Direct2DMeasurementContext(_dwriteFactory);
    }
}
