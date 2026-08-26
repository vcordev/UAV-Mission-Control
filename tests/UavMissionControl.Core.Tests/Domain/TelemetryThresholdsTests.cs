using Shouldly;
using UavMissionControl.Core.Domain;

namespace UavMissionControl.Core.Tests.Domain;

/// <summary>
/// Boundary-value analysis for the battery/signal warning thresholds. Each threshold gets
/// three points: just above, exactly on, and just below — the classic BVA triad.
/// </summary>
public class TelemetryThresholdsTests
{
    [Theory]
    [InlineData(100, BatteryStatus.Normal)]
    [InlineData(20.1, BatteryStatus.Normal)]
    [InlineData(20, BatteryStatus.Low)] // exactly on the low-battery boundary
    [InlineData(19.9, BatteryStatus.Low)]
    [InlineData(10.1, BatteryStatus.Low)]
    [InlineData(10, BatteryStatus.Critical)] // exactly on the critical-battery boundary
    [InlineData(9.9, BatteryStatus.Critical)]
    [InlineData(0, BatteryStatus.Critical)]
    public void EvaluateBattery_ReturnsExpectedStatus(double batteryPercent, BatteryStatus expected)
    {
        TelemetryThresholds.EvaluateBattery(batteryPercent).ShouldBe(expected);
    }

    [Theory]
    [InlineData(100, SignalStatus.Normal)]
    [InlineData(25.1, SignalStatus.Normal)]
    [InlineData(25, SignalStatus.Weak)] // exactly on the weak-signal boundary
    [InlineData(24.9, SignalStatus.Weak)]
    [InlineData(0, SignalStatus.Weak)]
    public void EvaluateSignal_ReturnsExpectedStatus(double signalPercent, SignalStatus expected)
    {
        TelemetryThresholds.EvaluateSignal(signalPercent).ShouldBe(expected);
    }
}
