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
}
