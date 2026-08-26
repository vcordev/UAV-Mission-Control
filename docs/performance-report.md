# Performance & Reliability Report

Scope, deliberately: lightweight, proportionate checks appropriate to a single-machine desktop
app — not a load-testing framework. See `docs/decisions.md` for why BenchmarkDotNet/NBomber were
not used.

## 1. Basic performance testing — event log insert cost

**What was measured:** the cost of adding entries to the event log's `ObservableCollection`, at
two different collection sizes, on the same run/machine (a relative comparison, not an absolute
time budget — absolute millisecond assertions are flaky across machines; relative growth is not).

**Test:** `EventLogViewModelPerformanceTests.AddingEntries_CostDoesNotDegradeBadly_AsTheLogGrows`
(`tests/UavMissionControl.App.Tests/Performance/`).

| Log size at time of batch | Cost of adding 5,000 entries |
|---|---|
| Before fix, ~5,000 | 3 ms |
| Before fix, ~55,000 | **60 ms** (20x cost for 11x size) |
| After fix, ~5,000 | 1 ms |
| After fix, ~55,000 | **0 ms** |

**Finding:** `EventLogViewModel` used `ObservableCollection.Insert(0, entry)` for newest-first
display, which is O(n) per call (backing `List<T>` shifts every element). Fixed by switching to
`Add(entry)` (O(1) amortized) and changing the display to oldest-first. Full writeup:
`docs/defects/06-eventlog-insert-performance.md`.

## 2. Reliability testing — soak tests

**What was measured:** whether the telemetry/logging pipeline survives far more ticks than any
realistic single session (100,000 ticks; at the real 500ms interval that's ~14 hours) without
throwing, without telemetry values drifting outside their documented bounds, and without gross
memory growth.

**Tests:** `SoakTests.TelemetryPipeline_Survives100kTicks_WithoutExceptions_AndStaysInBounds`,
`SoakTests.EventLog_Survives50kEntries_WithBoundedMemoryGrowth` (`tests/UavMissionControl.Core.Tests/Reliability/`).

**Result:** both pass. Battery/signal/altitude/speed stayed within their documented ranges across
100k ticks; 50,000 log entries did not approach a three-digit-megabyte memory footprint (a coarse
`GC.GetTotalMemory` before/after comparison — a sanity bound against gross accidental duplication
or retention, not a precision leak detector; see `docs/decisions.md`).

## 3. System-level reliability — UI stress test

**What was measured:** whether the *running app* — not just the Core pipeline in isolation —
survives sustained, rapid warning-threshold crossings while connected, since that is exactly the
background-thread code path defect #03 broke.

**Test:** `ReliabilityUiTests.StressCyclingWarningScenarios_WhileConnected_DoesNotCrashTheApp`
(`tests/UavMissionControl.UiAutomation.Tests/`) — connects, then alternates the Critical Battery /
Clear scenario buttons 30 times (~18 seconds of sustained background-thread telemetry activity),
then confirms the app is still alive and responsive.

**History:** this exact stress pattern is what was used, ad hoc, to *reproduce* defect #03 live
during Phase 5 (the app crashed with `System.NotSupportedException` within a few seconds) and
then to confirm the fix held. It's now a permanent regression test rather than a one-off manual
check, and reruns clean after the fix — 12 UI automation tests total, all green, stable across
multiple consecutive runs (see `docs/test-strategy.md` for the full test inventory).

## 4. What this report does not claim

- No throughput/load testing against concurrent users — this is a single-user desktop app; that
  category of performance testing does not apply here.
- The soak tests run tens of thousands of ticks in a tight loop for speed, not in real time at the
  real 500ms interval — this tests the same code path under sustained call volume, not literal
  multi-hour wall-clock survival (which would be impractical to run in CI).
- `GC.GetTotalMemory` is a coarse signal. A real memory-leak investigation would use a profiler
  (dotMemory, PerfView) — out of scope for this project's size.
