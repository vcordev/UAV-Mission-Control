using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.Core.Simulation;

/// <summary>
/// Logs an event the moment telemetry crosses into a worse battery/signal status, so warnings
/// show up in the event/log history automatically — not only when a human happens to be
/// watching the dashboard. Logs on entering a worse state once, not on every tick spent there.
/// Reacts to <see cref="ITelemetrySimulator.SnapshotUpdated"/>, which fires on whatever thread
/// is driving the simulator (a background thread for the real timer-driven simulator) — callers
/// that project this onto UI-bound state must marshal accordingly themselves.
/// </summary>
public sealed class TelemetryWarningMonitor
{
    private readonly IEventLog _eventLog;
    private BatteryStatus _lastBatteryStatus = BatteryStatus.Normal;
    private SignalStatus _lastSignalStatus = SignalStatus.Normal;

    public TelemetryWarningMonitor(ITelemetrySimulator simulator, IEventLog eventLog)
    {
        _eventLog = eventLog;
        simulator.SnapshotUpdated += (_, snapshot) => Evaluate(snapshot);
    }

    private void Evaluate(TelemetrySnapshot snapshot)
    {
        var batteryStatus = TelemetryThresholds.EvaluateBattery(snapshot.BatteryPercent);
        if (batteryStatus != _lastBatteryStatus)
        {
            if (batteryStatus == BatteryStatus.Critical)
            {
                _eventLog.Add(LogSeverity.Error, "Battery critical.");
            }
            else if (batteryStatus == BatteryStatus.Low)
            {
                _eventLog.Add(LogSeverity.Warning, "Battery low.");
            }

            _lastBatteryStatus = batteryStatus;
        }

        var signalStatus = TelemetryThresholds.EvaluateSignal(snapshot.SignalStrengthPercent);
        if (signalStatus != _lastSignalStatus)
        {
            if (signalStatus == SignalStatus.Weak)
            {
                _eventLog.Add(LogSeverity.Warning, "Signal weak.");
            }

            _lastSignalStatus = signalStatus;
        }
    }
}
