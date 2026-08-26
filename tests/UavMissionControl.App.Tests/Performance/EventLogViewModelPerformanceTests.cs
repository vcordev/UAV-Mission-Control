using System.Diagnostics;
using Shouldly;
using UavMissionControl.App.Services;
using UavMissionControl.App.ViewModels;
using EventLog = UavMissionControl.Core.Logging.EventLog;
using LogSeverity = UavMissionControl.Core.Logging.LogSeverity;
using Xunit.Abstractions;

namespace UavMissionControl.App.Tests.Performance;

/// <summary>
/// Basic performance testing: instead of asserting an absolute millisecond budget (flaky across
/// machines), this measures how the cost of adding an entry changes as the collection grows —
/// an O(1) operation should cost about the same at 1k entries as at 50k; an O(n) operation should
/// cost dramatically more. See docs/defects/06 and docs/performance-report.md.
/// </summary>
public class EventLogViewModelPerformanceTests(ITestOutputHelper output)
{
    private const int BatchSize = 5_000;
    private const int FillerSize = 45_000;

    [Fact]
    public void AddingEntries_CostDoesNotDegradeBadly_AsTheLogGrows()
    {
        var eventLog = new EventLog();
        var vm = new EventLogViewModel(eventLog, new ImmediateDispatcher());

        var earlyMs = TimeBatch(eventLog, BatchSize);
        TimeBatch(eventLog, FillerSize); // grow the log well past the early measurement
        var lateMs = TimeBatch(eventLog, BatchSize);

        output.WriteLine($"Batch cost at ~{BatchSize} entries: {earlyMs} ms");
        output.WriteLine($"Batch cost at ~{BatchSize + FillerSize + BatchSize} entries: {lateMs} ms");
        output.WriteLine($"Ratio: {(double)lateMs / Math.Max(earlyMs, 1):0.0}x");
        output.WriteLine($"Final Entries.Count: {vm.Entries.Count}");

        // This is a smoke check for gross (O(n)/O(n^2)) degradation, not a precise time budget:
        // the +20ms floor absorbs machine-speed/JIT noise at these small absolute durations, and
        // the 5x multiplier is deliberately tighter than the ~11x collection-size growth between
        // the two batches - an O(1)-per-add implementation stays flat regardless of machine speed
        // (ratio close to 1x); an O(n)-per-add implementation scales with collection size and
        // measured ~17x here, comfortably failing this bound. See docs/defects/06.
        lateMs.ShouldBeLessThan((earlyMs * 5) + 20);
    }

    private static long TimeBatch(EventLog eventLog, int count)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            eventLog.Add(LogSeverity.Info, "entry");
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }
}
