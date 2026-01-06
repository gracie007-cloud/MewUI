using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Platform;
using Aprillz.MewUI.Platform.Win32;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Rendering.Direct2D;
using Aprillz.MewUI.Rendering.Gdi;

namespace Aprillz.MewUI.Core;

/// <summary>
/// Represents the main application entry point and message loop.
/// </summary>
public sealed class Application
{
    private static Application? _current;
    private static readonly object _syncLock = new();
    private static GraphicsBackend _defaultGraphicsBackend = GraphicsBackend.Direct2D;
    private static IGraphicsFactory? _defaultGraphicsFactoryOverride;
    private static IPlatformHost _defaultPlatformHost = new Win32PlatformHost();

    /// <summary>
    /// Gets the current application instance.
    /// </summary>
    public static Application Current => _current ?? throw new InvalidOperationException("Application not initialized. Call Application.Run() first.");

    /// <summary>
    /// Gets whether an application instance is running.
    /// </summary>
    public static bool IsRunning => _current != null;

    public IPlatformHost PlatformHost { get; }

    public IUiDispatcher? Dispatcher { get; internal set; }

    /// <summary>
    /// Gets or sets the default graphics backend used by windows/controls.
    /// Can be configured before <see cref="Run(Window)"/>.
    /// </summary>
    public static GraphicsBackend DefaultGraphicsBackend
    {
        get => _defaultGraphicsBackend;
        set
        {
            _defaultGraphicsBackend = value;
            _defaultGraphicsFactoryOverride = null;
        }
    }

    /// <summary>
    /// Gets or sets the default graphics factory used by windows/controls.
    /// Prefer <see cref="DefaultGraphicsBackend"/> for built-in backends.
    /// </summary>
    public static IGraphicsFactory DefaultGraphicsFactory
    {
        get => _defaultGraphicsFactoryOverride ?? GetFactoryForBackend(_defaultGraphicsBackend);
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // Keep existing code working, but prefer enum configuration.
            if (ReferenceEquals(value, Direct2DGraphicsFactory.Instance))
            {
                DefaultGraphicsBackend = GraphicsBackend.Direct2D;
                return;
            }

            if (ReferenceEquals(value, GdiGraphicsFactory.Instance))
            {
                DefaultGraphicsBackend = GraphicsBackend.Gdi;
                return;
            }

            _defaultGraphicsFactoryOverride = value;
        }
    }

    public static IPlatformHost DefaultPlatformHost
    {
        get => _defaultPlatformHost;
        set => _defaultPlatformHost = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the graphics factory used by windows/controls for this application instance.
    /// </summary>
    public IGraphicsFactory GraphicsFactory
    {
        get => DefaultGraphicsFactory;
        set => DefaultGraphicsFactory = value;
    }

    /// <summary>
    /// Runs the application with the specified main window.
    /// </summary>
    public static void Run(Window mainWindow)
    {
        if (_current != null)
            throw new InvalidOperationException("Application is already running.");

        lock (_syncLock)
        {
            if (_current != null)
                throw new InvalidOperationException("Application is already running.");

            var app = new Application(DefaultPlatformHost);
            _current = app;
            app.RunCore(mainWindow);
        }
    }

    private Application(IPlatformHost platformHost) => PlatformHost = platformHost;

    private void RunCore(Window mainWindow)
    {
        PlatformHost.Run(this, mainWindow);
        _current = null;
    }

    /// <summary>
    /// Quits the application.
    /// </summary>
    public static void Quit()
    {
        if (_current == null)
            return;
        _current.PlatformHost.Quit(_current);
    }

    /// <summary>
    /// Dispatches pending messages in the message queue.
    /// </summary>
    public static void DoEvents()
    {
        if (_current == null)
            return;
        _current.PlatformHost.DoEvents();
    }

    private static IGraphicsFactory GetFactoryForBackend(GraphicsBackend backend) => backend switch
    {
        GraphicsBackend.Direct2D => Direct2DGraphicsFactory.Instance,
        GraphicsBackend.Gdi => GdiGraphicsFactory.Instance,
        _ => Direct2DGraphicsFactory.Instance,
    };
}
