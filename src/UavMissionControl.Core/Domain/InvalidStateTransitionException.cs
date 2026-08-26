namespace UavMissionControl.Core.Domain;

/// <summary>
/// Thrown when code attempts a state transition that the UAV state machine does not
/// consider legal from its current state (a structurally invalid edge, or a guarded
/// edge whose precondition — e.g. "must be connected" — isn't met).
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string message) : base(message)
    {
    }
}
