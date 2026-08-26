using Moq;
using Shouldly;
using UavMissionControl.App.Services;
using UavMissionControl.App.ViewModels;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.App.Tests.ViewModels;

public class WarningsBannerViewModelTests
{
    [Fact]
    public void NoWarnings_WhenTelemetryNormalAndNoAbort()
    {
        var (banner, _, _) = Create(battery: 100, signal: 100);

        banner.IsBatteryLow.ShouldBeFalse();
        banner.IsBatteryCritical.ShouldBeFalse();
        banner.IsSignalWeak.ShouldBeFalse();
        banner.IsEmergencyAbort.ShouldBeFalse();
    }

    [Fact]
    public void IsBatteryCritical_ReflectsTelemetry_AndUpdatesOnChange()
    {
        var (banner, simulatorMock, _) = Create(battery: 100, signal: 100);
        banner.IsBatteryCritical.ShouldBeFalse();

        simulatorMock.Raise(s => s.SnapshotUpdated += null, simulatorMock.Object,
            new TelemetrySnapshot(DateTimeOffset.UtcNow, 5, 100, 0, 0, 0, 0));

        banner.IsBatteryCritical.ShouldBeTrue();
        banner.IsBatteryLow.ShouldBeFalse();
    }

    [Fact]
    public void IsEmergencyAbort_TracksMissionState()
    {
        var (banner, _, stateMachine) = Create(battery: 100, signal: 100);
        stateMachine.TransitionConnection(ConnectionState.Connecting);
        stateMachine.TransitionConnection(ConnectionState.Connected);
        stateMachine.TransitionMission(MissionState.Active);

        stateMachine.TransitionConnection(ConnectionState.Disconnected);

        banner.IsEmergencyAbort.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "KnownDefect")]
    public void IsEmergencyAbort_ShouldClear_AfterReconnecting_DEFECT08()
    {
        // KNOWN DEFECT - see docs/defects/08-emergency-abort-banner-persists-after-reconnect.md.
        // Deliberately left FAILING: nothing ever transitions MissionState from EmergencyAbort
        // back to Idle on reconnect, so the red "EMERGENCY ABORT" banner stays lit forever
        // after the UAV reconnects, even though the connection and signal are fine again.
        // This test documents and automatically detects that regression; it is intentionally
        // NOT fixed. Tagged [Trait("Category","KnownDefect")] so
        // `dotnet test --filter "Category!=KnownDefect"` reproduces the "all green" demo run
        // without hiding this test's existence.
        var (banner, _, stateMachine) = Create(battery: 100, signal: 100);
        stateMachine.TransitionConnection(ConnectionState.Connecting);
        stateMachine.TransitionConnection(ConnectionState.Connected);
        stateMachine.TransitionMission(MissionState.Active);
        stateMachine.TransitionConnection(ConnectionState.Disconnected);

        banner.IsEmergencyAbort.ShouldBeTrue(); // sanity: banner is lit, as expected

        stateMachine.TransitionConnection(ConnectionState.Connecting);
        stateMachine.TransitionConnection(ConnectionState.Connected);

        // Expected (correct) behavior: reconnecting should clear the stale emergency banner.
        banner.IsEmergencyAbort.ShouldBeFalse();
    }

    private static (WarningsBannerViewModel Banner, Mock<ITelemetrySimulator> Simulator, UavStateMachine StateMachine) Create(
        double battery, double signal)
    {
        var simulator = new Mock<ITelemetrySimulator>();
        simulator.SetupGet(s => s.Current)
            .Returns(new TelemetrySnapshot(DateTimeOffset.UtcNow, battery, signal, 0, 0, 0, 0));
        var stateMachine = new UavStateMachine();
        var dispatcher = new ImmediateDispatcher();

        var telemetry = new TelemetryDashboardViewModel(simulator.Object, dispatcher);
        var mission = new MissionControlViewModel(stateMachine, new EventLog());
        var banner = new WarningsBannerViewModel(telemetry, mission);

        return (banner, simulator, stateMachine);
    }
}
