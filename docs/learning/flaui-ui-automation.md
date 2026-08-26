# FlaUI and UI Automation, explained from this project

## What "UI Automation" actually is

Windows UI Automation (UIA) is an accessibility API built into Windows itself — the same API
screen readers use to describe an app's UI to a blind user. Every accessible control exposes an
"automation peer": an object with a name, a control type, a bounding rectangle, and a set of
"patterns" (capabilities like `Invoke` for a button, or `Value` for a text box). A tool that
drives an app via UIA isn't simulating mouse/keyboard input at coordinates — it's asking the OS
"find me the element named X" and then "invoke it," which works regardless of window position,
DPI scaling, or whether the window even has focus.

FlaUI is a .NET wrapper around this API. It's not a browser-automation tool retargeted at
desktop — it's built for exactly this API from the ground up (see `docs/decisions.md` for why
that distinction ruled out Selenium).

## Attaching to a running app

```csharp
var exePath = Path.Combine(AppContext.BaseDirectory, "UavMissionControl.App.exe");
var app = Application.Launch(exePath);
var automation = new UIA3Automation();
var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(15));
```

(from `tests/UavMissionControl.UiAutomation.Tests/UiAutomationTestBase.cs`) — `Application.Launch`
starts a real, separate OS process running the actual compiled app; `GetMainWindow` polls (up to
the given timeout) until the main window appears. This is why the exe is found via
`AppContext.BaseDirectory`: because `UavMissionControl.UiAutomation.Tests` has a
`ProjectReference` to `UavMissionControl.App`, MSBuild automatically copies the App's built
output (the `.exe` and its dependencies) alongside the test assembly — no separate deployment
step needed.

## Finding elements: `AutomationId`

```csharp
Window.FindFirstDescendant(cf => cf.ByAutomationId("ConnectButton"))
```

`AutomationId` is a stable identifier a developer can set explicitly — WPF, for elements that get
an automation peer at all, defaults it to the element's `x:Name` if not set otherwise. Every
interactive element in this app's XAML has an explicit `x:Name` for exactly this reason (see
`ConnectionPanelView.xaml`: `x:Name="ConnectButton"`). Finding elements by `AutomationId` rather
than by visible text or screen position means tests keep working if the button's label text or
position changes — they only break if its *identity* changes, which is the right sensitivity for
a test to have.

## A real gotcha: not every WPF element has an automation peer

`Border` doesn't get one by default (only `Control`-derived elements and a few special cases like
`TextBlock` do) — see `docs/learning/wpf-and-mvvm.md` for the full story of how this was found and
fixed in `WarningsBannerView.xaml`. The practical lesson: if `FindFirstDescendant` can't find an
element you know exists in the visual tree, check whether it's the kind of element that gets an
automation peer at all before assuming the `AutomationId` is wrong.

## Waiting for asynchronous state, correctly

UI actions aren't instant — clicking Connect doesn't make the status text say "Connected" on the
very next line of test code (there's a deliberate ~1.2 second simulated connect delay). The wrong
fix is `Thread.Sleep(2000)` — sometimes too short (flaky), always too long (slow). The right fix
is polling with a timeout:

```csharp
protected void WaitUntil(Func<bool> condition, int timeoutSeconds = 10) =>
    Retry.WhileFalse(condition, TimeSpan.FromSeconds(timeoutSeconds), throwOnTimeout: true);
```

(`UiAutomationTestBase.WaitUntil`, wrapping `FlaUI.Core.Tools.Retry`) — the test proceeds the
moment the condition becomes true, and fails with a clear timeout exception (not a silent wrong
assertion) if it never does.

## Real problems this project hit and fixed

- **xUnit parallelizes test classes by default; UI Automation's COM interop isn't safe to call
  concurrently.** Running the suite for the first time produced intermittent
  `Win32Exception: Unexpected HRESULT` failures from two test classes driving separate app
  windows at the same time. Fixed with
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]` — see `docs/ci-cd.md`.
- **Forgetting that telemetry only flows once connected.** Two early banner tests clicked a
  "trigger low battery" button without connecting first — since the telemetry simulator only
  starts ticking on Connect, the forced value never actually reached the UI, and the tests failed
  waiting for a banner that could never appear. Not a FlaUI problem at all — a reminder that
  UI automation tests need the same understanding of the app's actual behavior as any other test.
- **Guaranteed cleanup.** Every test class implements `IAsyncLifetime` and kills its process in
  `DisposeAsync`, even on assertion failure — otherwise a failed test leaves an orphaned app
  process running, which is a common cause of a "stuck" CI runner or confusing local re-runs.
