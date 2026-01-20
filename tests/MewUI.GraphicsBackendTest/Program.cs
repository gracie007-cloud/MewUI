using Aprillz.MewUI;
using Aprillz.MewUI.Backends;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.GraphicsBackendTest;
using Aprillz.MewUI.PlatformHosts;

static void Startup()
{
    var args = Environment.GetCommandLineArgs();

    if (OperatingSystem.IsWindows())
    {
        Win32Platform.Register();

        if (args.Any(a => a is "--gdi"))
        {
            GdiBackend.Register();
        }
        else if (args.Any(a => a is "--gl"))
        {
            OpenGLWin32Backend.Register();
        }
        else
        {
            Direct2DBackend.Register();
        }
    }
    else
    {
        X11Platform.Register();
        OpenGLX11Backend.Register();
    }
}

Startup();

var window = new Window()
    .Title("MewUI.GraphicsBackendTest")
    .Fixed(1000, 800)
    .Content(new GraphicsBackendTestView());

Application.Run(window);
