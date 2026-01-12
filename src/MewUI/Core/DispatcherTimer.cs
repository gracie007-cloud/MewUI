using Aprillz.MewUI.Platform;

namespace Aprillz.MewUI.Core;

/// <summary>
/// UI-thread timer that raises <see cref="Tick"/> on the application's dispatcher.
/// </summary>
public sealed class DispatcherTimer : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _scheduled;
    private TimeSpan _interval = TimeSpan.FromMilliseconds(1000);
    private bool _isEnabled;

    public DispatcherTimer() { }

    public DispatcherTimer(TimeSpan interval)
    {
        Interval = interval;
    }

    public event EventHandler? Tick;

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return _isEnabled;
            }
        }
    }

    public TimeSpan Interval
    {
        get
        {
            lock (_gate)
            {
                return _interval;
            }
        }
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Interval must be greater than zero.");
            }

            lock (_gate)
            {
                _interval = value;
                if (_isEnabled)
                {
                    Reschedule();
                }
            }
        }
    }

    public void Start()
    {
        IUiDispatcher dispatcher = GetDispatcherOrThrow();

        dispatcher.Send(() =>
        {
            lock (_gate)
            {
                if (_isEnabled)
                {
                    return;
                }

                _isEnabled = true;
                _scheduled?.Dispose();
                _scheduled = dispatcher.Schedule(_interval, OnTick);
            }
        });
    }

    public void Stop()
    {
        if (!Application.IsRunning)
        {
            lock (_gate)
            {
                _isEnabled = false;
                _scheduled?.Dispose();
                _scheduled = null;
            }
            return;
        }

        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.Send(() =>
        {
            lock (_gate)
            {
                if (!_isEnabled)
                {
                    return;
                }

                _isEnabled = false;
                _scheduled?.Dispose();
                _scheduled = null;
            }
        });
    }

    private void OnTick()
    {
        IUiDispatcher dispatcher = GetDispatcherOrThrow();

        lock (_gate)
        {
            if (!_isEnabled)
            {
                return;
            }

            // One-shot schedule; re-arm after firing (WPF-style).
            _scheduled?.Dispose();
            _scheduled = null;
        }

        Tick?.Invoke(this, EventArgs.Empty);

        lock (_gate)
        {
            if (!_isEnabled)
            {
                return;
            }

            _scheduled = dispatcher.Schedule(_interval, OnTick);
        }
    }

    private void Reschedule()
    {
        if (!Application.IsRunning)
        {
            return;
        }

        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.Send(() =>
        {
            lock (_gate)
            {
                if (!_isEnabled)
                {
                    return;
                }

                _scheduled?.Dispose();
                _scheduled = dispatcher.Schedule(_interval, OnTick);
            }
        });
    }

    private static IUiDispatcher GetDispatcherOrThrow()
    {
        if (!Application.IsRunning)
        {
            throw new InvalidOperationException("DispatcherTimer requires an active Application.");
        }

        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher == null)
        {
            throw new InvalidOperationException("DispatcherTimer requires Application.Dispatcher.");
        }

        return dispatcher;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
