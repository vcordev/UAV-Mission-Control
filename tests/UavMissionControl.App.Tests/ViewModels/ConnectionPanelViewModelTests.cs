using Shouldly;
using UavMissionControl.App.ViewModels;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.App.Tests.ViewModels;

public class ConnectionPanelViewModelTests
{
    [Fact]
    public void Initial_IsDisconnected_ConnectEnabledDisconnectDisabled()
    {
        var vm = new ConnectionPanelViewModel(new UavStateMachine(), new EventLog(), TimeSpan.Zero);

        vm.StatusText.ShouldBe("Disconnected");
        vm.ConnectCommand.CanExecute(null).ShouldBeTrue();
        vm.DisconnectCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task ConnectAsync_TransitionsThroughConnectingToConnected()
    {
        var stateMachine = new UavStateMachine();
        // A generous delay here, not the TimeSpan.Zero used elsewhere in this file: this test
        // asserts the transient "Connecting" state before the delay completes, so the window
        // needs to comfortably outlast scheduling jitter. A short (~20ms) delay measured flaky
        // under load in this session — the delay's background continuation occasionally beat
        // the test's very next line to the CPU.
        var vm = new ConnectionPanelViewModel(stateMachine, new EventLog(), TimeSpan.FromMilliseconds(300));

        var connectTask = vm.ConnectAsync();
        vm.ConnectionState.ShouldBe(ConnectionState.Connecting);
        vm.StatusText.ShouldBe("Connecting...");

        await connectTask;

        vm.ConnectionState.ShouldBe(ConnectionState.Connected);
        vm.StatusText.ShouldBe("Connected");
        vm.ConnectCommand.CanExecute(null).ShouldBeFalse();
        vm.DisconnectCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectAsync_LogsConnectingAndConnectedEvents()
    {
        var eventLog = new EventLog();
        var vm = new ConnectionPanelViewModel(new UavStateMachine(), eventLog, TimeSpan.Zero);

        await vm.ConnectAsync();

        eventLog.Entries.Select(e => e.Message).ShouldContain("Connecting to UAV...");
        eventLog.Entries.Select(e => e.Message).ShouldContain("Connected to UAV.");
    }

    [Fact]
    public async Task Reconnecting_LogsTelemetryLinkEstablished_ExactlyOncePerConnectCycle()
    {
        // Regression guard for docs/defects/04: a per-connect subscription that isn't cleaned
        // up would make the second reconnect log this message twice, the third log it three
        // times, etc. Two connect cycles here already exposes that compounding.
        var eventLog = new EventLog();
        var stateMachine = new UavStateMachine();
        var vm = new ConnectionPanelViewModel(stateMachine, eventLog, TimeSpan.Zero);

        await vm.ConnectAsync();
        vm.DisconnectCommand.Execute(null);
        await vm.ConnectAsync();

        eventLog.Entries.Count(e => e.Message == "Telemetry link established.").ShouldBe(2);
    }

    [Fact]
    public async Task Disconnect_FromConnected_TransitionsToDisconnectedAndLogs()
    {
        var stateMachine = new UavStateMachine();
        var eventLog = new EventLog();
        var vm = new ConnectionPanelViewModel(stateMachine, eventLog, TimeSpan.Zero);
        await vm.ConnectAsync();

        vm.DisconnectCommand.Execute(null);

        vm.ConnectionState.ShouldBe(ConnectionState.Disconnected);
        vm.StatusText.ShouldBe("Disconnected");
        eventLog.Entries.Select(e => e.Message).ShouldContain("Disconnected from UAV.");
    }

    [Fact]
    public void PropertyChanged_IsRaised_OnConnectionStateChange()
    {
        var stateMachine = new UavStateMachine();
        var vm = new ConnectionPanelViewModel(stateMachine, new EventLog(), TimeSpan.Zero);
        var raisedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        stateMachine.TransitionConnection(ConnectionState.Connecting);

        raisedProperties.ShouldContain(nameof(ConnectionPanelViewModel.ConnectionState));
        raisedProperties.ShouldContain(nameof(ConnectionPanelViewModel.StatusText));
    }
}
