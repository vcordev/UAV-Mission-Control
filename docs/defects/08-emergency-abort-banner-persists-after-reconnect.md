# Defect #08 — Emergency Abort banner never clears after reconnecting

**Severity:** Major (persistent false alarm — the UI keeps claiming the connection/mission is in
an emergency state long after it has actually recovered)
**Priority:** P2
**Status:** Detected — deliberately left unresolved (see "Why this is left unresolved" below)
**Found by:** Manual exploratory testing (recovery path after `RegressionUiTests.ConnectionLoss_WhileMissionActive_ShowsEmergencyBannerAndAbortsMission`
was never exercised — that test proves the banner appears, but nothing proves it ever disappears)
**Component:** `UavMissionControl.App.ViewModels.WarningsBannerViewModel`, `MissionControlViewModel`

## Summary

`WarningsBannerViewModel.IsEmergencyAbort` is a direct, always-live projection of mission state:

```csharp
public bool IsEmergencyAbort => _mission.MissionState == MissionState.EmergencyAbort;
```

This is bound in `WarningsBannerView.xaml` to a `DarkRed` banner reading
"EMERGENCY ABORT: connection lost during mission". Losing connection during an active/paused
mission correctly forces `MissionState` to `EmergencyAbort`
(`UavStateMachine.TransitionConnection`), and the banner correctly lights up. The problem is
what happens next: `EmergencyAbort -> Idle` is a legal edge in `UavStateMachine.MissionTransitions`,
but — same root pattern as Defect #07 — **nothing in the App layer ever takes it**. Reconnecting
only calls `TransitionConnection(Connecting)` then `TransitionConnection(Connected)`; it never
touches `MissionState` at all. So `MissionState` stays `EmergencyAbort` forever, and the banner
stays lit forever, even though the connection and telemetry are both healthy again.

## Repro

1. Launch the app, click **Connect**, wait for "Connected".
2. Click **Start** → Mission: Active.
3. Click **Simulate Connection Loss** → Mission: EmergencyAbort; red "EMERGENCY ABORT" banner
   appears; connection drops to Disconnected. (This much is correct and already covered by
   `RegressionUiTests.ConnectionLoss_WhileMissionActive_ShowsEmergencyBannerAndAbortsMission`.)
4. Click **Connect** again, wait for "Connected".
5. **Actual:** Status correctly shows "Connected" and telemetry resumes updating, but the red
   "EMERGENCY ABORT" banner is still on screen. `MissionState` is still `EmergencyAbort`.
   `Clear` (the QA scenario-clear button) has no effect on it either — `ClearScenariosCommand`
   only clears forced battery/signal values, not mission state.

## Root cause

Same architectural gap as Defect #07: a terminal `MissionState` (`Stopped` in #07,
`EmergencyAbort` here) has a legal `-> Idle` edge defined in the domain layer, but no code path
in the App layer ever performs that transition — not automatically on reconnect, and not via any
explicit user action. The two defects are worth understanding together in an interview: they are
not two unrelated bugs, they are the same missing behavior ("who is responsible for returning to
Idle after a terminal state?") surfacing in two different places.

## Why this is left unresolved

Kept in the codebase, unfixed, on purpose — to demonstrate that a *stale UI state* defect (as
opposed to a crash or an outright wrong value) can still be pinned down with a precise,
repeatable, multi-layer automated assertion instead of relying on "it still looked red to me
during manual testing." See `docs/test-strategy.md` for how these are excluded from the CI gate
without hiding them.

## Test coverage (multi-layer)

| Layer | Test | Outcome | What it proves |
|---|---|---|---|
| Core | `UavStateMachineTests.TerminalStates_Stopped_And_EmergencyAbort_CanTransitionBackToIdle` | **PASSES** | The domain layer is not at fault — `EmergencyAbort -> Idle` is a legal transition the state machine already supports |
| App | `WarningsBannerViewModelTests.IsEmergencyAbort_ShouldClear_AfterReconnecting_DEFECT08` | **FAILS (by design)** | `IsEmergencyAbort` stays `true` after a full reconnect cycle, proving the App layer never takes the legal `EmergencyAbort -> Idle` edge |
| UI Automation | `KnownDefectsUiTests.EmergencyBanner_ShouldClear_AfterReconnecting_DEFECT08` | **FAILS (by design)** | The real, running app's red banner stays visibly on screen after a full, successful reconnect — reproduces end-to-end |

## Manual test coverage

`docs/test-cases/TC-003-emergency-banner-persists-after-reconnect.md` — documents the exact
manual repro above, with a FAIL result on the step that checks the banner after reconnecting.

## What a real fix would look like (not applied)

Either:
- Reconnecting (`ConnectionState -> Connected`) also transitions `MissionState` from
  `EmergencyAbort` back to `Idle` automatically, or
- A new, explicit "Acknowledge"/"Clear Emergency" action the user takes once they've confirmed
  the aircraft/link is actually safe again, so the transition isn't silently automatic for a
  safety-relevant state.

Neither is implemented here, deliberately.
