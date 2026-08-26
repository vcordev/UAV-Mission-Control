using UavMissionControl.Core.Domain;

namespace UavMissionControl.Core.Simulation;

/// <summary>
/// Generates a bounded random-walk telemetry stream. Movement/altitude/speed only progress
/// while the mission is Active (via <paramref name="missionStateProvider"/>); battery always
/// drains slowly, faster while a mission is Active. Not thread-affine to any UI — callers
/// decide how (and on which thread) to marshal <see cref="SnapshotUpdated"/> onward.
/// </summary>
public sealed class TelemetrySimulator : ITelemetrySimulator, IDisposable
{
    private readonly Func<MissionState> _missionStateProvider;
    private readonly Random _random;
    private readonly Lock _lock = new();

    private Timer? _timer;
    private double _battery = 100;
    private double _signal = 100;
    private double _latitude;
    private double _longitude;
    private double _altitude;
    private double _speed;
    private double? _forcedBattery;
    private double? _forcedSignal;

    public TelemetrySimulator(
        Func<MissionState> missionStateProvider,
        int? seed = null,
        double startLatitude = 38.7169,
        double startLongitude = -9.1399)
    {
        _missionStateProvider = missionStateProvider;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _latitude = startLatitude;
        _longitude = startLongitude;
        Current = BuildSnapshot();
    }

    public TelemetrySnapshot Current { get; private set; }

    public event EventHandler<TelemetrySnapshot>? SnapshotUpdated;

    public void Start(TimeSpan? interval = null)
    {
        interval ??= TimeSpan.FromMilliseconds(500);
        _timer = new Timer(_ => Tick(), null, interval.Value, interval.Value);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Tick()
    {
        lock (_lock)
        {
            var active = _missionStateProvider() == MissionState.Active;

            var batteryDrain = active ? 0.15 : 0.02;
            _battery = Math.Clamp(_battery - batteryDrain, 0, 100);

            var signalDelta = (_random.NextDouble() - 0.5) * 6;
            _signal = Math.Clamp(_signal + signalDelta, 0, 100);

            if (active)
            {
                _speed = Math.Clamp(_speed + ((_random.NextDouble() - 0.4) * 2), 0, 25);
                _altitude = Math.Clamp(_altitude + ((_random.NextDouble() - 0.45) * 5), 0, 500);
                var metersToDegrees = 0.000009;
                _latitude += _speed * metersToDegrees * (_random.NextDouble() - 0.5);
                _longitude += _speed * metersToDegrees * (_random.NextDouble() - 0.5);
            }
            else
            {
                _speed = Math.Max(0, _speed - 1);
                _altitude = Math.Max(0, _altitude - 2);
            }

            Current = BuildSnapshot();
        }

        SnapshotUpdated?.Invoke(this, Current);
    }

    public void ForceBatteryPercent(double percent) => _forcedBattery = Math.Clamp(percent, 0, 100);

    public void ForceSignalStrengthPercent(double percent) => _forcedSignal = Math.Clamp(percent, 0, 100);

    public void ClearForcedValues()
    {
        _forcedBattery = null;
        _forcedSignal = null;
    }

    public void Dispose() => Stop();

    private TelemetrySnapshot BuildSnapshot() => new(
        DateTimeOffset.UtcNow,
        _forcedBattery ?? _battery,
        _forcedSignal ?? _signal,
        _latitude,
        _longitude,
        _altitude,
        _speed);
}
