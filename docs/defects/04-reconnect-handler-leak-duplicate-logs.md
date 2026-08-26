# Defect #04 — Reconnecting duplicates "Telemetry link established" log entries

**Severity:** Minor (cosmetic log spam, not a functional or safety failure)
**Priority:** P3
**Status:** Fixed
**Found by:** Regression testing
**Component:** `UavMissionControl.App.ViewModels.ConnectionPanelViewModel`

## Summary

`ConnectAsync` subscribes a local one-shot handler to `UavStateMachine.ConnectionStateChanged`
each time it runs, intending to log "Telemetry link established." exactly once per connect and
then unsubscribe:

```csharp
void OnConnected(object? sender, ConnectionState state)
{
    if (state != ConnectionState.Connected) return;
    _eventLog.Add(LogSeverity.Info, "Telemetry link established.");
    _stateMachine.ConnectionStateChanged -= OnConnected;   // <- this line was removed
}
_stateMachine.ConnectionStateChanged += OnConnected;
```

With the unsubscribe removed, the handler from every past connect cycle is still attached.
Each reconnect adds one more, and — because the state machine's event is multicast — the very
next `Connected` transition fires *all* of them.

## Why it compounds, not just duplicates

This defect does not simply double every message; it produces exactly `N` copies of the message
on the `N`th connect of a session:

| Connect # | Handlers attached going in | Handlers that fire on this Connected transition | Log lines produced |
|---|---|---|---|
| 1 | 0 | 1 (itself) | 1 |
| 2 (reconnect) | 1 (leaked from #1) | 2 | 2 |
| 3 (reconnect) | 2 (leaked from #1, #2) | 3 | 3 |

A naive regression test that connects once, disconnects, reconnects once, and checks only "is
the message present" would pass even with this defect — it only becomes observable once you
count occurrences across at least two connect cycles. `ConnectionPanelViewModelTests` does this
by construction (`Reconnecting_LogsTelemetryLinkEstablished_ExactlyOncePerConnectCycle` connects
twice and asserts an exact count of 2), which is why it catches this at the very first reconnect
rather than needing a third cycle to notice anything looks wrong.

## Repro

1. Connect, then disconnect, then connect again.
2. Expected: exactly one "Telemetry link established." entry per connect (two total).
3. Actual (defect present): three entries after the second connect (one from the first cycle's
   still-attached handler, two freshly fired by the second cycle's `Connected` transition — one
   from the leaked first handler, one from the new second handler).

## Root cause

The one-shot unsubscribe pattern (subscribe → act once → unsubscribe) is correct but fragile:
removing or forgetting the unsubscribe line leaves the subscription permanently in place with no
compiler warning and no immediate symptom — the bug is silent on the very first connect and only
visible from the second reconnect onward, which is exactly the kind of defect that survives a
quick manual smoke test ("I clicked Connect once, it logged once, looks fine").

## Fix

Restored the `_stateMachine.ConnectionStateChanged -= OnConnected;` line.

## Regression coverage

`ConnectionPanelViewModelTests.Reconnecting_LogsTelemetryLinkEstablished_ExactlyOncePerConnectCycle`
(added alongside the feature, before this defect was planted) catches this immediately and
deterministically — no timing or threading involved, just an exact count assertion across two
connect cycles.
