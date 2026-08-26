namespace UavMissionControl.Core.Domain;

public enum BatteryStatus
{
    Normal,
    Low,
    Critical,
}

public enum SignalStatus
{
    Normal,
    Weak,
}

/// <summary>
/// Threshold values and evaluation rules for telemetry-driven warnings. Kept in one place,
/// separate from the ViewModels that display them, so boundary values are unambiguous and
/// unit-testable without WPF.
/// </summary>
public static class TelemetryThresholds
{
    public const double CriticalBatteryPercent = 10;
    public const double LowBatteryPercent = 20;
    public const double WeakSignalPercent = 25;

    public static BatteryStatus EvaluateBattery(double batteryPercent)
    {
        if (batteryPercent <= CriticalBatteryPercent)
        {
            return BatteryStatus.Critical;
        }

        if (batteryPercent <= LowBatteryPercent)
        {
            return BatteryStatus.Low;
        }

        return BatteryStatus.Normal;
    }

    public static SignalStatus EvaluateSignal(double signalStrengthPercent) =>
        signalStrengthPercent <= WeakSignalPercent ? SignalStatus.Weak : SignalStatus.Normal;
}
