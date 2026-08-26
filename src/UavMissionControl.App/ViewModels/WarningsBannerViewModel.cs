using UavMissionControl.Core.Domain;

namespace UavMissionControl.App.ViewModels;

public sealed class WarningsBannerViewModel : ViewModelBase
{
    private readonly TelemetryDashboardViewModel _telemetry;
    private readonly MissionControlViewModel _mission;

    public WarningsBannerViewModel(TelemetryDashboardViewModel telemetry, MissionControlViewModel mission)
    {
        _telemetry = telemetry;
        _mission = mission;

        _telemetry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or "" or nameof(TelemetryDashboardViewModel.BatteryStatus)
                or nameof(TelemetryDashboardViewModel.SignalStatus))
            {
                RaiseAllChanged();
            }
        };

        _mission.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MissionControlViewModel.MissionState))
            {
                RaiseAllChanged();
            }
        };
    }

    public BatteryStatus BatteryStatus => _telemetry.BatteryStatus;

    public SignalStatus SignalStatus => _telemetry.SignalStatus;

    public bool IsBatteryLow => BatteryStatus == BatteryStatus.Low;

    public bool IsBatteryCritical => BatteryStatus == BatteryStatus.Critical;

    public bool IsSignalWeak => SignalStatus == SignalStatus.Weak;

    public bool IsEmergencyAbort => _mission.MissionState == MissionState.EmergencyAbort;

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(BatteryStatus));
        OnPropertyChanged(nameof(SignalStatus));
        OnPropertyChanged(nameof(IsBatteryLow));
        OnPropertyChanged(nameof(IsBatteryCritical));
        OnPropertyChanged(nameof(IsSignalWeak));
        OnPropertyChanged(nameof(IsEmergencyAbort));
    }
}
