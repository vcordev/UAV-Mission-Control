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

**111 automated tests: 107 green + 4 intentionally red.** 56 in `Core.Tests` (all passing), 41
in `App.Tests` (39 passing + 2 intentionally failing), 14 in `UiAutomation.Tests` (12 passing +
2 intentionally failing; FlaUI, driving the real built exe). Plus three documented manual test
cases (`docs/test-cases/TC-001` through `TC-003`), executed against the running app.

The 4 intentionally-failing tests are not a build problem — they are permanent, automated proof
of the 2 known, deliberately-unresolved defects below (#07, #08), tagged
`[Trait("Category","KnownDefect")]` and excluded from the CI gate by filter (not skipped, not
hidden — see `docs/test-strategy.md`). `dotnet test --filter "Category!=KnownDefect"`
reproduces the clean 107/107 green run.

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

## Bugs found and deliberately left unresolved

Two more defects were found — the same way real ones usually are, by manual exploratory testing
that pushed one step past the existing happy-path scripts — and are being kept unfixed on
purpose, each backed by regression tests that fail by design, to demonstrate that "found but not
yet fixed" doesn't have to mean "undocumented and untracked":

| # | Defect | Found by | Severity/Priority | Automated proof |
|---|---|---|---|---|
| 07 | Stop Mission never returns to Idle — Start permanently disabled after the first Stop | Manual exploratory testing (past `TC-001`) | Major/P2 | `MissionControlViewModelTests` + `KnownDefectsUiTests` (both red) |
| 08 | Emergency Abort banner never clears after a successful reconnect | Manual exploratory testing (past `RegressionUiTests`) | Major/P2 | `WarningsBannerViewModelTests` + `KnownDefectsUiTests` (both red) |

Both share the same root cause: `Stopped -> Idle` and `EmergencyAbort -> Idle` are legal edges
in `UavStateMachine`'s transition tables, but nothing in the App layer ever takes them. A
supporting Core-layer test (`TerminalStates_Stopped_And_EmergencyAbort_CanTransitionBackToIdle`)
passes, proving the domain layer is not at fault. Full detail in `docs/defects/07-*.md` and
`docs/defects/08-*.md`, with manual repro in `docs/test-cases/TC-002-*.md` and `TC-003-*.md`.

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
`https://github.com/vcordev/UAV-Mission-Control` and confirmed green in the Actions tab. One real
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
