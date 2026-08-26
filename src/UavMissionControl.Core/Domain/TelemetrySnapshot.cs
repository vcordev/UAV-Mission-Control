namespace UavMissionControl.Core.Domain;

public sealed record TelemetrySnapshot(
    DateTimeOffset Timestamp,
    double BatteryPercent,
    double SignalStrengthPercent,
    double Latitude,
    double Longitude,
    double AltitudeMeters,
    double SpeedMetersPerSecond);
