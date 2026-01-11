using System.Collections.Concurrent;

using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Constants;

namespace Aprillz.MewUI.Platform.Win32;

public sealed class Win32UiDispatcher : SynchronizationContext, IUiDispatcher
{
    private readonly ConcurrentQueue<WorkItem> _workItems = new();
    private readonly Dictionary<nuint, Action> _timerCallbacks = new();
    private readonly nint _hwnd;
    private readonly int _mainThreadId;
    private int _nextTimerId;

    internal const uint WM_INVOKE = WindowMessages.WM_USER + 1;

    private readonly struct WorkItem
    {
        public readonly SendOrPostCallback Callback;
        public readonly object? State;
        public readonly ManualResetEventSlim? Signal;

        public WorkItem(SendOrPostCallback callback, object? state, ManualResetEventSlim? signal = null)
        {
            Callback = callback;
            State = state;
            Signal = signal;
        }
    }

    internal Win32UiDispatcher(nint hwnd)
    {
        _hwnd = hwnd;
        _mainThreadId = Environment.CurrentManagedThreadId;
    }

    public bool IsOnUIThread => Environment.CurrentManagedThreadId == _mainThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _workItems.Enqueue(new WorkItem(d, state));
        User32.PostMessage(_hwnd, WM_INVOKE, 0, 0);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        if (IsOnUIThread)
        {
            d(state);
            return;
        }

        using var signal = new ManualResetEventSlim(false);
        _workItems.Enqueue(new WorkItem(d, state, signal));
        User32.PostMessage(_hwnd, WM_INVOKE, 0, 0);
        signal.Wait();
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Post(_ => action(), null);
    }

    public void Send(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Send(_ => action(), null);
    }

    public IDisposable Schedule(TimeSpan dueTime, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dueTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(dueTime), dueTime, "DueTime must be non-negative.");

        if (!IsOnUIThread)
        {
            nuint id = 0;
            Send(() => id = ScheduleOnUiThread(dueTime, action));
            return new TimerHandle(this, id);
        }

        nuint timerId = ScheduleOnUiThread(dueTime, action);
        return new TimerHandle(this, timerId);
    }

    private nuint ScheduleOnUiThread(TimeSpan dueTime, Action action)
    {
        // Clamp to Win32 timer resolution (milliseconds).
        uint ms = dueTime == TimeSpan.Zero ? 1u : (uint)Math.Max(1, Math.Min(dueTime.TotalMilliseconds, uint.MaxValue));

        nuint id = (nuint)unchecked(++_nextTimerId);
        _timerCallbacks[id] = action;

        // WM_TIMER will keep firing until killed.
        if (User32.SetTimer(_hwnd, id, ms, 0) == 0)
        {
            _timerCallbacks.Remove(id);
            throw new InvalidOperationException("SetTimer failed.");
        }

        return id;
    }

    internal bool ProcessTimer(nuint timerId)
    {
        if (!_timerCallbacks.TryGetValue(timerId, out var callback))
            return false;

        // Kill first (WM_TIMER repeats).
        _timerCallbacks.Remove(timerId);
        User32.KillTimer(_hwnd, timerId);

        callback();
        return true;
    }

    private void CancelTimer(nuint timerId)
    {
        if (timerId == 0)
            return;

        if (!IsOnUIThread)
        {
            Post(() => CancelTimer(timerId));
            return;
        }

        if (_timerCallbacks.Remove(timerId))
            User32.KillTimer(_hwnd, timerId);
    }

    public void ProcessWorkItems()
    {
        while (_workItems.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            finally
            {
                item.Signal?.Set();
            }
        }
    }

    private sealed class TimerHandle : IDisposable
    {
        private Win32UiDispatcher? _dispatcher;
        private nuint _timerId;

        public TimerHandle(Win32UiDispatcher dispatcher, nuint timerId)
        {
            _dispatcher = dispatcher;
            _timerId = timerId;
        }

        public void Dispose()
        {
            var dispatcher = Interlocked.Exchange(ref _dispatcher, null);
            if (dispatcher == null)
                return;

            var id = Interlocked.Exchange(ref _timerId, 0);
            dispatcher.CancelTimer(id);
        }
    }
}
