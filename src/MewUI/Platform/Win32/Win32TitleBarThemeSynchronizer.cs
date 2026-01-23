namespace Aprillz.MewUI.Platform.Win32;

/// <summary>
/// Manages synchronization of the Win32 window title bar appearance with application themes.
/// </summary>
internal sealed class Win32TitleBarThemeSynchronizer : IDisposable
{
    private nint _hWnd;
    private bool _isSubscribed;

    /// <summary>
    /// Initializes the synchronizer and begins tracking theme changes for the specified window.
    /// </summary>
    /// <param name="hWnd">The window handle to synchronize.</param>
    public void Initialize(nint hWnd)
    {
        if (hWnd == 0)
        {
            throw new ArgumentException("Invalid window handle.", nameof(hWnd));
        }

        if (!Win32ImmersiveThemeManager.IsImmersiveThemeSupportedByVersion())
        {
            return;
        }

        _hWnd = hWnd;

        ApplyTheme(Application.Current.Theme);

        if (!_isSubscribed)
        {
            Application.Current.ThemeChanged += OnThemeChanged;
            _isSubscribed = true;
        }
    }

    /// <summary>
    /// Synchronizes the window's title bar appearance with the specified theme.
    /// </summary>
    /// <param name="theme">The theme to apply to the title bar.</param>
    public void ApplyTheme(Theme theme)
    {
        if (_hWnd == 0)
        {
            return;
        }

        bool isDark = Palette.IsDarkBackground(theme.Palette.WindowBackground);
        Win32ImmersiveThemeManager.SetWindowImmersiveTheme(_hWnd, isDark);
    }

    private void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        ApplyTheme(newTheme);
    }

    public void Dispose()
    {
        if (_isSubscribed)
        {
            Application.Current.ThemeChanged -= OnThemeChanged;
            _isSubscribed = false;
        }

        _hWnd = 0;
    }
}
