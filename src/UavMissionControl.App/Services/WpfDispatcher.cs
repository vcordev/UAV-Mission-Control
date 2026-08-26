using System.Windows.Threading;

namespace UavMissionControl.App.Services;

/// <summary>Real dispatcher. Must be constructed on the UI thread — it captures that thread's
/// <see cref="Dispatcher"/> via <see cref="Dispatcher.CurrentDispatcher"/>.</summary>
public sealed class WpfDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }
}
