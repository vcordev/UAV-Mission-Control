# Defect #03 — Event log crashes the app when updated from a background thread

**Severity:** Critical (unhandled exception, process terminates)
**Priority:** P1
**Status:** Fixed
**Found by:** Unit test (dispatcher-routing seam) + live reliability/soak-style repro
**Component:** `UavMissionControl.App.ViewModels.EventLogViewModel`

## Summary

`EventLogViewModel` subscribed to `IEventLog.EntryAdded` and mutated its bound
`ObservableCollection<EventLogEntry>` directly, without marshaling onto the UI thread:

```csharp
eventLog.EntryAdded += (_, entry) => Entries.Insert(0, entry); // missing _dispatcher.Invoke
```

`EntryAdded` is not guaranteed to fire on the UI thread. `TelemetryWarningMonitor` reacts to
`ITelemetrySimulator.SnapshotUpdated`, which the real `TelemetrySimulator` raises from inside
`Tick()` — running on a `System.Threading.Timer` callback, i.e. a ThreadPool thread, by design
(see `TelemetrySimulator`'s XML doc: intentionally not `DispatcherTimer`, so telemetry keeps
ticking regardless of UI thread activity). The moment a warning-triggering event log entry gets
added while the app is connected, `EventLogViewModel` tries to mutate the `ListView`-bound
collection from that background thread.

## Repro

1. Launch the app, click **Connect**.
2. Rapidly alternate **Critical Battery** / **Clear** (or just wait for telemetry to drift
   through a threshold naturally).
3. Actual: the app crashes with an unhandled exception within a few seconds.

Reproduced live in this session — full stack trace:

```
Unhandled exception. System.NotSupportedException: This type of CollectionView does not
support changes to its SourceCollection from a thread different from the Dispatcher thread.
   at System.Windows.Data.CollectionView.OnCollectionChanged(...)
   at System.Collections.ObjectModel.ObservableCollection`1.OnCollectionChanged(...)
   at UavMissionControl.App.ViewModels.EventLogViewModel.<.ctor>b__1_0(Object _, EventLogEntry entry)
   at UavMissionControl.Core.Logging.EventLog.Add(LogSeverity severity, String message)
   at UavMissionControl.Core.Simulation.TelemetryWarningMonitor.Evaluate(TelemetrySnapshot snapshot)
   at UavMissionControl.Core.Simulation.TelemetryWarningMonitor.<.ctor>b__3_0(...)
   at UavMissionControl.Core.Simulation.TelemetrySimulator.Tick()
   at UavMissionControl.Core.Simulation.TelemetrySimulator.<Start>b__20_0(Object _)
   at System.Threading.TimerQueueTimer.Fire(Boolean isThreadPool)
   at System.Threading.ThreadPoolWorkQueue.Dispatch()
```

The stack trace confirms the exact path predicted by design: background timer → `Tick()` →
`TelemetryWarningMonitor.Evaluate` → `EventLog.Add` → the un-marshaled `EntryAdded` handler →
`ObservableCollection` → WPF's `CollectionView`, which does enforce single-thread access and
throws.

## Root cause

`TelemetryDashboardViewModel` (added in Phase 3) got this right from the start — it wraps its
`SnapshotUpdated` handler in `_dispatcher.Invoke(...)`. `EventLogViewModel` was written at the
same time but the wrapper was omitted; nothing failed until `TelemetryWarningMonitor` (added
later) gave `EntryAdded` a genuine background-thread caller. This is a common way this class of
bug hides: the code path is only exercised by a real background caller once a *later*, unrelated
feature is added, so the gap survives an initial review untouched.

## Why a unit test could catch this without reproducing the race

A raw `ObservableCollection<T>` has no thread-affinity check of its own — only WPF's
`CollectionView`, once actually bound to a live UI element, enforces it. That makes the crash
itself non-reproducible in a headless unit-test host (no `CollectionView` is ever attached). The
correct unit-testing strategy is therefore to test the *seam* that guarantees safety, not the
*symptom*: assert that every mutation is routed through `IUiDispatcher.Invoke`, regardless of
which thread raised the event. `EventLogViewModelTests.EntryAdded_AlwaysRoutesThroughTheDispatcher`
does exactly this with a `RecordingDispatcher` test double, and fails the instant the wrapper is
removed — see `docs/test-strategy.md` for why this pattern is used instead of trying to force a
timing-dependent race in-process.

The live crash above is the system-level confirmation that the seam actually matters; the Phase 7
soak test (`docs/performance-report.md`) exercises the same path under sustained load as
additional, system-level regression coverage.

## Fix

Restored the dispatcher wrapper:

```csharp
eventLog.EntryAdded += (_, entry) => _dispatcher.Invoke(() => Entries.Insert(0, entry));
```

## Regression coverage

- `EventLogViewModelTests.EntryAdded_AlwaysRoutesThroughTheDispatcher` (unit, fast, deterministic — added proactively before this defect was planted).
- Phase 7 soak test exercises the real background-thread path against the live app as a second, system-level safety net.
