using Moq;
using Shouldly;
using UavMissionControl.App.Services;
using UavMissionControl.App.ViewModels;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.App.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Constructor_WiresAllSubViewModels()
    {
        var vm = Create(out _, out _);

        vm.ConnectionPanel.ShouldNotBeNull();
        vm.MissionControl.ShouldNotBeNull();
        vm.TelemetryDashboard.ShouldNotBeNull();
        vm.EventLog.ShouldNotBeNull();
        vm.WarningsBanner.ShouldNotBeNull();
    }

    [Fact]
    public async Task Connecting_StartsTheSimulator()
    {
        var vm = Create(out var simulator, out _);

        await vm.ConnectionPanel.ConnectAsync();

        simulator.Verify(s => s.Start(It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task Disconnecting_StopsTheSimulator()
    {
        var vm = Create(out var simulator, out _);
        await vm.ConnectionPanel.ConnectAsync();

        vm.ConnectionPanel.DisconnectCommand.Execute(null);

        simulator.Verify(s => s.Stop(), Times.Once);
    }

    [Fact]
    public async Task SimulateConnectionLossCommand_OnlyExecutableWhileConnected()
    {
        var vm = Create(out _, out var eventLog);
        vm.SimulateConnectionLossCommand.CanExecute(null).ShouldBeFalse();

        await vm.ConnectionPanel.ConnectAsync();
        vm.SimulateConnectionLossCommand.CanExecute(null).ShouldBeTrue();

        vm.SimulateConnectionLossCommand.Execute(null);

        vm.ConnectionPanel.ConnectionState.ShouldBe(ConnectionState.Disconnected);
        vm.SimulateConnectionLossCommand.CanExecute(null).ShouldBeFalse();
        eventLog.Entries.ShouldContain(e => e.Message.Contains("Simulated connection loss"));
    }

    [Fact]
    public void ScenarioCommands_ForwardToTheSimulator()
    {
        var vm = Create(out var simulator, out _);

        vm.TriggerLowBatteryCommand.Execute(null);
        vm.TriggerCriticalBatteryCommand.Execute(null);
        vm.TriggerWeakSignalCommand.Execute(null);
        vm.ClearScenariosCommand.Execute(null);

        simulator.Verify(s => s.ForceBatteryPercent(TelemetryThresholds.LowBatteryPercent), Times.Once);
        simulator.Verify(s => s.ForceBatteryPercent(TelemetryThresholds.CriticalBatteryPercent), Times.Once);
        simulator.Verify(s => s.ForceSignalStrengthPercent(TelemetryThresholds.WeakSignalPercent), Times.Once);
        simulator.Verify(s => s.ClearForcedValues(), Times.Once);
    }

    private static MainViewModel Create(out Mock<ITelemetrySimulator> simulator, out IEventLog eventLog)
    {
        simulator = new Mock<ITelemetrySimulator>();
        simulator.SetupGet(s => s.Current)
            .Returns(new TelemetrySnapshot(DateTimeOffset.UtcNow, 100, 100, 0, 0, 0, 0));
        eventLog = new EventLog();

        return new MainViewModel(new UavStateMachine(), eventLog, simulator.Object, new ImmediateDispatcher());
    }
}
