# TC-003 — Emergency Abort banner persists after successful reconnect

**Type:** Manual / exploratory (automated in `KnownDefectsUiTests.EmergencyBanner_ShouldClear_AfterReconnecting_DEFECT08`)
**Priority:** P2
**Area:** Warnings Banner, Connection
**Related defect:** `docs/defects/08-emergency-abort-banner-persists-after-reconnect.md`

## Preconditions

- App built (`dotnet build UavMissionControl.slnx`) and launched (`dotnet run --project src/UavMissionControl.App`).

## Steps and expected results

| # | Step | Expected result | Actual (2026-08-26) |
|---|---|---|---|
| 1 | Click **Connect**, wait for "Connected" | Status: Connected | Pass |
| 2 | Click **Start** | Mission: Active | Pass |
| 3 | Click **Simulate Connection Loss** | Status: Disconnected; Mission: EmergencyAbort; red "EMERGENCY ABORT: connection lost during mission" banner appears | Pass |
| 4 | Click **Connect** again, wait for "Connected" | Status: Connected; telemetry resumes updating | Pass |
| 5 | Look at the warnings banner area | The red "EMERGENCY ABORT" banner should have disappeared now that the connection has recovered | **FAIL — banner is still visible, still red** |
| 6 | Click **Clear** (QA scenario-clear button) | Does not affect the banner | **FAIL — Clear only resets forced battery/signal values; the banner is unaffected either way** |
| 7 | Click **Start** to begin a new mission now that the connection is healthy | Mission should be startable | **FAIL — Start is disabled; MissionState is still EmergencyAbort, same underlying gap as TC-002/Defect #07** |

## Result

**FAIL at step 5.** `MissionState` never transitions from `EmergencyAbort` back to `Idle` on
reconnect, so the derived `IsEmergencyAbort` flag — and the red banner bound to it — stays lit
indefinitely, regardless of the actual (healthy) connection and telemetry state. See
`docs/defects/08-emergency-abort-banner-persists-after-reconnect.md` for full root-cause detail
and automated regression coverage (`WarningsBannerViewModelTests` and `KnownDefectsUiTests`, both
intentionally red).

## Notes

Step 7 shows this defect compounds with Defect #07 (`TC-002`): after an emergency abort, the
mission is just as stuck as after a normal Stop, for the same reason — nothing ever takes the
legal `-> Idle` edge back.
