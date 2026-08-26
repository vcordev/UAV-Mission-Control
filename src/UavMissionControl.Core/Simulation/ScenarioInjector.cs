using UavMissionControl.Core.Domain;

namespace UavMissionControl.Core.Simulation;

/// <summary>
/// Named, intention-revealing presets over <see cref="ITelemetrySimulator"/>'s raw Force*
/// methods, so manual/exploratory QA testers (and demos) can trigger a specific warning
/// scenario without knowing the exact threshold numbers — and so those numbers are defined
/// in exactly one place, <see cref="TelemetryThresholds"/>.
/// </summary>
public sealed class ScenarioInjector(ITelemetrySimulator simulator)
{
    public void TriggerLowBattery() =>
        simulator.ForceBatteryPercent(TelemetryThresholds.LowBatteryPercent);

    public void TriggerCriticalBattery() =>
        simulator.ForceBatteryPercent(TelemetryThresholds.CriticalBatteryPercent);

    public void TriggerWeakSignal() =>
        simulator.ForceSignalStrengthPercent(TelemetryThresholds.WeakSignalPercent);

    public void ClearAllScenarios() => simulator.ClearForcedValues();
}
