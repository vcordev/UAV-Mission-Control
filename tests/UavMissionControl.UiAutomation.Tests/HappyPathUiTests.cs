using Shouldly;

namespace UavMissionControl.UiAutomation.Tests;

/// <summary>Automates TC-001 (docs/test-cases/TC-001-happy-path-mission-lifecycle.md).</summary>
public class HappyPathUiTests : UiAutomationTestBase
{
    [Fact]
    public void InitialState_IsDisconnectedAndIdle()
    {
        TextOf("StatusText").ShouldBe("Disconnected");
        TextOf("MissionStateText").ShouldBe("Idle");
        TextOf("BatteryText").ShouldBe("100.0%");
        TextOf("SignalText").ShouldBe("100.0%");
    }

    [Fact]
    public void FullMissionLifecycle_ConnectStartPauseResumeStop()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        Btn("StartButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Active");

        Btn("PauseButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Paused");
        var elapsedAtPause = TextOf("ElapsedText");
        Thread.Sleep(2200);
        TextOf("ElapsedText").ShouldBe(elapsedAtPause);

        Btn("ResumeButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Active");

        Btn("StopButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Stopped");
    }

    [Fact]
    public void SimulateConnectionLoss_DisconnectsAndLogsIt()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        Btn("SimulateConnectionLossButton").Invoke();

        WaitUntil(() => TextOf("StatusText") == "Disconnected");
        WaitUntil(() => CountLogEntriesContaining("Simulated connection loss") == 1);
    }
}
