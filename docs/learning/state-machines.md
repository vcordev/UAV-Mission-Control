# State machines, explained from this project

## Why bother with an explicit state machine at all

The naive way to track "is the UAV connected" is a couple of booleans (`isConnected`,
`isConnecting`) scattered across a ViewModel. The problem: nothing stops you from accidentally
setting both `isConnected = true` and `isConnecting = true` at once, or forgetting to reset one
of them somewhere — the "current state" is implicit, spread across multiple variables that can
drift out of sync.

An explicit state machine makes the state a *single value* from a *closed set of possibilities*
(an `enum`), and makes every transition between values go through one controlled place that can
say yes or no. See `src/UavMissionControl.Core/Domain/UavStateMachine.cs`.

## The two enums

```csharp
public enum ConnectionState { Disconnected, Connecting, Connected }
public enum MissionState { Idle, Active, Paused, Stopped, EmergencyAbort }
```

At any moment, `UavStateMachine.ConnectionState` is exactly one of those three values — never
"kind of connected," never two values at once. Same for `MissionState`.

## The transition table

Not every state can go directly to every other state. `UavStateMachine` encodes exactly which
transitions are legal as a dictionary of arrays:

```csharp
private static readonly Dictionary<MissionState, MissionState[]> MissionTransitions = new()
{
    [MissionState.Idle] = [MissionState.Active],
    [MissionState.Active] = [MissionState.Paused, MissionState.Stopped, MissionState.EmergencyAbort],
    ...
};
```

Reading this: from `Idle`, the *only* legal next state is `Active` — you can't jump straight to
`Paused` or `Stopped` without ever starting a mission. `CanTransitionMission(to)` is just a
lookup into this table. `TransitionMission(to)` checks the same table and throws
`InvalidStateTransitionException` if the caller tries an illegal edge, rather than silently
ignoring the request or (worse) doing it anyway.

This is directly testable as data: `UavStateMachineTests` has a `[Theory]` with one row per edge
that should be legal and one row per edge that shouldn't
(`CanTransitionConnection_MatchesExpectedGraph`), so the whole graph is verified, not just a
couple of examples.

## Guards vs. structural legality

Some rules aren't just "is this edge in the table" — they depend on *other* state too. Starting a
mission (`Idle → Active`) is structurally legal, but only makes sense if the UAV is actually
connected:

```csharp
public bool CanTransitionMission(MissionState to)
{
    if (to == MissionState.Active && ConnectionState != ConnectionState.Connected)
        return false;
    return MissionTransitions[MissionState].Contains(to);
}
```

This is a **guard** — an extra condition layered on top of the structural table. The project's
defect #02 (`docs/defects/02-stop-button-defense-in-depth-gap.md`) is exactly a bug in a guard: the
UI's copy of "should Stop be clickable" checked the wrong condition, even though the state
machine's own guard was correct the whole time. That's why the fix involves two layers of tests —
one proving the state machine defends itself regardless of what the UI does, another proving the
UI *also* should have prevented the user from trying.

## Side effects of a transition: the connection-loss safety rule

A state machine can do more than just "is this legal" — `TransitionConnection` also encodes a
safety rule as a side effect:

```csharp
var missionWasInFlight = MissionState is MissionState.Active or MissionState.Paused;
if (previous == ConnectionState.Connected && to == ConnectionState.Disconnected && missionWasInFlight)
{
    TransitionMission(MissionState.EmergencyAbort);
}
```

Losing connection while a mission is running automatically forces `EmergencyAbort` — modeling the
real constraint that a UAV shouldn't continue an unsupervised mission without a command link. This
lives in the state machine itself (not the ViewModel) specifically so it can't be bypassed by any
future UI code path that changes `ConnectionState` — see `UavStateMachineTests.ConnectionLost_WhileMissionInFlight_ForcesEmergencyAbort`.

## Why throw instead of silently ignoring illegal transitions

`InvalidStateTransitionException` is deliberately loud. A silent "do nothing" on an illegal
transition attempt would hide bugs — a caller who thinks they successfully started a mission but
actually didn't (because, say, the UAV wasn't connected) would have no way to know, and would
debug the *symptom* (mission never starts) rather than the *cause* (an illegal transition was
attempted and silently dropped). Throwing surfaces the bug at the exact place and moment it
happens, which is what caught defect #02 in testing before it ever reached a real user.
