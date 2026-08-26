using Shouldly;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.Core.Tests.Logging;

public class EventLogTests
{
    [Fact]
    public void Add_AppendsEntryWithTimestampAndSeverity()
    {
        var log = new EventLog();

        log.Add(LogSeverity.Warning, "Battery low");

        log.Entries.Count.ShouldBe(1);
        log.Entries[0].Severity.ShouldBe(LogSeverity.Warning);
        log.Entries[0].Message.ShouldBe("Battery low");
    }

    [Fact]
    public void Add_PreservesInsertionOrder()
    {
        var log = new EventLog();

        log.Add(LogSeverity.Info, "first");
        log.Add(LogSeverity.Info, "second");
        log.Add(LogSeverity.Info, "third");

        log.Entries.Select(e => e.Message).ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public void Add_RaisesEntryAdded_WithTheNewEntry()
    {
        var log = new EventLog();
        EventLogEntry? raised = null;
        log.EntryAdded += (_, e) => raised = e;

        log.Add(LogSeverity.Error, "connection lost");

        raised.ShouldNotBeNull();
        raised!.Message.ShouldBe("connection lost");
    }

    [Fact]
    public void Entries_ReturnsASnapshot_UnaffectedByLaterAdds()
    {
        var log = new EventLog();
        log.Add(LogSeverity.Info, "first");

        var snapshot = log.Entries;
        log.Add(LogSeverity.Info, "second");

        snapshot.Count.ShouldBe(1);
        log.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public void Add_FromManyConcurrentThreads_LosesNoEntries()
    {
        var log = new EventLog();
        const int threads = 8;
        const int perThread = 200;

        Parallel.For(0, threads, _ =>
        {
            for (var i = 0; i < perThread; i++)
            {
                log.Add(LogSeverity.Info, "concurrent");
            }
        });

        log.Entries.Count.ShouldBe(threads * perThread);
    }
}
