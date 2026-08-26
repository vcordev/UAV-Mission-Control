# Test Strategy & Test Plan

## 1. Test Strategy

### 1.1 Approach

Three layers, each testing a different kind of risk, each with its own tooling:

```
        ┌─────────────────────────────┐
        │  UI Automation (FlaUI)      │  12 tests — the real, built exe, real UI Automation
        │  slowest, highest-fidelity  │
        ├─────────────────────────────┤
        │  App.Tests (xUnit + Moq)    │  39 tests — ViewModels in isolation
        │  fast, isolates WPF glue    │
        ├─────────────────────────────┤
        │  Core.Tests (xUnit)         │  55 tests — pure domain, no WPF, no process
        │  fastest, most numerous     │
        └─────────────────────────────┘
```

106 tests total. The ratio (55 : 39 : 12, roughly a classic pyramid) is a direct consequence of
architecture: `UavMissionControl.Core` has no WPF dependency, so every state-transition rule,
boundary value, and simulation behavior is testable at the cheapest layer. Only genuinely
UI-specific concerns (does the button visually disable, does the banner actually render) need the
slow, real-process FlaUI layer.

### 1.2 Techniques used, and why each one is here

| Technique | Where | Example |
|---|---|---|
| Functional testing | All three layers | `MissionControlViewModelTests.Start_TransitionsToActive_AndResetsElapsedToZero` |
| Boundary-value analysis | Core.Tests, UiAutomation.Tests | `TelemetryThresholdsTests` (three-point triad at each threshold); `NegativeAndBoundaryUiTests.LowBatteryBanner_ExactlyAtThreshold_Appears` |
| Negative / state-transition testing | Core.Tests, App.Tests | `UavStateMachineTests.TransitionMission_ToStructurallyIllegalState_Throws`; `MissionControlViewModelTests.StopCommand_CanExecute_IsFalse_WhenConnectedButMissionIdle` |
| Regression testing | App.Tests, UiAutomation.Tests | `ConnectionPanelViewModelTests.Reconnecting_LogsTelemetryLinkEstablished_ExactlyOncePerConnectCycle` |
| Risk-based testing | Test design decisions (see 1.3) | Exhaustive transition-matrix coverage on `UavStateMachine`; a dedicated dispatcher-routing test on every background-thread-reachable UI mutation |
| Reliability / soak testing | Core.Tests, UiAutomation.Tests | `SoakTests` (100k ticks); `ReliabilityUiTests.StressCyclingWarningScenarios_WhileConnected_DoesNotCrashTheApp` |
| Performance testing | App.Tests | `EventLogViewModelPerformanceTests` |
| Manual / exploratory testing | `docs/test-cases/` | `TC-001-happy-path-mission-lifecycle.md` — executed against the real running app, screenshot-verified |
| WPF UI automation | UiAutomation.Tests | FlaUI/UIA3 driving the built exe (see `docs/decisions.md` for tool selection) |

### 1.3 Risk-based prioritization

Test effort was not spread uniformly — it was weighted toward where a mistake would be worst or
hardest to notice:

- **`UavStateMachine` gets an exhaustive transition matrix** (every legal edge, representative
  illegal edges, both machines' interaction on connection loss) because it is the one place a bug
  could let the "aircraft" enter a nonsensical or unsafe state — the highest-consequence code in
  the project.
- **Every background-thread-reachable UI mutation gets a dedicated dispatcher-routing test**
  (`EntryAdded_AlwaysRoutesThroughTheDispatcher`, the `CountingDispatcher` test on
  `TelemetryDashboardViewModel`) because this exact class of bug is invisible in a quick manual
  smoke test and only manifests under sustained real-world use — see defect #03.
  Once that risk was identified, the same seam-testing pattern was applied preemptively to the
  elapsed-mission timer (`IsElapsedTimerRunning`) *before* defect #05 was planted, and it caught it
  immediately.
- **Performance testing was applied to the one place with unbounded growth** (the event log,
  which only ever grows for the life of a session) rather than everywhere — a fixed-size
  telemetry snapshot or a bounded state enum has no analogous risk.

### 1.4 What is explicitly out of scope

- **Localization / globalization** — the app is English-only by design; no resource files, no
  culture-specific formatting tests.
- **Accessibility audit** — beyond what WPF/UI Automation provide by default (every interactive
  element has an `AutomationId`, which the UI automation suite already depends on to function), no
  screen-reader or contrast audit was performed.
- **Multi-machine / cross-OS testing** — Windows-only by design (WPF); CI runs on GitHub-hosted
  `windows-latest` only, not a matrix of Windows versions.
- **Load/concurrency testing against multiple users** — this is a single-user desktop app; that
  category of performance testing does not apply.

## 2. Test Plan

### 2.1 Scope

In scope: `UavMissionControl.Core` (domain logic, telemetry simulation, event logging) and
`UavMissionControl.App` (WPF ViewModels, Views, and their integration). Out of scope: anything
outside this repository (this is an independent, original project, not any real company's platform).

### 2.2 Entry / exit criteria

- **Entry** for any change: solution builds with 0 errors.
- **Exit** for any change: `dotnet test UavMissionControl.slnx` reports 0 failures across all
  three test projects, and (for anything touching `App`) a manual or automated UI check confirms
  the app still launches and the changed area behaves as expected.
- **Exit for the project as a whole:** all 106 tests green locally *and* in GitHub Actions CI
  (both jobs) — see `docs/ci-cd.md`.

### 2.3 Test environment / tools

| Concern | Tool |
|---|---|
| Unit/integration test framework | xUnit 2.9.3 |
| Assertions | Shouldly |
| Mocking | Moq |
| WPF UI automation | FlaUI.Core + FlaUI.UIA3 |
| CI | GitHub Actions, `windows-latest` |
| Local dev environment | Windows 11, .NET 10 SDK, VS Code (no Visual Studio) |

Full rationale for each tool choice is in `docs/decisions.md`.

### 2.4 Defect severity × priority matrix

| # | Defect | Severity | Priority | Found via |
|---|---|---|---|---|
| 01 | Low-battery boundary off-by-one | Major | P2 | Boundary-value analysis |
| 02 | Stop-button defense-in-depth gap | Major | P2 | Negative / state-transition testing |
| 03 | Event log cross-thread crash | **Critical** | **P1** | Unit (dispatcher-routing seam) + live reliability repro |
| 04 | Reconnect handler leak (duplicate logs) | Minor | P3 | Regression testing |
| 05 | Pause doesn't stop elapsed timer | Major | P2 | Functional testing (timer-state seam) |
| 06 | Event log O(n) prepend | Minor | P3 | Performance testing |

(Severity = impact if it ships; Priority = how urgently it should be fixed relative to other open
work. #03 is the only Critical/P1: it's the only defect that crashes the running application.)

Full repro/root-cause/fix detail for each is in `docs/defects/`.

### 2.5 Deliverables

- This document, plus `docs/architecture.md`, `docs/decisions.md`, `docs/ci-cd.md`,
  `docs/performance-report.md`.
- `docs/defects/01`–`06-*.md`: one report per defect.
- `docs/test-cases/TC-001-*.md`: manual test case documentation.
- `docs/learning/`, `docs/interview/`: supporting material for the interview this project was
  built to prepare for.
- `docs/final-report.md`: summary tying everything together.
