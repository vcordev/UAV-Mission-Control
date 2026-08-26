using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.App.ViewModels;

public sealed class ConnectionPanelViewModel : ViewModelBase
{
    private readonly UavStateMachine _stateMachine;
    private readonly IEventLog _eventLog;
    private readonly TimeSpan _connectDelay;

    public ConnectionPanelViewModel(UavStateMachine stateMachine, IEventLog eventLog, TimeSpan? connectDelay = null)
    {
        _stateMachine = stateMachine;
        _eventLog = eventLog;
        _connectDelay = connectDelay ?? TimeSpan.FromMilliseconds(1200);

        _stateMachine.ConnectionStateChanged += (_, _) => OnConnectionStateChanged();

        ConnectCommand = new RelayCommand(
            async () => await ConnectAsync(),
            () => _stateMachine.ConnectionState == ConnectionState.Disconnected);

        DisconnectCommand = new RelayCommand(
            Disconnect,
            () => _stateMachine.ConnectionState != ConnectionState.Disconnected);
    }

    public ConnectionState ConnectionState => _stateMachine.ConnectionState;

    public string StatusText => ConnectionState switch
    {
        ConnectionState.Disconnected => "Disconnected",
        ConnectionState.Connecting => "Connecting...",
        ConnectionState.Connected => "Connected",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public RelayCommand ConnectCommand { get; }

    public RelayCommand DisconnectCommand { get; }

    /// <summary>Public (not just wrapped in <see cref="ConnectCommand"/>) so tests can await
    /// the connect flow deterministically instead of racing an async-void command execution.</summary>
    public async Task ConnectAsync()
    {
        _stateMachine.TransitionConnection(ConnectionState.Connecting);
        _eventLog.Add(LogSeverity.Info, "Connecting to UAV...");

        await Task.Delay(_connectDelay);

        _stateMachine.TransitionConnection(ConnectionState.Connected);
        _eventLog.Add(LogSeverity.Info, "Connected to UAV.");
    }

    private void Disconnect()
    {
        _stateMachine.TransitionConnection(ConnectionState.Disconnected);
        _eventLog.Add(LogSeverity.Warning, "Disconnected from UAV.");
    }

    private void OnConnectionStateChanged()
    {
        OnPropertyChanged(nameof(ConnectionState));
        OnPropertyChanged(nameof(StatusText));
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
    }
}
