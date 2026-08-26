using Shouldly;
using UavMissionControl.Core.Domain;

namespace UavMissionControl.Core.Tests.Domain;

public class UavStateMachineTests
{
    [Fact]
    public void InitialState_IsDisconnectedAndIdle()
    {
        var sm = new UavStateMachine();

        sm.ConnectionState.ShouldBe(ConnectionState.Disconnected);
        sm.MissionState.ShouldBe(MissionState.Idle);
    }

    [Theory]
    [InlineData(ConnectionState.Disconnected, ConnectionState.Connecting, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Connected, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Disconnected, true)]
    [InlineData(ConnectionState.Connected, ConnectionState.Disconnected, true)]
    [InlineData(ConnectionState.Disconnected, ConnectionState.Connected, false)]
    [InlineData(ConnectionState.Connected, ConnectionState.Connecting, false)]
    public void CanTransitionConnection_MatchesExpectedGraph(
        ConnectionState from, ConnectionState to, bool expected)
    {
        var sm = new UavStateMachine();
        DriveConnectionTo(sm, from);

        sm.CanTransitionConnection(to).ShouldBe(expected);
    }

    [Fact]
    public void TransitionConnection_ToIllegalState_ThrowsAndLeavesStateUnchanged()
    {
        var sm = new UavStateMachine();

        Should.Throw<InvalidStateTransitionException>(() => sm.TransitionConnection(ConnectionState.Connected));

        sm.ConnectionState.ShouldBe(ConnectionState.Disconnected);
    }

    [Fact]
    public void TransitionConnection_ToLegalState_RaisesConnectionStateChanged()
    {
        var sm = new UavStateMachine();
        ConnectionState? raised = null;
        sm.ConnectionStateChanged += (_, s) => raised = s;

        sm.TransitionConnection(ConnectionState.Connecting);

        raised.ShouldBe(ConnectionState.Connecting);
        sm.ConnectionState.ShouldBe(ConnectionState.Connecting);
    }

    [Fact]
    public void TransitionMission_ToActive_WhileNotConnected_Throws()
    {
        var sm = new UavStateMachine();

        Should.Throw<InvalidStateTransitionException>(() => sm.TransitionMission(MissionState.Active));

        sm.MissionState.ShouldBe(MissionState.Idle);
    }

    [Fact]
    public void TransitionMission_ToActive_WhileConnected_Succeeds()
    {
        var sm = new UavStateMachine();
        Connect(sm);

        sm.TransitionMission(MissionState.Active);

        sm.MissionState.ShouldBe(MissionState.Active);
    }

    [Theory]
    [InlineData(MissionState.Idle, MissionState.Stopped)]
    [InlineData(MissionState.Idle, MissionState.EmergencyAbort)]
    [InlineData(MissionState.Active, MissionState.Idle)]
    [InlineData(MissionState.Stopped, MissionState.Active)]
    public void TransitionMission_ToStructurallyIllegalState_Throws(MissionState from, MissionState to)
    {
        var sm = new UavStateMachine();
        Connect(sm);
        DriveMissionTo(sm, from);

        Should.Throw<InvalidStateTransitionException>(() => sm.TransitionMission(to));
    }

    [Fact]
    public void FullMissionLifecycle_StartPauseResumeStop_Succeeds()
    {
        var sm = new UavStateMachine();
        Connect(sm);

        sm.TransitionMission(MissionState.Active);
        sm.TransitionMission(MissionState.Paused);
        sm.TransitionMission(MissionState.Active);
        sm.TransitionMission(MissionState.Stopped);
        sm.TransitionMission(MissionState.Idle);

        sm.MissionState.ShouldBe(MissionState.Idle);
    }

    [Theory]
    [InlineData(MissionState.Active)]
    [InlineData(MissionState.Paused)]
    public void ConnectionLost_WhileMissionInFlight_ForcesEmergencyAbort(MissionState inFlightState)
    {
        var sm = new UavStateMachine();
        Connect(sm);
        sm.TransitionMission(MissionState.Active);
        if (inFlightState == MissionState.Paused)
        {
            sm.TransitionMission(MissionState.Paused);
        }

        sm.TransitionConnection(ConnectionState.Disconnected);

        sm.MissionState.ShouldBe(MissionState.EmergencyAbort);
        sm.ConnectionState.ShouldBe(ConnectionState.Disconnected);
    }

    [Fact]
    public void ConnectionLost_WhileMissionIdle_DoesNotTouchMissionState()
    {
        var sm = new UavStateMachine();
        Connect(sm);

        sm.TransitionConnection(ConnectionState.Disconnected);

        sm.MissionState.ShouldBe(MissionState.Idle);
    }

    [Fact]
    public void TerminalStates_Stopped_And_EmergencyAbort_CanTransitionBackToIdle()
    {
        // Supporting evidence for docs/defects/07 and docs/defects/08: the state machine
        // itself is NOT the bug. Both Stopped->Idle and EmergencyAbort->Idle are legal edges
        // in MissionTransitions. The defect in both cases lives in the App layer, which never
        // actually calls TransitionMission(Idle) to take this edge after a Stop
        // (MissionControlViewModel.Stop()) or after a reconnect following an EmergencyAbort
        // (nowhere at all). See MissionControlViewModelTests.StartCommand_ShouldReenable_AfterStop_DEFECT07
        // and WarningsBannerViewModelTests.IsEmergencyAbort_ShouldClear_AfterReconnecting_DEFECT08
        // for the App-layer tests that fail because of that missing call.
        var stoppedMachine = new UavStateMachine();
        Connect(stoppedMachine);
        stoppedMachine.TransitionMission(MissionState.Active);
        stoppedMachine.TransitionMission(MissionState.Stopped);

        stoppedMachine.CanTransitionMission(MissionState.Idle).ShouldBeTrue();

        var abortMachine = new UavStateMachine();
        Connect(abortMachine);
        abortMachine.TransitionMission(MissionState.Active);
        abortMachine.TransitionConnection(ConnectionState.Disconnected); // forces EmergencyAbort

        abortMachine.MissionState.ShouldBe(MissionState.EmergencyAbort);
        abortMachine.CanTransitionMission(MissionState.Idle).ShouldBeTrue();
    }

    private static void Connect(UavStateMachine sm)
    {
        sm.TransitionConnection(ConnectionState.Connecting);
        sm.TransitionConnection(ConnectionState.Connected);
    }

    private static void DriveConnectionTo(UavStateMachine sm, ConnectionState state)
    {
        if (state == ConnectionState.Disconnected)
        {
            return;
        }

        sm.TransitionConnection(ConnectionState.Connecting);
        if (state == ConnectionState.Connected)
        {
            sm.TransitionConnection(ConnectionState.Connected);
        }
    }

    private static void DriveMissionTo(UavStateMachine sm, MissionState state)
    {
        if (state == MissionState.Idle)
        {
            return;
        }

        sm.TransitionMission(MissionState.Active);
        if (state == MissionState.Active)
        {
            return;
        }

        sm.TransitionMission(state);
    }
}
