# Defect #05 — Pause does not stop the mission elapsed-time clock

**Severity:** Major (mission telemetry — elapsed time — is silently wrong)
**Priority:** P2
**Status:** Fixed
**Found by:** Functional testing (timer-state unit test)
**Component:** `UavMissionControl.App.ViewModels.MissionControlViewModel`

## Summary

`Pause()` transitioned `MissionState` to `Paused` and logged the event, but no longer called
`_elapsedTimer.Stop()`. The `DispatcherTimer` driving the elapsed-time display kept running, so
the mission clock kept advancing while the mission was supposedly paused.

## Repro

1. Connect, click **Start**, wait a couple of seconds.
2. Click **Pause**.
3. Expected: the elapsed time display stops advancing.
4. Actual (defect present): the elapsed time keeps counting up as if the mission were still Active.

## Root cause

Straightforward omission — `Stop()`/`TransitionMission`/`Add` are three independent statements
in `Pause()` with no compiler-enforced link between "mission state says paused" and "the clock
that visually represents elapsed mission time is actually stopped." Removing one line silently
breaks the invariant with no build error and no immediately obvious symptom outside of watching
the clock.

## Detection

`DispatcherTimer.IsEnabled` reflects `Start()`/`Stop()` calls immediately and does not require a
running Dispatcher message loop — so `MissionControlViewModelTests` exposes it via an `internal`
test seam (`IsElapsedTimerRunning`) and asserts on it directly, rather than trying to wait for a
real tick in a headless test host (which wouldn't fire without a message pump anyway). This is
the same "test the seam, not the symptom" approach used for defect #03's dispatcher-marshaling
check: instead of racing real time or a real UI thread, assert on the piece of state that
*guarantees* correct behavior.

Three tests cover the elapsed timer's start/stop lifecycle (`Start_BeginsTheElapsedTimer`,
`Pause_StopsTheElapsedTimer`, `Resume_RestartsTheElapsedTimer`); only the Pause one failed,
correctly isolating exactly which transition broke.

## Fix

Restored `_elapsedTimer.Stop();` as the first line of `Pause()`.

## Regression coverage

`MissionControlViewModelTests.Pause_StopsTheElapsedTimer` (added proactively before this defect
was planted, alongside its Start/Resume siblings for symmetric coverage of the same lifecycle).
