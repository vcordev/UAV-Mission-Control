# CI/CD

## Where it runs

`https://github.com/vcordev/UAV-Mission-Control`, branch `main`, workflow at
`.github/workflows/ci.yml`. Confirmed green in the repository's Actions tab after the Phase 8
push.

## Workflow shape

Two jobs, both on `windows-latest`:

```
build-and-unit-test               ui-automation-test
  checkout                          (needs: build-and-unit-test)
  setup-dotnet 10.0.x               checkout
  restore                           setup-dotnet 10.0.x
  build (Release)                   restore
  test Core.Tests                   build App (Release)
  test App.Tests                    test UiAutomation.Tests   [10 min timeout]
  upload TRX results                upload TRX results
```

`ui-automation-test` is **required, not allow-failure** — it has no `continue-on-error: true`.
Silently-allowed-to-fail CI is exactly the kind of QA smell this project is meant to demonstrate
avoiding; if the UI suite ever becomes genuinely flaky in CI, the correct response is to fix or
quarantine specific tests with a documented reason, not blanket-ignore the whole job.

## Why `windows-latest` works for real UI automation

GitHub-hosted `windows-latest` runners execute jobs under an actual interactive, logged-in
desktop session — unlike a self-hosted Windows runner running as a background service (no
desktop session), which is the classic reason UI Automation fails in CI. This is why FlaUI-style
automation is documented as working on GitHub-hosted Windows runners without any special
configuration: no virtual display setup, no headless mode, because the underlying OS session
genuinely has a desktop.

## A real gotcha this project hit and fixed

xUnit parallelizes test **classes** by default. `UavMissionControl.UiAutomation.Tests` launches
and drives a real application window per test, and UI Automation's COM interop is not safe to
call concurrently across threads without care. Running the suite for the first time surfaced
intermittent `System.ComponentModel.Win32Exception: Unexpected HRESULT has been returned from a
call to a COM component` failures — two different test classes fighting over UI Automation calls
at the same time.

**Fix:** `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
(`tests/UavMissionControl.UiAutomation.Tests/AssemblyInfo.cs`). This is the standard, documented
fix for this class of suite — UI automation tests are conventionally run serially for exactly
this reason, not because the tests themselves are slow to write faster.

## Flakiness mitigations already built in

- **No fixed `Thread.Sleep` waits for asynchronous UI state.** Every wait goes through
  `UiAutomationTestBase.WaitUntil`, which wraps `FlaUI.Core.Tools.Retry.WhileFalse` with an
  explicit timeout and polling interval — a test proceeds the moment its condition is true, and
  fails with a clear timeout message (not a silent false-pass) if it never becomes true.
- **Guaranteed process cleanup.** Every UI automation test class implements `IAsyncLifetime` and
  kills its launched process in `DisposeAsync`, even if the test body threw — an orphaned FlaUI
  process left running is a classic cause of a "stuck" or resource-starved CI runner on later runs.
- **One process per test.** xUnit creates a new test class instance per `[Fact]`, and
  `UiAutomationTestBase.InitializeAsync` launches a fresh app instance there — no test starts with
  state left over from a previous one.

## Verified locally before trusting CI

Before pushing, the exact commands CI runs were run locally in Release configuration (not just
the Debug configuration used during day-to-day development) — `dotnet build -c Release` and
`dotnet test ... -c Release` for all three projects, including the full 12-test UI automation
suite twice in a row to check for flakiness. All 106 tests passed both times before the workflow
was ever pushed.
