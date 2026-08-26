# TC-001 — Happy-path mission lifecycle

**Type:** Manual / exploratory (executed against the running app; automated in Phase 6 as the FlaUI smoke test)
**Priority:** P1
**Area:** Connection, Mission Control, Telemetry, Warnings

## Preconditions

- App built (`dotnet build UavMissionControl.slnx`) and launched (`dotnet run --project src/UavMissionControl.App`).

## Steps and expected results

| # | Step | Expected result | Actual (2026-08-26) |
|---|---|---|---|
| 1 | Launch the app | Window titled "UAV Mission Control" opens; Connection=Disconnected, Mission=Idle, Battery/Signal=100%, all Start/Pause/Resume/Stop/Disconnect buttons disabled, Connect enabled | Pass |
| 2 | Click **Connect** | Status shows "Connecting..." briefly, then "Connected"; Disconnect enabled, Connect disabled; telemetry begins updating | Pass |
| 3 | Click **Start** | Mission state → Active; elapsed clock begins counting up from 00:00 | Pass |
| 4 | Wait ~2 seconds | Elapsed clock has advanced (not stuck at 00:00) | Pass (00:02) |
| 5 | Click **Pause** | Mission state → Paused; elapsed clock stops advancing | Pass |
| 6 | Wait ~2 seconds while Paused | Elapsed clock value unchanged from step 5 | Pass |
| 7 | Click **Resume** | Mission state → Active again; clock resumes counting | Pass |
| 8 | Click **Stop** | Mission state → Stopped | Pass |
| 9 | Click **Critical Battery** (QA test scenario) | Critical-battery banner (red, "CRITICAL BATTERY") appears | Pass |
| 10 | Click **Clear** | Forced scenario cleared, banner disappears on next tick | Pass |
| 11 | Click **Simulate Connection Loss** | Connection → Disconnected; event log records the simulated loss | Pass |

## Result

**PASS** — all 11 steps behaved as expected. Executed both visually (screenshot of initial render) and driven programmatically via FlaUI (UIA3) attaching to the running process, exercising the exact automation IDs the Phase 6 UI automation suite will use.

## Notes / findings during this pass

- Found and fixed during this session (not one of the tracked planted defects): WPF `Border` elements have no default UI Automation peer, so a `Border`'s `Visibility` binding alone is invisible to UI Automation / FlaUI regardless of its actual on-screen state. Fixed by anchoring automation IDs to the `TextBlock` inside each warning banner instead of the `Border` — see `src/UavMissionControl.App/Views/WarningsBannerView.xaml`. Documented in `docs/learning/wpf-and-mvvm.md` as a real gotcha worth knowing before writing WPF UI automation.
