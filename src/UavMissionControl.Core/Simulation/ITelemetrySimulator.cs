using UavMissionControl.Core.Domain;

namespace UavMissionControl.Core.Simulation;

public interface ITelemetrySimulator
{
    TelemetrySnapshot Current { get; }

    event EventHandler<TelemetrySnapshot>? SnapshotUpdated;

    void Start(TimeSpan? interval = null);

    void Stop();

    /// <summary>Advances the simulation by exactly one tick. Public so tests (and any manual
    /// "step" control) can drive deterministic simulation without waiting on a wall-clock timer.</summary>
    void Tick();

    void ForceBatteryPercent(double percent);

    void ForceSignalStrengthPercent(double percent);

    void ClearForcedValues();
}
