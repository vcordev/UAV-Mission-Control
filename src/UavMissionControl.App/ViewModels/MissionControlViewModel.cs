using System.Windows.Threading;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;

namespace UavMissionControl.App.ViewModels;

public sealed class MissionControlViewModel : ViewModelBase
{
    private readonly UavStateMachine _stateMachine;
    private readonly IEventLog _eventLog;
    private readonly DispatcherTimer _elapsedTimer;
    private TimeSpan _elapsed = TimeSpan.Zero;

    public MissionControlViewModel(UavStateMachine stateMachine, IEventLog eventLog)
    {
        _stateMachine = stateMachine;
        _eventLog = eventLog;

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => Tick();

        _stateMachine.MissionStateChanged += (_, _) => OnMissionStateChanged();
        _stateMachine.ConnectionStateChanged += (_, _) => RaiseAllCanExecuteChanged();

        StartCommand = new RelayCommand(
            Start,
            () => _stateMachine.MissionState == MissionState.Idle
                  && _stateMachine.ConnectionState == ConnectionState.Connected);

        PauseCommand = new RelayCommand(Pause, () => _stateMachine.MissionState == MissionState.Active);

        ResumeCommand = new RelayCommand(Resume, () => _stateMachine.MissionState == MissionState.Paused);

        StopCommand = new RelayCommand(
            Stop,
            () => _stateMachine.MissionState is MissionState.Active or MissionState.Paused);
    }

    public MissionState MissionState => _stateMachine.MissionState;

    public TimeSpan Elapsed => _elapsed;

    public string ElapsedDisplay => _elapsed.ToString(@"mm\:ss");

    public RelayCommand StartCommand { get; }

    public RelayCommand PauseCommand { get; }

    public RelayCommand ResumeCommand { get; }

    public RelayCommand StopCommand { get; }

    /// <summary>Test seam: DispatcherTimer.IsEnabled reflects Start()/Stop() calls without
    /// needing a running Dispatcher message loop, so this is directly assertable in unit tests.</summary>
    internal bool IsElapsedTimerRunning => _elapsedTimer.IsEnabled;

    private void Start()
    {
        _elapsed = TimeSpan.Zero;
        _stateMachine.TransitionMission(MissionState.Active);
        _elapsedTimer.Start();
        _eventLog.Add(LogSeverity.Info, "Mission started.");
        RaiseElapsedChanged();
    }

    private void Pause()
    {
        _elapsedTimer.Stop();
        _stateMachine.TransitionMission(MissionState.Paused);
        _eventLog.Add(LogSeverity.Info, "Mission paused.");
    }

    private void Resume()
    {
        _stateMachine.TransitionMission(MissionState.Active);
        _elapsedTimer.Start();
        _eventLog.Add(LogSeverity.Info, "Mission resumed.");
    }

    private void Stop()
    {
        _elapsedTimer.Stop();
        _stateMachine.TransitionMission(MissionState.Stopped);
        _eventLog.Add(LogSeverity.Info, $"Mission stopped. Elapsed: {ElapsedDisplay}");
    }

    private void Tick()
    {
        _elapsed += TimeSpan.FromSeconds(1);
        RaiseElapsedChanged();
    }

    private void OnMissionStateChanged()
    {
        OnPropertyChanged(nameof(MissionState));
        RaiseAllCanExecuteChanged();

        if (_stateMachine.MissionState == MissionState.EmergencyAbort)
        {
            _elapsedTimer.Stop();
            _eventLog.Add(LogSeverity.Error, "Mission aborted: connection lost.");
        }
    }

    private void RaiseElapsedChanged()
    {
        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ElapsedDisplay));
    }

    private void RaiseAllCanExecuteChanged()
    {
        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
