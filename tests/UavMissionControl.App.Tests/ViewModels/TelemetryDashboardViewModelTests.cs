using Moq;
using Shouldly;
using UavMissionControl.App.Services;
using UavMissionControl.App.ViewModels;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.App.Tests.ViewModels;

public class TelemetryDashboardViewModelTests
{
    [Fact]
    public void Constructor_ReadsInitialSnapshotFromSimulator()
    {
        var initial = new TelemetrySnapshot(DateTimeOffset.UtcNow, 87, 42, 1, 2, 3, 4);
        var simulator = new Mock<ITelemetrySimulator>();
        simulator.SetupGet(s => s.Current).Returns(initial);

        var vm = new TelemetryDashboardViewModel(simulator.Object, new ImmediateDispatcher());

        vm.BatteryPercent.ShouldBe(87);
        vm.SignalStrengthPercent.ShouldBe(42);
        vm.AltitudeMeters.ShouldBe(3);
        vm.SpeedMetersPerSecond.ShouldBe(4);
    }

    [Fact]
    public void SnapshotUpdated_UpdatesAllBoundProperties()
    {
        var simulator = new Mock<ITelemetrySimulator>();
        simulator.SetupGet(s => s.Current).Returns(new TelemetrySnapshot(DateTimeOffset.UtcNow, 100, 100, 0, 0, 0, 0));
        var vm = new TelemetryDashboardViewModel(simulator.Object, new ImmediateDispatcher());

        var updated = new TelemetrySnapshot(DateTimeOffset.UtcNow, 55, 30, 10, 20, 100, 12);
        simulator.Raise(s => s.SnapshotUpdated += null, simulator.Object, updated);

        vm.BatteryPercent.ShouldBe(55);
        vm.SignalStrengthPercent.ShouldBe(30);
        vm.Latitude.ShouldBe(10);
        vm.Longitude.ShouldBe(20);
        vm.AltitudeMeters.ShouldBe(100);
        vm.SpeedMetersPerSecond.ShouldBe(12);
    }

    [Theory]
    [InlineData(20, BatteryStatus.Low)]
    [InlineData(10, BatteryStatus.Critical)]
    [InlineData(50, BatteryStatus.Normal)]
    public void BatteryStatus_ReflectsCurrentBatteryPercent(double battery, BatteryStatus expected)
    {
        var simulator = new Mock<ITelemetrySimulator>();
        simulator.SetupGet(s => s.Current).Returns(new TelemetrySnapshot(DateTimeOffset.UtcNow, battery, 100, 0, 0, 0, 0));

        var vm = new TelemetryDashboardViewModel(simulator.Object, new ImmediateDispatcher());

        vm.BatteryStatus.ShouldBe(expected);
    }

    [Fact]
    public void PropertyChanged_IsRaisedOnDispatcherThread_ViaProvidedDispatcher()
    {
        var simulator = new Mock<ITelemetrySimulator>();
        simulator.SetupGet(s => s.Current).Returns(new TelemetrySnapshot(DateTimeOffset.UtcNow, 100, 100, 0, 0, 0, 0));
        var invokeCount = 0;
        var countingDispatcher = new CountingDispatcher(() => invokeCount++);
        var vm = new TelemetryDashboardViewModel(simulator.Object, countingDispatcher);

        simulator.Raise(s => s.SnapshotUpdated += null, simulator.Object,
            new TelemetrySnapshot(DateTimeOffset.UtcNow, 50, 50, 0, 0, 0, 0));

        invokeCount.ShouldBe(1);
    }

    private sealed class CountingDispatcher(Action onInvoke) : IUiDispatcher
    {
        public void Invoke(Action action)
        {
            onInvoke();
            action();
        }
    }
}
