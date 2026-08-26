# TC-002 — Stop Mission blocks starting a second mission

**Type:** Manual / exploratory (automated in `KnownDefectsUiTests.StartButton_ShouldReenable_AfterStop_DEFECT07`)
**Priority:** P2
**Area:** Mission Control
**Related defect:** `docs/defects/07-stop-mission-no-idle-transition.md`

## Preconditions

- App built (`dotnet build UavMissionControl.slnx`) and launched (`dotnet run --project src/UavMissionControl.App`).

## Steps and expected results

| # | Step | Expected result | Actual (2026-08-26) |
|---|---|---|---|
| 1 | Click **Connect**, wait for "Connected" | Status: Connected | Pass |
| 2 | Click **Start** | Mission: Active, elapsed clock starts | Pass |
| 3 | Wait ~3 seconds | Elapsed advances (e.g. 00:03) | Pass |
| 4 | Click **Stop** | Mission: Stopped; log shows "Mission stopped. Elapsed: 00:03" | Pass |
| 5 | Click **Start** again | Mission should return to Active and a new elapsed count should begin from 00:00 | **FAIL — Start button is disabled and cannot be clicked** |
| 6 | Look for any other control that resets the mission (Reset, New Mission, etc.) | Some control lets the user begin a new mission without relaunching the app | **FAIL — no such control exists anywhere in the UI** |

## Result

**FAIL at step 5.** Once a mission is Stopped, `MissionState` never returns to `Idle`, so
`StartCommand` never re-enables. The only way to run a second mission in this build is to close
and relaunch the application. See `docs/defects/07-stop-mission-no-idle-transition.md` for full
root-cause detail and automated regression coverage (`MissionControlViewModelTests` and
`KnownDefectsUiTests`, both intentionally red).

## Notes

This defect was found by going one step past the existing happy-path script
(`TC-001-happy-path-mission-lifecycle.md`), which stops at "Stop" and never attempts a second
Start. It is a good example of why a test plan needs to explicitly cover "and then do it again,"
not just a single pass through a workflow.
