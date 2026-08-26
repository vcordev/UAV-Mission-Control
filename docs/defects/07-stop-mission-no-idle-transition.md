# Defect #07 — Stop Mission never transitions back to Idle, permanently disabling Start

**Severity:** Major (user-facing blocker — a second mission can never be started in the same session)
**Priority:** P2
**Status:** Detected — deliberately left unresolved (see "Why this is left unresolved" below)
**Found by:** Manual exploratory testing (gap in `TC-001`'s happy-path script, which stops at Stop and never tries Start again)
**Component:** `UavMissionControl.App.ViewModels.MissionControlViewModel`

## Summary

`Stop()` transitions `MissionState` to `Stopped` and never goes any further:

```csharp
private void Stop()
{
    _elapsedTimer.Stop();
    _stateMachine.TransitionMission(MissionState.Stopped);
    _eventLog.Add(LogSeverity.Info, $"Mission stopped. Elapsed: {ElapsedDisplay}");
}
```

`StartCommand`'s guard requires `MissionState == MissionState.Idle`:

```csharp
StartCommand = new RelayCommand(
    Start,
    () => _stateMachine.MissionState == MissionState.Idle
          && _stateMachine.ConnectionState == ConnectionState.Connected);
```

`Stopped -> Idle` is a **legal** edge in `UavStateMachine.MissionTransitions` — the domain layer
was built to support exactly this recovery path — but nothing in the App layer ever calls
`TransitionMission(MissionState.Idle)` to take that edge. The mission is left permanently
"Stopped," `StartCommand.CanExecute` never becomes true again, and the user cannot start a
second mission without restarting the whole application.

## Repro

1. Launch the app, click **Connect**, wait for "Connected".
2. Click **Start** → Mission: Active.
3. Click **Stop** → Mission: Stopped.
4. Try to click **Start** again.
5. **Actual:** Start button is disabled. There is no button, menu item, or command anywhere in
   the UI that returns `MissionState` to `Idle`. The only way to run a second mission is to
   close and relaunch the app.

## Root cause

The state machine correctly models `Stopped -> Idle` as a legal recovery transition (see
`UavStateMachine.cs`, `MissionTransitions[MissionState.Stopped] = [MissionState.Idle]`), but
`MissionControlViewModel.Stop()` only performs the first half of the intended recovery
(`Active/Paused -> Stopped`) and was never finished with the second half
(`Stopped -> Idle`, or an explicit user-triggered "Reset"/"New Mission" action). This is a gap
in the **application layer**, not the domain layer — see the Core-level supporting test below.

## Why this is left unresolved

This defect is being kept in the codebase, unfixed, on purpose: to demonstrate that a defect
found through manual exploratory testing can be captured as a **permanent, automated regression
proof** at more than one layer, before anyone touches the fix. The three tests below are
expected to show 2 real (not skipped) failures — that is the point, not a mistake. See
`docs/test-strategy.md` for how these are excluded from the CI gate without hiding them.

## Test coverage (multi-layer)

| Layer | Test | Outcome | What it proves |
|---|---|---|---|
| Core | `UavStateMachineTests.TerminalStates_Stopped_And_EmergencyAbort_CanTransitionBackToIdle` | **PASSES** | The domain layer is not at fault — `Stopped -> Idle` is a legal transition that the state machine already supports |
| App | `MissionControlViewModelTests.StartCommand_ShouldReenable_AfterStop_DEFECT07` | **FAILS (by design)** | `StartCommand.CanExecute` stays `false` after `Stop()`, proving the App layer never takes the legal `Stopped -> Idle` edge |
| UI Automation | `KnownDefectsUiTests.StartButton_ShouldReenable_AfterStop_DEFECT07` | **FAILS (by design)** | The real, running app's Start button stays visibly disabled after Stop — the same defect reproduces end-to-end, not just in an isolated ViewModel test |

## Manual test coverage

`docs/test-cases/TC-002-stop-mission-blocks-restart.md` — documents the exact manual repro
above, with a FAIL result on the step that tries to start a second mission.

## What a real fix would look like (not applied)

Either:
- `Stop()` also calls `_stateMachine.TransitionMission(MissionState.Idle)` immediately after
  `Stopped` (auto-reset), or
- A new, explicit `ResetCommand`/"New Mission" button that performs `Stopped -> Idle` on user
  demand, so the "mission stopped" state remains visible until the user is ready to reset it.

Neither is implemented here, deliberately.
