using Shouldly;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.Core.Tests.Simulation;

public class TelemetrySimulatorTests
{
    [Fact]
    public void Current_BeforeAnyTick_StartsAtFullBatteryAndSignal()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);

        sim.Current.BatteryPercent.ShouldBe(100);
        sim.Current.SignalStrengthPercent.ShouldBe(100);
        sim.Current.SpeedMetersPerSecond.ShouldBe(0);
    }

    [Fact]
    public void Tick_WhileIdle_DrainsBatterySlowlyAndKeepsSpeedZero()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);

        sim.Tick();

        sim.Current.BatteryPercent.ShouldBeLessThan(100);
        sim.Current.BatteryPercent.ShouldBeGreaterThan(99);
        sim.Current.SpeedMetersPerSecond.ShouldBe(0);
    }

    [Fact]
    public void Tick_WhileActive_DrainsBatteryFasterThanWhileIdle()
    {
        var idleSim = new TelemetrySimulator(() => MissionState.Idle, seed: 42);
        var activeSim = new TelemetrySimulator(() => MissionState.Active, seed: 42);

        for (var i = 0; i < 10; i++)
        {
            idleSim.Tick();
            activeSim.Tick();
        }

        activeSim.Current.BatteryPercent.ShouldBeLessThan(idleSim.Current.BatteryPercent);
    }

    [Fact]
    public void Tick_ManyTimes_KeepsBatteryAndSignalWithinBounds()
    {
        var sim = new TelemetrySimulator(() => MissionState.Active, seed: 7);

        for (var i = 0; i < 2000; i++)
        {
            sim.Tick();
        }

        sim.Current.BatteryPercent.ShouldBeInRange(0, 100);
        sim.Current.SignalStrengthPercent.ShouldBeInRange(0, 100);
        sim.Current.AltitudeMeters.ShouldBeInRange(0, 500);
        sim.Current.SpeedMetersPerSecond.ShouldBeInRange(0, 25);
    }

    [Fact]
    public void Tick_WithSameSeed_ProducesIdenticalSequence()
    {
        var simA = new TelemetrySimulator(() => MissionState.Active, seed: 99);
        var simB = new TelemetrySimulator(() => MissionState.Active, seed: 99);

        for (var i = 0; i < 50; i++)
        {
            simA.Tick();
            simB.Tick();

            simA.Current.BatteryPercent.ShouldBe(simB.Current.BatteryPercent);
            simA.Current.SignalStrengthPercent.ShouldBe(simB.Current.SignalStrengthPercent);
            simA.Current.AltitudeMeters.ShouldBe(simB.Current.AltitudeMeters);
        }
    }

    [Fact]
    public void Tick_RaisesSnapshotUpdated_WithCurrentSnapshot()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        TelemetrySnapshot? raised = null;
        sim.SnapshotUpdated += (_, s) => raised = s;

        sim.Tick();

        raised.ShouldBe(sim.Current);
    }

    [Fact]
    public void ForceBatteryPercent_OverridesRealValueOnNextTick()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);

        sim.ForceBatteryPercent(15);
        sim.Tick();

        sim.Current.BatteryPercent.ShouldBe(15);
    }

    [Fact]
    public void ClearForcedValues_ResumesRealSimulatedValue()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        sim.ForceBatteryPercent(15);
        sim.Tick();

        sim.ClearForcedValues();
        sim.Tick();

        sim.Current.BatteryPercent.ShouldNotBe(15);
        sim.Current.BatteryPercent.ShouldBeGreaterThan(90);
    }
}
