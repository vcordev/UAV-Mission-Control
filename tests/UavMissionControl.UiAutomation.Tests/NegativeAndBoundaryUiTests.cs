using Shouldly;

namespace UavMissionControl.UiAutomation.Tests;

public class NegativeAndBoundaryUiTests : UiAutomationTestBase
{
    [Fact]
    public void AllMissionButtons_Disabled_WhileDisconnected()
    {
        Elem("StartButton")!.IsEnabled.ShouldBeFalse();
        Elem("PauseButton")!.IsEnabled.ShouldBeFalse();
        Elem("ResumeButton")!.IsEnabled.ShouldBeFalse();
        Elem("StopButton")!.IsEnabled.ShouldBeFalse();
        Elem("DisconnectButton")!.IsEnabled.ShouldBeFalse();
        Elem("SimulateConnectionLossButton")!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void StopButton_Disabled_WhenConnectedButMissionIdle()
    {
        // Regression guard for docs/defects/02 at the UI level, not just via unit tests.
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        Elem("StopButton")!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void StartButton_Disabled_AfterMissionAlreadyStarted()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");
        Btn("StartButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Active");

        Elem("StartButton")!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void CriticalBatteryBanner_ExactlyAtThreshold_Appears()
    {
        // Boundary value at the UI level: TelemetryThresholds.CriticalBatteryPercent (10).
        // Telemetry only ticks while connected (the simulator only starts on Connect), so the
        // scenario buttons have no visible effect until then.
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        Btn("TriggerCriticalBatteryButton").Invoke();

        WaitUntil(() => IsVisible("CriticalBatteryBannerText"));
        TextOf("BatteryText").ShouldBe("10.0%");
    }

    [Fact]
    public void LowBatteryBanner_ExactlyAtThreshold_Appears()
    {
        // Regression guard for docs/defects/01 at the UI level.
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        Btn("TriggerLowBatteryButton").Invoke();

        WaitUntil(() => IsVisible("LowBatteryBannerText"));
        TextOf("BatteryText").ShouldBe("20.0%");
    }

    [Fact]
    public void ClearScenarios_HidesAllWarningBanners()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");
        Btn("TriggerCriticalBatteryButton").Invoke();
        WaitUntil(() => IsVisible("CriticalBatteryBannerText"));

        Btn("ClearScenariosButton").Invoke();

        WaitUntil(() => !IsVisible("CriticalBatteryBannerText"));
    }
}
