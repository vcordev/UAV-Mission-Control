# Final Report

## What was built

An original, independent WPF desktop application — **UAV Mission Control** — simulating
connection management, mission control (start/pause/resume/stop), live telemetry (battery,
signal, GPS, altitude, speed), threshold-based warnings, and an event/log history, governed by an
explicit two-part state machine. It is not a copy of any real product; it exists specifically to
demonstrate, end-to-end, the skills commonly asked for in Mid-Level QA Engineer roles for
desktop/WPF applications — test strategy and planning, functional/negative/boundary/regression/
reliability/performance testing, automated UI testing with tooling appropriate to the platform
(not a browser-automation tool blindly applied), defect tracking, and CI/CD — by building both
the system under test *and* the QA program around it.

## Architecture

`UavMissionControl.Core` (no WPF dependency) owns all domain logic — the state machine, telemetry
simulation, event logging, warning thresholds. `UavMissionControl.App` is a thin WPF/MVVM layer
over it, with a testability seam (`IUiDispatcher`) specifically so ViewModels reacting to
background-thread events can be unit tested without a running `Application`. Full detail:
`docs/architecture.md`.

## Technologies

.NET 10, WPF, hand-rolled MVVM, xUnit 2.9.3, Shouldly, Moq, FlaUI 5.0.0, GitHub Actions. Every
choice is written up with alternatives and trade-offs in `docs/decisions.md`.

## QA strategy

Three-layer test pyramid (Core/App/UI automation), risk-based prioritization (exhaustive coverage
on the state machine and every background-thread-reachable UI mutation), and eight named testing
techniques each mapped to where it's actually used. Full detail: `docs/test-strategy.md`.

## Tests

**106 automated tests, all green:** 55 in `Core.Tests`, 39 in `App.Tests`, 12 in
`UiAutomation.Tests` (FlaUI, driving the real built exe). Plus one documented manual test case
(`docs/test-cases/TC-001-happy-path-mission-lifecycle.md`), executed against the running app and
screenshot-verified.

## Bugs found and fixed

Six defects, each deliberately planted, caught by a specific testing technique, and documented
with repro/root-cause/fix/regression-coverage in `docs/defects/`:

| # | Defect | Caught by | Severity/Priority |
|---|---|---|---|
| 01 | Low-battery boundary off-by-one | Boundary-value analysis | Major/P2 |
| 02 | Stop-button defense-in-depth gap | Negative/state-transition testing | Major/P2 |
| 03 | Event log cross-thread crash | Dispatcher-seam unit test + live reliability repro | **Critical/P1** |
| 04 | Reconnect handler leak (duplicate logs) | Regression testing | Minor/P3 |
| 05 | Pause doesn't stop elapsed timer | Functional testing (timer-state seam) | Major/P2 |
| 06 | Event log O(n) prepend | Performance testing | Minor/P3 |

Two of these (#03, #04) required adding a small, genuinely useful feature first
(`TelemetryWarningMonitor`, a one-shot reconnect notice) specifically to create the code path the
defect could live in — documented honestly as such rather than presented as if they were always
there.

## Performance & reliability findings

Real, measured numbers, not estimates: the event-log performance defect showed a 20x cost
increase for an 11x collection-size increase before the fix, and effectively 0x after. Soak
testing ran the telemetry pipeline for 100,000 ticks (Core.Tests) and stress-cycled the live app
for ~18 seconds of sustained background-thread activity (UiAutomation.Tests) without failure.
Full detail, including what these numbers do *not* claim (no profiler-grade leak detection, no
load testing — this is a single-user desktop app): `docs/performance-report.md`.

## CI/CD

GitHub Actions, two jobs on `windows-latest`, both required (no allow-failure). Pushed to
`https://github.com/vcordev/qa-challenge` and confirmed green in the Actions tab. One real
concurrency gotcha (xUnit's default test-class parallelization colliding with UI Automation's COM
interop) was found and fixed with a one-line, standard, documented fix. Full detail:
`docs/ci-cd.md`.

## Important decisions

Eight ADR-style entries in `docs/decisions.md`, covering the .NET 10 target, the FlaUI vs.
Selenium/Appium/WinAppDriver choice, the Shouldly-over-FluentAssertions license consideration, and
the auto-scroll feature that was tried, found to cause a real crash, and deliberately reverted
rather than over-engineered further.

## Limitations (stated plainly, not hidden)

- No localization/globalization, no formal accessibility audit beyond what UI Automation's
  `AutomationId` requirements already provide, no cross-Windows-version test matrix, no
  multi-user load testing (this is a single-user desktop app — that category doesn't apply).
- The soak tests run many ticks in a tight loop for CI-friendly speed, not literal multi-hour
  wall-clock survival.
- `GC.GetTotalMemory` is a coarse memory-growth sanity check, not a precision leak detector — a
  real investigation would use a profiler.
- The 6 planted defects were designed to be realistic and are documented honestly as
  deliberately introduced, not discovered in "production" — that distinction is made explicit
  everywhere it matters (see each `docs/defects/*.md`).

## What to review before the interview

1. `docs/test-strategy.md` — the technique→defect mapping table is likely the single
   highest-value artifact to have fresh.
2. `docs/defects/03-eventlog-cross-thread-crash.md` — the most substantial defect, with a real
   reproduced stack trace and a two-pronged (unit + live) detection story.
3. `docs/decisions.md` D4 (FlaUI vs. Selenium/Appium) and D7 (the auto-scroll revert) — both
   directly demonstrate judgment, not just tool knowledge.
4. `docs/interview/likely-questions.md` — rehearsed, honest answers grounded only in this project.
5. Actually run the app (`dotnet run --project src/UavMissionControl.App`) and the full test
   suite (`dotnet test UavMissionControl.slnx`) once beforehand, so the numbers being discussed
   are freshly verified, not just remembered.
