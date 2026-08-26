using System.Collections.ObjectModel;
using UavMissionControl.App.Services;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.App.ViewModels;

public sealed class EventLogViewModel : ViewModelBase
{
    private readonly IUiDispatcher _dispatcher;

    public EventLogViewModel(IEventLog eventLog, IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        foreach (var entry in eventLog.Entries)
        {
            Entries.Add(entry);
        }

        eventLog.EntryAdded += (_, entry) => _dispatcher.Invoke(() => Entries.Add(entry));
    }

    /// <summary>Oldest entry first (append-only, O(1) per add) with the view auto-scrolling to
    /// the bottom on each new entry — see docs/defects/06 for why this replaced a newest-first
    /// Insert(0, ...) that degraded under sustained load.</summary>
    public ObservableCollection<EventLogEntry> Entries { get; } = [];
}
