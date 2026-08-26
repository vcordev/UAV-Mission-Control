# xUnit and this project's testing patterns, explained

## The basics

xUnit is a .NET unit testing framework. A test class is a plain C# class; a test method is any
public method marked `[Fact]` (a single test case) or `[Theory]` (a parameterized test run once
per `[InlineData(...)]` row). xUnit discovers and runs them via `dotnet test`.

```csharp
[Theory]
[InlineData(20, BatteryStatus.Low)]      // exactly on the boundary
[InlineData(19.9, BatteryStatus.Low)]
[InlineData(20.1, BatteryStatus.Normal)]
public void EvaluateBattery_ReturnsExpectedStatus(double batteryPercent, BatteryStatus expected)
{
    TelemetryThresholds.EvaluateBattery(batteryPercent).ShouldBe(expected);
}
```

(from `tests/UavMissionControl.Core.Tests/Domain/TelemetryThresholdsTests.cs`) — one method, three
test cases, each showing up individually in test results. This is how this project implements
boundary-value analysis: one `[Theory]` per threshold, with an above/on/below triad per row.

`.ShouldBe(...)` and `Should.Throw<T>(...)` are Shouldly assertion syntax (see `docs/decisions.md`
for why Shouldly over FluentAssertions) — they read close to plain English and produce a readable
diff on failure.

## Isolating a class from its dependencies: Moq

`MissionControlViewModel` depends on a real `UavStateMachine` and `IEventLog` — both cheap, real
objects, so most tests in this project just construct real instances (see
`MissionControlViewModelTests.Create`). But `TelemetryDashboardViewModel` depends on
`ITelemetrySimulator`, and tests need to push an *exact, crafted* `TelemetrySnapshot` into it
without waiting for a real random-walk simulation to happen to produce one. That's what Moq is
for:

```csharp
var simulator = new Mock<ITelemetrySimulator>();
simulator.SetupGet(s => s.Current).Returns(new TelemetrySnapshot(..., 55, 30, ...));
var vm = new TelemetryDashboardViewModel(simulator.Object, new ImmediateDispatcher());

simulator.Raise(s => s.SnapshotUpdated += null, simulator.Object, updatedSnapshot);

vm.BatteryPercent.ShouldBe(55);
```

(from `TelemetryDashboardViewModelTests.cs`) — `Mock<ITelemetrySimulator>` creates a fake object
implementing the interface; `SetupGet` controls what a property returns; `.Raise(...)` fires an
event on it exactly as if the real simulator had. The ViewModel under test can't tell the
difference between this and a real simulator — which is the point: the test isolates the
ViewModel's *own* logic from the simulator's.

## Test doubles that aren't from a library: `ImmediateDispatcher` and friends

Not every test double needs Moq. `ImmediateDispatcher` (`src/UavMissionControl.App/Services/ImmediateDispatcher.cs`)
is a hand-written, one-line implementation of `IUiDispatcher` that just runs the action inline —
used everywhere in `App.Tests` so ViewModels can be constructed and tested without a real WPF
`Application`/`Dispatcher` running.

A more targeted example: `EventLogViewModelTests` has a private nested `RecordingDispatcher` that
counts how many times `Invoke` was called:

```csharp
private sealed class RecordingDispatcher : IUiDispatcher
{
    public int InvokeCount { get; private set; }
    public void Invoke(Action action) { InvokeCount++; action(); }
}
```

This tests something Moq isn't the natural fit for: not "what value came back," but "was this
specific safety mechanism actually used." See the next section for why that distinction mattered.

## "Test the seam, not the symptom"

The single most-repeated testing pattern in this project. The *symptom* of defect #03 was an
intermittent crash — reproducing a real race condition reliably, in a fast unit test, without a
live WPF window, isn't really possible (see `docs/defects/03-eventlog-cross-thread-crash.md` for
why). But the *seam* that guarantees safety — "does every mutation route through
`IUiDispatcher.Invoke`?" — is directly, deterministically testable with the `RecordingDispatcher`
above: call `eventLog.Add(...)`, assert `dispatcher.InvokeCount == 1`. No threads, no timing, no
flakiness, and it fails immediately if the dispatcher wrapper is ever removed.

The same pattern reappears for defect #05: instead of waiting for a real `DispatcherTimer` to
tick (which wouldn't even fire without a running message loop in a test host),
`MissionControlViewModel` exposes an `internal bool IsElapsedTimerRunning => _elapsedTimer.IsEnabled;`
test seam, and `Pause_StopsTheElapsedTimer` asserts on that flag directly.

## Making a test project see another project's `internal` members

`MainViewModelTests` needs `MainViewModel`'s `internal` constructor (the one that accepts fakes)
to inject `Moq` objects. That requires:

```xml
<!-- src/UavMissionControl.App/UavMissionControl.App.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="UavMissionControl.App.Tests" />
</ItemGroup>
```

This grants exactly one named assembly access to `internal` members — not a general relaxation of
encapsulation, just a deliberate, narrow seam for tests.
