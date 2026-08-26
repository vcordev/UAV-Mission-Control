using UavMissionControl.App.Services;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.App.ViewModels;

public sealed class TelemetryDashboardViewModel : ViewModelBase
{
    private readonly IUiDispatcher _dispatcher;
    private TelemetrySnapshot _snapshot;

    public TelemetryDashboardViewModel(ITelemetrySimulator simulator, IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _snapshot = simulator.Current;
        simulator.SnapshotUpdated += (_, snapshot) => _dispatcher.Invoke(() => Update(snapshot));
    }

    public double BatteryPercent => _snapshot.BatteryPercent;

    public double SignalStrengthPercent => _snapshot.SignalStrengthPercent;

    public double Latitude => _snapshot.Latitude;

    public double Longitude => _snapshot.Longitude;

    public double AltitudeMeters => _snapshot.AltitudeMeters;

    public double SpeedMetersPerSecond => _snapshot.SpeedMetersPerSecond;

    public BatteryStatus BatteryStatus => TelemetryThresholds.EvaluateBattery(BatteryPercent);

    public SignalStatus SignalStatus => TelemetryThresholds.EvaluateSignal(SignalStrengthPercent);

    private void Update(TelemetrySnapshot snapshot)
    {
        _snapshot = snapshot;

        // A raw property name of null/empty tells WPF bindings to refresh every property on
        // this object — appropriate here since every telemetry field changes on every tick.
        OnPropertyChanged(string.Empty);
    }
}
