namespace UavMissionControl.App.Services;

/// <summary>Test double: runs the action synchronously, inline, on whatever thread called it.</summary>
public sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Invoke(Action action) => action();
}
