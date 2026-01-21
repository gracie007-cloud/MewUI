using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.GraphicsBackendTest;

static void Startup()
{
    var args = Environment.GetCommandLineArgs();

    if (OperatingSystem.IsWindows())
    {
        if (args.Any(a => a is "--gdi"))
        {
            Application.DefaultGraphicsBackend = GraphicsBackend.Gdi;
        }
        else if (args.Any(a => a is "--gl"))
        {
            Application.DefaultGraphicsBackend = GraphicsBackend.OpenGL;
        }
        else
        {
            Application.DefaultGraphicsBackend = GraphicsBackend.Direct2D;
        }
    }
    else
    {
        Application.DefaultGraphicsBackend = GraphicsBackend.OpenGL;
    }
}

Startup();

var window = new Window()
    .Title("MewUI.GraphicsBackendTest")
    .Fixed(1000, 860)
    .Content(new GraphicsBackendTestView());

Application.Run(window);
