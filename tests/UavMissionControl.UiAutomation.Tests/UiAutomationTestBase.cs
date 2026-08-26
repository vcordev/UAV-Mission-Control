using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace UavMissionControl.UiAutomation.Tests;

/// <summary>
/// Launches a fresh instance of the app for every test method (xUnit creates a new test class
/// instance per [Fact]/[Theory] case, so IAsyncLifetime here means "one process per test") and
/// guarantees it's killed afterwards even if the test fails an assertion mid-way — orphaned
/// FlaUI-launched processes are the classic cause of flaky/stuck CI runs for this kind of suite.
/// </summary>
public abstract class UiAutomationTestBase : IAsyncLifetime
{
    private Application? _app;
    private UIA3Automation? _automation;

    protected Window Window { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "UavMissionControl.App.exe");
        _app = Application.Launch(exePath);
        _automation = new UIA3Automation();
        Window = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(15))
                 ?? throw new InvalidOperationException("Main window did not appear within 15s.");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            _app?.Close();
        }
        catch
        {
            // best-effort graceful close
        }
        finally
        {
            try
            {
                _app?.Kill();
            }
            catch
            {
                // process may already be gone
            }

            _automation?.Dispose();
        }

        return Task.CompletedTask;
    }

    protected Button Btn(string automationId) =>
        (Elem(automationId) ?? throw new InvalidOperationException($"Element '{automationId}' not found.")).AsButton();

    protected AutomationElement? Elem(string automationId) =>
        Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    protected string TextOf(string automationId) =>
        Elem(automationId)?.Name ?? throw new InvalidOperationException($"Element '{automationId}' not found.");

    protected bool IsVisible(string automationId) => Elem(automationId)?.IsOffscreen == false;

    protected void WaitUntil(Func<bool> condition, int timeoutSeconds = 10) =>
        Retry.WhileFalse(condition, TimeSpan.FromSeconds(timeoutSeconds), throwOnTimeout: true);

    protected int CountLogEntriesContaining(string text) =>
        Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Count(e => e.Name.Contains(text, StringComparison.Ordinal));
}
