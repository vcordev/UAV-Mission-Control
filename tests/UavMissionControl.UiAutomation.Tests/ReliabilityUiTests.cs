using Shouldly;

namespace UavMissionControl.UiAutomation.Tests;

/// <summary>
/// System-level reliability coverage: this is the soak-style test that would have caught
/// defect #03 (EventLogViewModel mutating its ObservableCollection off the UI thread) at the
/// system level, complementing the fast unit-level dispatcher-routing test. See
/// docs/defects/03-eventlog-cross-thread-crash.md and docs/performance-report.md.
/// </summary>
public class ReliabilityUiTests : UiAutomationTestBase
{
    [Fact]
    public void StressCyclingWarningScenarios_WhileConnected_DoesNotCrashTheApp()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        for (var i = 0; i < 30; i++)
        {
            Btn(i % 2 == 0 ? "TriggerCriticalBatteryButton" : "ClearScenariosButton").Invoke();
            Thread.Sleep(600);
        }

        // If the app crashed mid-loop, the button invocations above would already have thrown.
        // This final check confirms the window is still alive and responsive.
        TextOf("StatusText").ShouldBe("Connected");
    }
}
