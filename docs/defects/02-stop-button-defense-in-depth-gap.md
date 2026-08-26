# Defect #02 — Stop Mission button enabled in states where stopping is illegal

**Severity:** Major (user-facing action leads to an unhandled domain exception)
**Priority:** P2
**Status:** Fixed
**Found by:** Negative testing / state-transition testing, App.Tests
**Component:** `UavMissionControl.App.ViewModels.MissionControlViewModel`

## Summary

`StopCommand`'s `CanExecute` predicate checked `ConnectionState == Connected` instead of
`MissionState is Active or Paused`. As soon as the UAV connects, the Stop button becomes
clickable even though no mission has started (`MissionState.Idle`) or one has already been
stopped (`MissionState.Stopped`) — both states where `UavStateMachine.TransitionMission(Stopped)`
is illegal and throws `InvalidStateTransitionException`.

This is a **defense-in-depth gap**: the domain layer (`UavStateMachine`) already, correctly,
refuses the illegal transition on its own — that guard was never broken. The defect is that the
*UI* stopped pre-empting an action the user should never have been able to attempt in the first
place, pushing an avoidable exception onto a layer that shouldn't need to handle it.

## Repro

1. Launch the app, click **Connect**, wait for "Connected".
2. Do **not** click Start.
3. Observe: **Stop** is enabled (it should be disabled — no mission is running).
4. Click **Stop**.
5. Actual: `InvalidStateTransitionException: Cannot transition MissionState from 'Idle' to 'Stopped'.`
   surfaces (in a shipped build this would be an unhandled-exception crash, since the UI has no
   reason to expect Stop to ever throw).

## Root cause

Likely a copy/paste or find-and-replace slip while wiring up the four mission commands' guards —
`ConnectionState == Connected` is the correct (and correctly used) guard for `StartCommand`, and
was pasted into `StopCommand` in place of the mission-state check it actually needed.

## Two-layer coverage

| Layer | Test | What it proves |
|---|---|---|
| Core | `UavStateMachineTests.TransitionMission_ToStructurallyIllegalState_Throws(Idle, Stopped)` (existing, Phase 2) | The domain independently refuses the illegal transition — this guard was never broken |
| App | `MissionControlViewModelTests.StopCommand_CanExecute_IsFalse_WhenConnectedButMissionIdle` and the `Idle`/`Stopped` cases of `StopCommand_CanExecute_OnlyWhenActiveOrPaused` | The UI must not let the user attempt the illegal transition in the first place |
| App | `Stop_WhenExecutedDespiteInvalidState_ThrowsFromCore` (added alongside this defect) | Documents, permanently, that Core still refuses even if a future change reintroduces a CanExecute gap |

Both layers are independently tested on purpose: a UI-only fix without the Core guard would
leave the app one refactor away from actually corrupting state; a Core-only guard without the
UI fix leaves users hitting a jarring, avoidable crash.

## Fix

Restored `StopCommand`'s guard to `_stateMachine.MissionState is MissionState.Active or MissionState.Paused`.

## Regression coverage

Three existing App.Tests catch this the moment it regresses; no new test was required beyond the
one added for extra documentation (`Stop_WhenExecutedDespiteInvalidState_ThrowsFromCore`), which
passes regardless of this defect's presence since it exercises the Core guard directly.
