using Shouldly;

namespace UavMissionControl.UiAutomation.Tests;

/// <summary>UI-level regression coverage for fixed defects, complementing (not replacing) the
/// unit-level regression tests in Core.Tests / App.Tests.</summary>
public class RegressionUiTests : UiAutomationTestBase
{
    [Fact]
    public void Reconnecting_LogsTelemetryLinkEstablished_ExactlyOncePerConnect()
    {
        // UI-level guard for docs/defects/04.
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");
        WaitUntil(() => CountLogEntriesContaining("Telemetry link established.") == 1);

        Btn("DisconnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Disconnected");

        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        WaitUntil(() => CountLogEntriesContaining("Telemetry link established.") == 2);
    }

    [Fact]
    public void ConnectionLoss_WhileMissionActive_ShowsEmergencyBannerAndAbortsMission()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");
        Btn("StartButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Active");

        Btn("SimulateConnectionLossButton").Invoke();

        WaitUntil(() => TextOf("MissionStateText") == "EmergencyAbort");
        WaitUntil(() => IsVisible("EmergencyBannerText"));
    }
}
