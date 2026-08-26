namespace UavMissionControl.App.Services;

/// <summary>
/// Marshals an action onto the UI thread. Abstracted so ViewModels can be constructed and
/// tested without a running WPF <c>Application</c> — tests supply <see cref="ImmediateDispatcher"/>.
/// </summary>
public interface IUiDispatcher
{
    void Invoke(Action action);
}
