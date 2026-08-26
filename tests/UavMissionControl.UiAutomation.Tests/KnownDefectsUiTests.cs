using Shouldly;

namespace UavMissionControl.UiAutomation.Tests;

/// <summary>
/// UI-level regression coverage for two KNOWN, DELIBERATELY UNRESOLVED defects
/// (<c>docs/defects/07-stop-mission-no-idle-transition.md</c> and
/// <c>docs/defects/08-emergency-abort-banner-persists-after-reconnect.md</c>).
///
/// Unlike every other test in this suite, the two tests here are expected to FAIL against the
/// current build — that is the point. They exist so each defect is provably detectable end to
/// end, against the real running app, not just in an isolated ViewModel test — matching the
/// manual repro steps in <c>docs/test-cases/TC-002</c> and <c>TC-003</c>.
///
/// Both are tagged [Trait("Category","KnownDefect")] so `dotnet test --filter
/// "Category!=KnownDefect"` reproduces an all-green demo run without silently hiding these
/// tests (they still exist, are still collected, and still run when asked for — they are not
/// [Fact(Skip=...)]).
///
/// Do not "fix" these tests by loosening their assertions — either fix the production defect
/// they document, or leave both the code and the test alone.
/// </summary>
public class KnownDefectsUiTests : UiAutomationTestBase
{
    [Fact]
    [Trait("Category", "KnownDefect")]
    public void StartButton_ShouldReenable_AfterStop_DEFECT07()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");
        Btn("StartButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Active");

        Btn("StopButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Stopped");

        // Expected (correct) behavior: Start should be clickable again for a second mission.
        Elem("StartButton")!.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "KnownDefect")]
    public void EmergencyBanner_ShouldClear_AfterReconnecting_DEFECT08()
    {
        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");
        Btn("StartButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "Active");

        Btn("SimulateConnectionLossButton").Invoke();
        WaitUntil(() => TextOf("MissionStateText") == "EmergencyAbort");
        WaitUntil(() => IsVisible("EmergencyBannerText"));

        Btn("ConnectButton").Invoke();
        WaitUntil(() => TextOf("StatusText") == "Connected");

        // Expected (correct) behavior: reconnecting should clear the stale emergency banner.
        WaitUntil(() => !IsVisible("EmergencyBannerText"), timeoutSeconds: 3);
    }
}
