namespace Aprillz.MewUI.Platform;

public interface IUiDispatcher
{
    bool IsOnUIThread { get; }

    void Post(Action action);

    void Send(Action action);

    /// <summary>
    /// Schedules an action to run on the UI thread after <paramref name="dueTime"/>.
    /// </summary>
    IDisposable Schedule(TimeSpan dueTime, Action action);

    void ProcessWorkItems();
}
