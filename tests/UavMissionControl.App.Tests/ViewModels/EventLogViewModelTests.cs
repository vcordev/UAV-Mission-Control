using Shouldly;
using UavMissionControl.App.Services;
using UavMissionControl.App.ViewModels;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.App.Tests.ViewModels;

public class EventLogViewModelTests
{
    [Fact]
    public void Constructor_HydratesFromExistingEntries()
    {
        var eventLog = new EventLog();
        eventLog.Add(LogSeverity.Info, "already there");

        var vm = new EventLogViewModel(eventLog, new ImmediateDispatcher());

        vm.Entries.Count.ShouldBe(1);
        vm.Entries[0].Message.ShouldBe("already there");
    }

    [Fact]
    public void EntryAdded_InsertsNewestFirst()
    {
        var eventLog = new EventLog();
        var vm = new EventLogViewModel(eventLog, new ImmediateDispatcher());

        eventLog.Add(LogSeverity.Info, "first");
        eventLog.Add(LogSeverity.Info, "second");

        vm.Entries[0].Message.ShouldBe("second");
        vm.Entries[1].Message.ShouldBe("first");
    }

    [Fact]
    public void EntryAdded_AlwaysRoutesThroughTheDispatcher()
    {
        // EntryAdded can fire from a background thread (TelemetryWarningMonitor reacts to the
        // simulator's own background timer). ObservableCollection mutations must always go
        // through the dispatcher, regardless of which thread raised the event — this is the
        // seam a real cross-thread crash (see docs/defects/03) would come from skipping.
        var eventLog = new EventLog();
        var dispatcher = new RecordingDispatcher();
        var vm = new EventLogViewModel(eventLog, dispatcher);

        eventLog.Add(LogSeverity.Warning, "battery low");

        dispatcher.InvokeCount.ShouldBe(1);
        vm.Entries.Count.ShouldBe(1);
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public void Invoke(Action action)
        {
            InvokeCount++;
            action();
        }
    }
}
