using Shouldly;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.Core.Tests.Reliability;

/// <summary>
/// Reliability testing: run the telemetry/logging pipeline far past any realistic single-session
/// duration (100k ticks at the real 500ms interval would be ~14 hours) and confirm nothing throws,
/// values stay within their documented bounds, and memory growth is bounded. This is a coarse
/// signal (GC.GetTotalMemory is not a precision leak detector) deliberately kept simple rather
/// than pulling in a profiling library - see docs/performance-report.md.
/// </summary>
public class SoakTests
{
    [Fact]
    public void TelemetryPipeline_Survives100kTicks_WithoutExceptions_AndStaysInBounds()
    {
        var eventLog = new EventLog();
        var mission = MissionState.Active;
        var sim = new TelemetrySimulator(() => mission, seed: 123);
        _ = new TelemetryWarningMonitor(sim, eventLog);

        for (var i = 0; i < 100_000; i++)
        {
            sim.Tick();

            sim.Current.BatteryPercent.ShouldBeInRange(0, 100);
            sim.Current.SignalStrengthPercent.ShouldBeInRange(0, 100);
            sim.Current.AltitudeMeters.ShouldBeInRange(0, 500);
            sim.Current.SpeedMetersPerSecond.ShouldBeInRange(0, 25);
        }

        eventLog.Entries.Count.ShouldBeGreaterThan(0); // battery necessarily drains to 0 over 100k active ticks
    }

    [Fact]
    public void EventLog_Survives50kEntries_WithBoundedMemoryGrowth()
    {
        var eventLog = new EventLog();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        for (var i = 0; i < 50_000; i++)
        {
            eventLog.Add(LogSeverity.Info, "soak entry");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var after = GC.GetTotalMemory(forceFullCollection: true);

        eventLog.Entries.Count.ShouldBe(50_000);

        // Coarse sanity bound, not a precision measurement: 50k small records should not approach
        // three-digit megabytes. Catches gross accidental duplication/retention, not fine leaks.
        (after - before).ShouldBeLessThan(200 * 1024 * 1024);
    }
}
