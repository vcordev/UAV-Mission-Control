# Test Strategy & Test Plan

## 1. Test Strategy

### 1.1 Approach

Three layers, each testing a different kind of risk, each with its own tooling:

```
        ┌─────────────────────────────┐
        │  UI Automation (FlaUI)      │  14 tests (12 green + 2 known-defect) — real UI Automation
        │  slowest, highest-fidelity  │
        ├─────────────────────────────┤
        │  App.Tests (xUnit + Moq)    │  41 tests (39 green + 2 known-defect) — ViewModels in isolation
        │  fast, isolates WPF glue    │
        ├─────────────────────────────┤
        │  Core.Tests (xUnit)         │  56 tests (all green) — pure domain, no WPF, no process
        │  fastest, most numerous     │
        └─────────────────────────────┘
```

111 tests total: 107 green, plus 4 tagged `[Trait("Category","KnownDefect")]` that are
intentionally red (see 1.4a below — these are permanent regression proof for two documented,
deliberately-unresolved defects, not accidental failures). The green ratio (56 : 39 : 12,
roughly a classic pyramid) is a direct consequence of architecture: `UavMissionControl.Core` has
no WPF dependency, so every state-transition rule, boundary value, and simulation behavior is
testable at the cheapest layer. Only genuinely UI-specific concerns (does the button visually
disable, does the banner actually render) need the slow, real-process FlaUI layer.

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

### 1.4a Known, deliberately unresolved defects, and how they stay visible without breaking CI

Two defects (`docs/defects/07-stop-mission-no-idle-transition.md` and
`docs/defects/08-emergency-abort-banner-persists-after-reconnect.md`) were found by manual
exploratory testing and are being kept **unfixed on purpose**, specifically to demonstrate that
"found but not yet fixed" doesn't have to mean "undocumented, untracked, and un-detectable."

Each has a regression test at the App layer and at the UI-automation layer (4 tests total) that
asserts the **correct** behavior and therefore currently **fails** — a real, executed failure,
not a `[Fact(Skip = "...")]` placeholder that silently stops running. All four are tagged:

```csharp
[Fact]
[Trait("Category", "KnownDefect")]
public void SomeTest_DEFECT0X() { ... }
```

This makes both things possible at once, on purpose:

- `dotnet test UavMissionControl.slnx --filter "Category!=KnownDefect"` — the clean, all-green
  107-test run, used as the CI gate (see `.github/workflows/ci.yml`, both test steps carry this
  same filter) and as the "does the shippable behavior still work" check during normal
  development.
- `dotnet test UavMissionControl.slnx --filter "Category=KnownDefect"` — runs exactly the 4
  tests that prove the 2 known defects are still present and still automatically detectable.

A supporting Core-layer test
(`UavStateMachineTests.TerminalStates_Stopped_And_EmergencyAbort_CanTransitionBackToIdle`) is
**not** tagged `KnownDefect` and always passes — it exists to show the domain layer already
supports the correct recovery transitions; the gap is entirely in the App layer never taking
them.

This pattern was chosen over `[Fact(Skip = "...")]` deliberately: a skipped test proves nothing
each run (it doesn't execute), whereas a red, filtered-out test is still compiled, still
collected, and still fails for the documented reason every single time someone runs it —
closer to how a real team would track a triaged-but-not-yet-scheduled bug with a
"known failing, do not fix yet" regression test than to silencing it.

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
- **Exit** for any change: `dotnet test UavMissionControl.slnx --filter "Category!=KnownDefect"`
  reports 0 failures across all three test projects, and (for anything touching `App`) a manual
  or automated UI check confirms the app still launches and the changed area behaves as
  expected. (The 4 `KnownDefect`-tagged tests are expected to keep failing — see 1.4a.)
- **Exit for the project as a whole:** all 107 non-`KnownDefect` tests green locally *and* in
  GitHub Actions CI (both jobs) — see `docs/ci-cd.md` — plus the 4 `KnownDefect` tests still
  failing for their documented reason (a `KnownDefect` test passing unexpectedly means either
  the defect was accidentally fixed, or the test itself regressed — either way, investigate).

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
| 07 | Stop Mission never returns to Idle | Major | P2 | Manual exploratory testing |
| 08 | Emergency Abort banner never clears after reconnect | Major | P2 | Manual exploratory testing |

(Severity = impact if it ships; Priority = how urgently it should be fixed relative to other open
work. #03 is the only Critical/P1: it's the only defect that crashes the running application.
#07 and #08 are the only two **Status: deliberately unresolved** — see 1.4a — everything else in
this table was found, fixed, and has regression coverage that passes.)

Full repro/root-cause/fix detail for each is in `docs/defects/`.

### 2.5 Deliverables

- This document, plus `docs/architecture.md`, `docs/decisions.md`, `docs/ci-cd.md`,
  `docs/performance-report.md`.
- `docs/defects/01`–`06-*.md`: one report per defect.
- `docs/test-cases/TC-001-*.md`: manual test case documentation.
- `docs/learning/`, `docs/interview/`: supporting material for the interview this project was
  built to prepare for.
- `docs/final-report.md`: summary tying everything together.
