# Defect #01 — Low-battery warning misses the exact threshold value

**Severity:** Major (a real safety warning silently fails to fire)
**Priority:** P2
**Status:** Fixed
**Found by:** Boundary-value analysis (unit test), Core.Tests
**Component:** `UavMissionControl.Core.Domain.TelemetryThresholds`

## Summary

`TelemetryThresholds.EvaluateBattery` used `batteryPercent < LowBatteryPercent` instead of
`<= LowBatteryPercent`. At exactly 20% battery — the documented threshold — the UAV is
reported as `BatteryStatus.Normal` instead of `BatteryStatus.Low`, so the low-battery banner
does not appear until the battery drops to 19.9%.

## Equivalence partitions and boundary values

| Battery % | Correct status | Partition |
|---|---|---|
| 100 – 20.1 | Normal | above low threshold |
| **20** | **Low** | **exact low threshold — the value this defect got wrong** |
| 19.9 – 10.1 | Low | below low, above critical |
| **10** | **Critical** | exact critical threshold |
| 9.9 – 0 | Critical | below critical threshold |

This is the classic boundary-value-analysis triad (just above / exactly on / just below) applied
to both thresholds; the critical-battery boundary (`<=10`) was implemented correctly from the
start and was never affected.

## Repro

1. Call `TelemetryThresholds.EvaluateBattery(20)`.
2. Expected: `BatteryStatus.Low`.
3. Actual (defect present): `BatteryStatus.Normal`.

Equivalently, via the app: click **Critical Battery** then **Clear** repeatedly while battery
telemetry is draining, and watch it pass through exactly 20% — the orange "LOW BATTERY" banner
does not appear at that instant.

## Root cause

Off-by-one in the comparison operator (`<` vs `<=`) when translating "at or below 20% is low"
into code — an easy mistake because both readings ("low starts at 20%" vs "low starts below
20%") sound similar in conversation but produce different behavior at exactly the boundary.

## Detection

Two existing tests failed immediately when the defect was introduced, with no other test
affected — a clean, isolated signal:

- `TelemetryThresholdsTests.EvaluateBattery_ReturnsExpectedStatus(batteryPercent: 20, expected: Low)`
- `ScenarioInjectorTests.TriggerLowBattery_ResultsInLowBatteryStatus` (which forces the battery to
  exactly `TelemetryThresholds.LowBatteryPercent`, i.e. 20, for manual/exploratory QA use — so it
  exercises the same boundary from a different angle)

## Fix

Restored `<=` in `EvaluateBattery`. No other code changed.

## Regression coverage

Both tests above already existed *before* this defect was introduced (written during the Phase 2
bug-free baseline) and now pass again — no new test was needed, which is itself a demonstration
of why boundary values belong in the test suite from day one rather than added reactively.
