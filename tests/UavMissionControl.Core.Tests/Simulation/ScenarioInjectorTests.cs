using Shouldly;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.Core.Tests.Simulation;

public class ScenarioInjectorTests
{
    [Fact]
    public void TriggerLowBattery_ResultsInLowBatteryStatus()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        var injector = new ScenarioInjector(sim);

        injector.TriggerLowBattery();
        sim.Tick();

        TelemetryThresholds.EvaluateBattery(sim.Current.BatteryPercent).ShouldBe(BatteryStatus.Low);
    }

    [Fact]
    public void TriggerCriticalBattery_ResultsInCriticalBatteryStatus()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        var injector = new ScenarioInjector(sim);

        injector.TriggerCriticalBattery();
        sim.Tick();

        TelemetryThresholds.EvaluateBattery(sim.Current.BatteryPercent).ShouldBe(BatteryStatus.Critical);
    }

    [Fact]
    public void TriggerWeakSignal_ResultsInWeakSignalStatus()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        var injector = new ScenarioInjector(sim);

        injector.TriggerWeakSignal();
        sim.Tick();

        TelemetryThresholds.EvaluateSignal(sim.Current.SignalStrengthPercent).ShouldBe(SignalStatus.Weak);
    }

    [Fact]
    public void ClearAllScenarios_RemovesForcedValues()
    {
        var sim = new TelemetrySimulator(() => MissionState.Idle, seed: 1);
        var injector = new ScenarioInjector(sim);
        injector.TriggerCriticalBattery();
        sim.Tick();

        injector.ClearAllScenarios();
        sim.Tick();

        TelemetryThresholds.EvaluateBattery(sim.Current.BatteryPercent).ShouldBe(BatteryStatus.Normal);
    }
}
