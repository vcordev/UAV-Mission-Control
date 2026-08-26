namespace UavMissionControl.Core.Domain;

/// <summary>
/// Governs the two explicit state machines of the UAV — connection lifecycle and mission
/// lifecycle — and the one safety rule that couples them: losing connection while a mission
/// is active or paused forces an emergency abort, because a UAV cannot safely continue an
/// unsupervised mission without a command link.
/// </summary>
public sealed class UavStateMachine
{
    private static readonly Dictionary<ConnectionState, ConnectionState[]> ConnectionTransitions = new()
    {
        [ConnectionState.Disconnected] = [ConnectionState.Connecting],
        [ConnectionState.Connecting] = [ConnectionState.Connected, ConnectionState.Disconnected],
        [ConnectionState.Connected] = [ConnectionState.Disconnected],
    };

    private static readonly Dictionary<MissionState, MissionState[]> MissionTransitions = new()
    {
        [MissionState.Idle] = [MissionState.Active],
        [MissionState.Active] = [MissionState.Paused, MissionState.Stopped, MissionState.EmergencyAbort],
        [MissionState.Paused] = [MissionState.Active, MissionState.Stopped, MissionState.EmergencyAbort],
        [MissionState.Stopped] = [MissionState.Idle],
        [MissionState.EmergencyAbort] = [MissionState.Idle],
    };

    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

    public MissionState MissionState { get; private set; } = MissionState.Idle;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public event EventHandler<MissionState>? MissionStateChanged;

    public bool CanTransitionConnection(ConnectionState to) =>
        ConnectionTransitions[ConnectionState].Contains(to);

    public bool CanTransitionMission(MissionState to)
    {
        if (to == MissionState.Active && ConnectionState != ConnectionState.Connected)
        {
            return false;
        }

        return MissionTransitions[MissionState].Contains(to);
    }

    public void TransitionConnection(ConnectionState to)
    {
        if (!CanTransitionConnection(to))
        {
            throw new InvalidStateTransitionException(
                $"Cannot transition ConnectionState from '{ConnectionState}' to '{to}'.");
        }

        var previous = ConnectionState;
        ConnectionState = to;
        ConnectionStateChanged?.Invoke(this, to);

        var missionWasInFlight = MissionState is MissionState.Active or MissionState.Paused;
        if (previous == ConnectionState.Connected && to == ConnectionState.Disconnected && missionWasInFlight)
        {
            TransitionMission(MissionState.EmergencyAbort);
        }
    }

    public void TransitionMission(MissionState to)
    {
        if (to == MissionState.Active && ConnectionState != ConnectionState.Connected)
        {
            throw new InvalidStateTransitionException(
                $"Cannot transition MissionState to '{to}': UAV is not connected.");
        }

        if (!MissionTransitions[MissionState].Contains(to))
        {
            throw new InvalidStateTransitionException(
                $"Cannot transition MissionState from '{MissionState}' to '{to}'.");
        }

        MissionState = to;
        MissionStateChanged?.Invoke(this, to);
    }
}
