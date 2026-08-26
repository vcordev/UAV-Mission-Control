# Defect #06 — Event log prepend degrades under sustained load

**Severity:** Minor (no incorrect behavior at normal scale; a real cost only under sustained load)
**Priority:** P3
**Status:** Fixed
**Found by:** Performance testing
**Component:** `UavMissionControl.App.ViewModels.EventLogViewModel`

## Summary

Unlike defects #01–#05, this was never a working implementation that regressed — it's a
performance characteristic that was present in the original Phase 3 design and only surfaced
once Phase 7 built the tooling to measure it. `EventLogViewModel` displayed newest-entry-first by
calling `Entries.Insert(0, entry)` on every new log entry. `ObservableCollection<T>` is backed by
a `List<T>`; inserting at index 0 shifts every existing element, making each insert
**O(n)** in the current entry count rather than O(1).

## Measurement

`EventLogViewModelPerformanceTests.AddingEntries_CostDoesNotDegradeBadly_AsTheLogGrows` times a
5,000-entry batch early (log size ~5k) against an identical batch after the log has grown to
~50k entries, and compares the two — a relative comparison on the same run/machine, not a fixed
millisecond budget (which would be flaky across hardware):

| Log size at time of batch | Cost of 5,000 adds | 
|---|---|
| ~5,000 | 3 ms |
| ~55,000 | 60 ms (**20x** for an 11x size increase) |

The near-linear-to-superlinear cost growth confirms the O(n)-per-insert behavior. 50,000+ entries
is a realistic number over a long-running mission session (telemetry ticks every 500ms; a warning
crossing threshold repeatedly over hours easily reaches this scale), so this is a genuine, not
hypothetical, concern.

## Fix

Switched to `Entries.Add(entry)` (O(1) amortized, standard `List<T>` append) for both the
constructor's hydration loop and the `EntryAdded` handler. This is a genuine **UX trade-off**, not
a free fix: the log now reads oldest-first instead of newest-first.

An attempt to keep newest-first while fixing the performance (auto-scrolling the `ListView` to
the bottom on every add, so new entries still land in the visible area) was tried and reverted —
see "What didn't work" below. Given the actual requirement is "don't silently degrade under load,"
oldest-first-with-no-scroll was accepted as the simpler, honestly-documented outcome; auto-scroll
is left as a known limitation / future enhancement rather than adding more moving parts to chase it.

## What didn't work (and why it's documented, not hidden)

The first attempt kept newest-first display by having `EventLogView`'s code-behind subscribe to
`Entries.CollectionChanged` and call `ListView.ScrollIntoView(...)` on every add. This **crashed
the app**: calling `ScrollIntoView` synchronously from inside the same `CollectionChanged`
notification that the `ListView`'s own `ItemContainerGenerator` was still processing is a known
WPF reentrancy hazard, not a hypothetical one — it reproduced immediately once the UI automation
suite exercised it (9 of 11 UI tests failed with "Element not found" because the app window had
disappeared). Deferring the scroll via `Dispatcher.BeginInvoke(..., DispatcherPriority.Background)`
stopped the crash, but live verification (screenshot after a stress cycle) showed the scroll
still wasn't reliably reaching the bottom. Rather than keep layering complexity onto a cosmetic
feature, it was removed. This progression — try the "obviously correct" fix, discover it's
actually fragile, back off to the simpler correct-but-less-polished option — is itself worth
recording for the interview: the *decision to stop* was intentional, not a sign the feature is
half-implemented.

## Regression coverage

`EventLogViewModelPerformanceTests.AddingEntries_CostDoesNotDegradeBadly_AsTheLogGrows` (App.Tests)
fails if a future change reintroduces O(n)-or-worse per-add cost. `EventLogViewModelTests.EntryAdded_AppendsOldestFirst`
locks in the new ordering.
