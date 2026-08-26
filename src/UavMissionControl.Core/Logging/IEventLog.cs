namespace UavMissionControl.Core.Logging;

/// <summary>
/// The mission's event/log history. Deliberately UI-agnostic — the WPF layer projects this
/// onto an ObservableCollection for binding; that projection is where UI-thread-affinity
/// concerns belong, not here.
/// </summary>
public interface IEventLog
{
    IReadOnlyList<EventLogEntry> Entries { get; }

    event EventHandler<EventLogEntry>? EntryAdded;

    void Add(LogSeverity severity, string message);
}
