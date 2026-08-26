using UavMissionControl.App.Services;
using UavMissionControl.Core.Domain;
using UavMissionControl.Core.Logging;
using UavMissionControl.Core.Simulation;

namespace UavMissionControl.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly UavStateMachine _stateMachine;
    private readonly ITelemetrySimulator _simulator;
    private readonly ScenarioInjector _scenarioInjector;
    private readonly IEventLog _eventLog;

    public MainViewModel() : this(new UavStateMachine(), new EventLog(), null, new WpfDispatcher())
    {
    }

    internal MainViewModel(
        UavStateMachine stateMachine,
        IEventLog eventLog,
        ITelemetrySimulator? simulator,
        IUiDispatcher dispatcher)
    {
        _stateMachine = stateMachine;
        _eventLog = eventLog;
        _simulator = simulator ?? new TelemetrySimulator(() => stateMachine.MissionState);
        _scenarioInjector = new ScenarioInjector(_simulator);

        ConnectionPanel = new ConnectionPanelViewModel(stateMachine, eventLog);
        MissionControl = new MissionControlViewModel(stateMachine, eventLog);
        TelemetryDashboard = new TelemetryDashboardViewModel(_simulator, dispatcher);
        EventLog = new EventLogViewModel(eventLog, dispatcher);
        WarningsBanner = new WarningsBannerViewModel(TelemetryDashboard, MissionControl);

        TriggerLowBatteryCommand = new RelayCommand(_scenarioInjector.TriggerLowBattery);
        TriggerCriticalBatteryCommand = new RelayCommand(_scenarioInjector.TriggerCriticalBattery);
        TriggerWeakSignalCommand = new RelayCommand(_scenarioInjector.TriggerWeakSignal);
        ClearScenariosCommand = new RelayCommand(_scenarioInjector.ClearAllScenarios);

        SimulateConnectionLossCommand = new RelayCommand(
            SimulateConnectionLoss,
            () => _stateMachine.ConnectionState == ConnectionState.Connected);

        _stateMachine.ConnectionStateChanged += (_, state) =>
        {
            if (state == ConnectionState.Connected)
            {
                _simulator.Start();
            }
            else if (state == ConnectionState.Disconnected)
            {
                _simulator.Stop();
            }

            SimulateConnectionLossCommand.RaiseCanExecuteChanged();
        };
    }

    public ConnectionPanelViewModel ConnectionPanel { get; }

    public MissionControlViewModel MissionControl { get; }

    public TelemetryDashboardViewModel TelemetryDashboard { get; }

    public EventLogViewModel EventLog { get; }

    public WarningsBannerViewModel WarningsBanner { get; }

    public RelayCommand TriggerLowBatteryCommand { get; }

    public RelayCommand TriggerCriticalBatteryCommand { get; }

    public RelayCommand TriggerWeakSignalCommand { get; }

    public RelayCommand ClearScenariosCommand { get; }

    public RelayCommand SimulateConnectionLossCommand { get; }

    private void SimulateConnectionLoss()
    {
        _eventLog.Add(LogSeverity.Error, "Simulated connection loss (QA test scenario).");
        _stateMachine.TransitionConnection(ConnectionState.Disconnected);
    }
}
