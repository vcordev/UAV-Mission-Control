using Shouldly;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.Core.Tests.Simulation;

public class TelemetryWarningMonitorTests
{
    [Fact]
    public void CrossingIntoLowBattery_LogsOnce()
    {
        var eventLog = new EventLog();
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        _ = new TelemetryWarningMonitor(sim, eventLog);

        sim.ForceBatteryPercent(TelemetryThresholds.LowBatteryPercent);
        sim.Tick();
        sim.Tick();
        sim.Tick();

        eventLog.Entries.Count(e => e.Message == "Battery low.").ShouldBe(1);
    }

    [Fact]
    public void CrossingIntoCriticalBattery_LogsAsError()
    {
        var eventLog = new EventLog();
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        _ = new TelemetryWarningMonitor(sim, eventLog);

        sim.ForceBatteryPercent(TelemetryThresholds.CriticalBatteryPercent);
        sim.Tick();

        eventLog.Entries.ShouldContain(e => e.Severity == LogSeverity.Error && e.Message == "Battery critical.");
    }

    [Fact]
    public void CrossingIntoWeakSignal_LogsOnce()
    {
        var eventLog = new EventLog();
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        _ = new TelemetryWarningMonitor(sim, eventLog);

        sim.ForceSignalStrengthPercent(TelemetryThresholds.WeakSignalPercent);
        sim.Tick();
        sim.Tick();

        eventLog.Entries.Count(e => e.Message == "Signal weak.").ShouldBe(1);
    }

    [Fact]
    public void NormalTelemetry_NeverLogsAnything()
    {
        var eventLog = new EventLog();
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        _ = new TelemetryWarningMonitor(sim, eventLog);

        for (var i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        eventLog.Entries.ShouldBeEmpty();
    }
}
