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
            Entries.Insert(0, entry);
        }

        eventLog.EntryAdded += (_, entry) => _dispatcher.Invoke(() => Entries.Insert(0, entry));
    }

    /// <summary>Newest entry first, so the panel reads top-to-bottom as a live feed.</summary>
    public ObservableCollection<EventLogEntry> Entries { get; } = [];
}
