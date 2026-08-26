using Shouldly;
using UavMissionControl.App.ViewModels;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.App.Tests.ViewModels;

public class MissionControlViewModelTests
{
    [Fact]
    public void Initial_Idle_Disconnected_OnlyNoCommandIsExecutable()
    {
        var vm = Create(out _);

        vm.StartCommand.CanExecute(null).ShouldBeFalse();
        vm.PauseCommand.CanExecute(null).ShouldBeFalse();
        vm.ResumeCommand.CanExecute(null).ShouldBeFalse();
        vm.StopCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void StartCommand_CanExecute_RequiresBothIdleAndConnected()
    {
        var vm = Create(out var stateMachine);

        vm.StartCommand.CanExecute(null).ShouldBeFalse(); // Idle, Disconnected

        Connect(stateMachine);
        vm.StartCommand.CanExecute(null).ShouldBeTrue(); // Idle, Connected
    }

    [Fact]
    public void Start_TransitionsToActive_AndResetsElapsedToZero()
    {
        var vm = Create(out var stateMachine);
        Connect(stateMachine);

        vm.StartCommand.Execute(null);

        vm.MissionState.ShouldBe(MissionState.Active);
        vm.ElapsedDisplay.ShouldBe("00:00");
    }

    [Theory]
    [InlineData(MissionState.Idle, false)]
    [InlineData(MissionState.Active, true)]
    [InlineData(MissionState.Paused, true)]
    [InlineData(MissionState.Stopped, false)]
    public void StopCommand_CanExecute_OnlyWhenActiveOrPaused(MissionState state, bool expected)
    {
        var vm = Create(out var stateMachine);
        Connect(stateMachine);
        DriveTo(vm, state);

        vm.StopCommand.CanExecute(null).ShouldBe(expected);
    }

    [Fact]
    public void StopCommand_CanExecute_IsFalse_WhenConnectedButMissionIdle()
    {
        // The scenario the "defense-in-depth" defect (see docs/defects) breaks: connected,
        // but no mission ever started — Stop must stay disabled or clicking it throws from Core.
        var vm = Create(out var stateMachine);
        Connect(stateMachine);

        vm.MissionState.ShouldBe(MissionState.Idle);
        vm.StopCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Stop_WhenExecutedDespiteInvalidState_ThrowsFromCore()
    {
        // Defense in depth: even if a future change lets the Stop button become clickable in a
        // state where stopping is illegal (see docs/defects/02), the domain must still refuse
        // the transition rather than corrupting state. This bypasses CanExecute deliberately.
        var vm = Create(out var stateMachine);
        Connect(stateMachine);

        Should.Throw<InvalidStateTransitionException>(() => vm.StopCommand.Execute(null));
    }

    [Fact]
    public void PauseThenResume_ReturnsToActive()
    {
        var vm = Create(out var stateMachine);
        Connect(stateMachine);
        vm.StartCommand.Execute(null);

        vm.PauseCommand.Execute(null);
        vm.MissionState.ShouldBe(MissionState.Paused);

        vm.ResumeCommand.Execute(null);
        vm.MissionState.ShouldBe(MissionState.Active);
    }

    [Fact]
    public void Start_BeginsTheElapsedTimer()
    {
        var vm = Create(out var stateMachine);
        Connect(stateMachine);

        vm.StartCommand.Execute(null);

        vm.IsElapsedTimerRunning.ShouldBeTrue();
    }

    [Fact]
    public void Pause_StopsTheElapsedTimer()
    {
        var vm = Create(out var stateMachine);
        Connect(stateMachine);
        vm.StartCommand.Execute(null);

        vm.PauseCommand.Execute(null);

        vm.IsElapsedTimerRunning.ShouldBeFalse();
    }

    [Fact]
    public void Resume_RestartsTheElapsedTimer()
    {
        var vm = Create(out var stateMachine);
        Connect(stateMachine);
        vm.StartCommand.Execute(null);
        vm.PauseCommand.Execute(null);

        vm.ResumeCommand.Execute(null);

        vm.IsElapsedTimerRunning.ShouldBeTrue();
    }

    [Fact]
    public void Stop_TransitionsToStopped_AndLogsElapsed()
    {
        var eventLog = new EventLog();
        var stateMachine = new UavStateMachine();
        var vm = new MissionControlViewModel(stateMachine, eventLog);
        Connect(stateMachine);
        vm.StartCommand.Execute(null);

        vm.StopCommand.Execute(null);

        vm.MissionState.ShouldBe(MissionState.Stopped);
        eventLog.Entries.Select(e => e.Message).ShouldContain(m => m.StartsWith("Mission stopped."));
    }

    [Fact]
    public void ConnectionLost_WhileActive_ForcesEmergencyAbort_AndLogsIt()
    {
        var eventLog = new EventLog();
        var stateMachine = new UavStateMachine();
        var vm = new MissionControlViewModel(stateMachine, eventLog);
        Connect(stateMachine);
        vm.StartCommand.Execute(null);

        stateMachine.TransitionConnection(ConnectionState.Disconnected);

        vm.MissionState.ShouldBe(MissionState.EmergencyAbort);
        eventLog.Entries.ShouldContain(e => e.Severity == LogSeverity.Error
                                            && e.Message.Contains("aborted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "KnownDefect")]
    public void StartCommand_ShouldReenable_AfterStop_DEFECT07()
    {
        // KNOWN DEFECT - see docs/defects/07-stop-mission-no-idle-transition.md.
        // Deliberately left FAILING: Stop() transitions MissionState to Stopped but never on
        // to Idle, so StartCommand's CanExecute guard (MissionState == Idle) never re-opens
        // and the user can never start a second mission in the same session. This test
        // documents and automatically detects that regression; it is intentionally NOT fixed.
        // Tagged [Trait("Category","KnownDefect")] so `dotnet test --filter "Category!=KnownDefect"`
        // reproduces the clean, all-green demo run without hiding this test's existence.
        var vm = Create(out var stateMachine);
        Connect(stateMachine);
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.MissionState.ShouldBe(MissionState.Stopped);

        // Expected (correct) behavior: a stopped mission should allow starting a new one.
        vm.StartCommand.CanExecute(null).ShouldBeTrue();
    }

    private static MissionControlViewModel Create(out UavStateMachine stateMachine)
    {
        stateMachine = new UavStateMachine();
        return new MissionControlViewModel(stateMachine, new EventLog());
    }

    private static void Connect(UavStateMachine stateMachine)
    {
        stateMachine.TransitionConnection(ConnectionState.Connecting);
        stateMachine.TransitionConnection(ConnectionState.Connected);
    }

    private static void DriveTo(MissionControlViewModel vm, MissionState state)
    {
        if (state == MissionState.Idle)
        {
            return;
        }

        vm.StartCommand.Execute(null);
        if (state == MissionState.Active)
        {
            return;
        }

        if (state == MissionState.Paused)
        {
            vm.PauseCommand.Execute(null);
            return;
        }

        vm.StopCommand.Execute(null);
    }
}
