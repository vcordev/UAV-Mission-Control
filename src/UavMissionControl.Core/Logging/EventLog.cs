namespace UavMissionControl.Core.Logging;

public sealed class EventLog : IEventLog
{
    private readonly List<EventLogEntry> _entries = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<EventLogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public event EventHandler<EventLogEntry>? EntryAdded;

    public void Add(LogSeverity severity, string message)
    {
        var entry = new EventLogEntry(DateTimeOffset.UtcNow, severity, message);

        lock (_lock)
        {
            _entries.Add(entry);
        }

        EntryAdded?.Invoke(this, entry);
    }
}
