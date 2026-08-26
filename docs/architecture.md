# Architecture

## Solution layout

```
UavMissionControl.slnx
src/
  UavMissionControl.Core/          net10.0, no WPF reference — the entire testable domain
    Domain/                       ConnectionState, MissionState, UavStateMachine, TelemetrySnapshot, TelemetryThresholds
    Logging/                      LogSeverity, EventLogEntry, IEventLog, EventLog
    Simulation/                   ITelemetrySimulator, TelemetrySimulator, ScenarioInjector, TelemetryWarningMonitor
  UavMissionControl.App/           net10.0-windows, UseWPF=true
    ViewModels/                    RelayCommand, ViewModelBase, MainViewModel (composition root), one VM per panel
    Views/                         one UserControl per panel + MainWindow
    Services/                      IUiDispatcher, WpfDispatcher, ImmediateDispatcher
tests/
  UavMissionControl.Core.Tests/            xUnit — pure domain logic, no WPF, no process
  UavMissionControl.App.Tests/             xUnit + Moq — ViewModels in isolation
  UavMissionControl.UiAutomation.Tests/    xUnit + FlaUI — drives the real built exe
```

`UavMissionControl.Core` has zero dependency on WPF or on anything in `App`. Every rule that
matters — what states are legal, what counts as a low battery, how telemetry evolves — lives
there and is unit-testable without a UI thread, a Dispatcher, or a running process. `App` is a
thin MVVM layer over it.

## The state machine

`UavStateMachine` (`src/UavMissionControl.Core/Domain/UavStateMachine.cs`) owns two explicit,
independently-declared state machines rather than one combined enum:

- `ConnectionState`: `Disconnected → Connecting → Connected → Disconnected`, plus `Connecting → Disconnected` (a connect attempt abandoned before completing).
- `MissionState`: `Idle → Active ⇄ Paused → Stopped → Idle`, plus `Active`/`Paused → EmergencyAbort → Idle`.

Both are represented as adjacency dictionaries (`ConnectionTransitions`, `MissionTransitions`)
checked by `CanTransitionConnection`/`CanTransitionMission`, so "is this edge legal" is one
dictionary lookup, not a chain of `if`s that drifts out of sync with reality. Illegal transitions
throw `InvalidStateTransitionException` rather than silently no-op-ing — a caller that doesn't
check `CanTransition*` first finds out immediately, in a test, not as a user-visible glitch later.

The two machines are not fully independent: `TransitionConnection` contains one safety rule —
losing connection while `MissionState` is `Active` or `Paused` forces an automatic transition to
`EmergencyAbort`. This models a real constraint (a UAV cannot safely continue an unsupervised
mission without a command link) and is exercised by
`UavStateMachineTests.ConnectionLost_WhileMissionInFlight_ForcesEmergencyAbort`.

## Telemetry pipeline

`TelemetrySimulator` (`src/UavMissionControl.Core/Simulation/TelemetrySimulator.cs`) generates a
bounded random walk for battery, signal, altitude, speed, and GPS position. Two design choices
matter for testability and for the defects this project documents:

- **`Tick()` is public.** Production code drives it via a `System.Threading.Timer` on a real
  background thread (`Start()`), but tests call `Tick()` directly — no wall-clock waiting, and a
  seeded `Random` (`seed:` constructor parameter) makes sequences deterministic and reproducible.
- **The timer is a background `System.Threading.Timer`, not a `DispatcherTimer`, on purpose.**
  A `DispatcherTimer` would auto-marshal its callback onto the UI thread, which would have hidden
  the cross-thread bug documented in `docs/defects/03-eventlog-cross-thread-crash.md`. Telemetry
  genuinely arrives on a background thread here, the way a real hardware link would; anything
  downstream that touches UI-bound state has to marshal explicitly, and that's exactly the seam
  the defect (and its regression test) is about.

`TelemetryWarningMonitor` reacts to `SnapshotUpdated` and logs an event the moment battery/signal
status crosses into something worse — once per crossing, not once per tick — so the event log
fills up with meaningful transitions instead of noise. It runs on whatever thread raised the
event (the background timer thread when the real simulator is running), which is why it's the
trigger for defect #03.

`ScenarioInjector` is a thin, named-preset facade over `TelemetrySimulator.ForceBatteryPercent`/
`ForceSignalStrengthPercent` (`TriggerLowBattery()`, `TriggerCriticalBattery()`, ...), so manual
QA and UI automation tests can hit exact threshold values on demand instead of waiting for the
random walk to happen to cross them.

## MVVM composition

`MainViewModel` (`src/UavMissionControl.App/ViewModels/MainViewModel.cs`) is the composition
root. Its default (parameterless) constructor builds the real `UavStateMachine`, `EventLog`,
`TelemetrySimulator`, and `WpfDispatcher` and wires everything together; an `internal` second
constructor accepts all four as parameters, which is how `MainViewModelTests` substitutes a
`Mock<ITelemetrySimulator>` and `ImmediateDispatcher` without touching the real object graph
(`InternalsVisibleTo` in `UavMissionControl.App.csproj` grants `App.Tests` access to it).

Each panel gets its own ViewModel (`ConnectionPanelViewModel`, `MissionControlViewModel`,
`TelemetryDashboardViewModel`, `EventLogViewModel`, `WarningsBannerViewModel`), all constructed
by `MainViewModel` and bound to their matching `UserControl` via a per-control `DataContext`
binding in `MainWindow.xaml` (e.g. `<views:ConnectionPanelView DataContext="{Binding ConnectionPanel}"/>`).
`RelayCommand` (`ViewModels/RelayCommand.cs`) is hand-rolled rather than pulled from a library —
it exposes `RaiseCanExecuteChanged()` explicitly rather than relying on WPF's
`CommandManager.RequerySuggested`, because that mechanism only re-queries on UI input events, not
on state-machine-driven changes; every ViewModel calls it explicitly after any change that
affects a guard.

### The `IUiDispatcher` seam

`TelemetryDashboardViewModel` and `EventLogViewModel` both receive telemetry/log events that can
originate on a background thread. Rather than reference `Application.Current.Dispatcher` directly
(which would make them impossible to construct in a unit test host with no running `Application`),
both take an `IUiDispatcher`:

- `WpfDispatcher` — captures `Dispatcher.CurrentDispatcher` at construction (must be built on the
  UI thread) and marshals via `Invoke`.
- `ImmediateDispatcher` — runs the action synchronously, inline, on whatever thread called it.
  Used throughout `App.Tests` so ViewModel tests run without any WPF message loop.

This single abstraction is also the reason defect #03 was cheaply, deterministically testable
(`EventLogViewModelTests.EntryAdded_AlwaysRoutesThroughTheDispatcher` uses a `RecordingDispatcher`
test double to assert the mutation always goes through `Invoke`, regardless of which thread raised
the event) instead of needing a flaky, timing-dependent race to reproduce in a unit test.

## Where each requirement lives

| Requirement | Where |
|---|---|
| Connection/disconnection | `ConnectionPanelViewModel`, `UavStateMachine.ConnectionState` |
| Mission start/pause/resume/stop | `MissionControlViewModel`, `UavStateMachine.MissionState` |
| UAV status, battery, signal, GPS, altitude, speed | `TelemetrySnapshot`, `TelemetryDashboardViewModel` |
| Real-time telemetry | `TelemetrySimulator` (background timer) |
| Connection loss/recovery | `UavStateMachine`'s auto-`EmergencyAbort` rule + "Simulate Connection Loss" QA command |
| Low battery / weak signal warnings | `TelemetryThresholds`, `WarningsBannerViewModel`, `TelemetryWarningMonitor` |
| Event/log history | `IEventLog`/`EventLog` (Core), `EventLogViewModel` (WPF projection) |
| Explicit states and valid/invalid transitions | `UavStateMachine` |
