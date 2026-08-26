# Decisions

ADR-style: What was decided, Why, How it was implemented, what Alternatives existed, and the
Trade-offs accepted. Ordered roughly by when each decision was made.

## D1. Target framework: .NET 10

**What:** `net10.0` (Core) / `net10.0-windows` (App), pinned via `global.json` (`10.0.400`,
`rollForward: latestMinor`).

**Why:** At the time this project started (Aug 2026), .NET 8 and .NET 9 were both roughly
2.5 months from End-of-Support (Nov 10, 2026). .NET 10 is the current LTS, supported through
Nov 2028.

**How:** Installed via the official Microsoft SDK installer (no `winget` available in this
environment); `global.json` pins the exact SDK version so `dotnet` commands are reproducible
across machines/CI.

**Alternatives:** Stay on .NET 8 (previous LTS) — would have been the "safe, boring" choice a
few months earlier, but building new work on a runtime weeks from EOL undercuts a project meant
to demonstrate engineering judgment.

**Trade-off:** .NET 10 is newer and less battle-tested than .NET 8 was at the same age. Accepted
because the alternative (shipping on soon-to-be-unsupported infrastructure) is worse for a
long-lived reference/portfolio piece.

## D2. Assertion library: Shouldly, not FluentAssertions

**What:** Shouldly (MIT license) for all three test projects.

**Why:** FluentAssertions v8+ (released Jan 2025) requires a paid commercial license for
non-OSS use; only v7.x and earlier remain Apache-2.0. A public portfolio repository that a
reviewer clones and runs must not silently trip a license gate.

**How:** `Shouldly` package reference in all test `.csproj` files; `.ShouldBe(...)` /
`Should.Throw<T>(...)` throughout.

**Alternatives:** Pin `FluentAssertions` to `7.2.0` (last free version) — viable, but pinning a
library specifically to dodge its own licensing model is a worse long-term choice than picking
an actively-maintained MIT alternative. `AwesomeAssertions` (a community fork of pre-license
FluentAssertions) was also considered.

**Trade-off:** Shouldly's failure messages and API surface differ slightly from FluentAssertions
(more common in tutorials/StackOverflow answers), a minor familiarity cost.

## D3. Test framework: xUnit v2, not v3

**What:** `xunit` 2.9.3 + `xunit.runner.visualstudio` (template default, 3.1.4).

**Why:** xUnit v3 introduces a new execution model (Microsoft.Testing.Platform) whose tooling
support across editors/CI was still stabilizing at the time. v2 is the boring, ubiquitous,
extremely well-documented choice — the right default for test infrastructure whose entire point
is reliability, not for chasing the newest execution model.

**Alternatives:** xUnit v3 (newer, MTP-native); NUnit; MSTest.

**Trade-off:** Will eventually need a migration to v3 as v2 ages out of active development —
acceptable for now given how young v3's tooling ecosystem still is.

## D4. WPF UI automation: FlaUI, not Selenium/Appium/WinAppDriver

**What:** FlaUI.Core + FlaUI.UIA3, version 5.0.0.

**Why:** The target job posting names "Selenium, Appium, or similar tools" for automated UI
testing — but neither is actually suited to a native WPF app:

- **Selenium** speaks the WebDriver protocol against a DOM. A WPF window has no DOM; Selenium has
  no applicable concept of a native Win32/WPF element tree. This isn't a matter of configuration —
  it's the wrong protocol for the target entirely.
- **Appium**'s Windows support is a thin wrapper around **WinAppDriver**, which has had no stable
  release since November 2020 and 1,100+ open issues at the time of writing — effectively
  unmaintained. Appium also requires running a separate server process for what could be an
  in-process test.
- **FlaUI** is an MIT-licensed, actively maintained (commit activity into mid-2026) .NET library
  built directly on Windows UI Automation (the same OS-level accessibility API WinAppDriver/Appium
  ultimately sit on top of, minus the extra server hop). It plugs directly into an ordinary xUnit
  test project.

Correctly identifying that the posting's literal tool suggestion doesn't fit the target platform,
and picking the tool that actually does, is itself meant to be a signal in this project.

**How:** `tests/UavMissionControl.UiAutomation.Tests` launches the real built `.exe` via
`FlaUI.Core.Application.Launch`, drives it via `UIA3Automation`, and tears the process down in
`IAsyncLifetime.DisposeAsync` for every test.

**Alternatives considered and rejected:** WinAppDriver, Appium (Windows driver), Selenium (see
above); Coded UI (Microsoft, deprecated years ago, not viable on .NET Core/5+ anyway).

**Trade-off:** FlaUI has no Native AOT support as of this writing (open upstream issue) —
irrelevant here since nothing in this project is AOT-published, but worth naming as a real,
checked trade-off rather than an unexamined pick.

## D5. Hand-rolled MVVM primitives, not CommunityToolkit.Mvvm

**What:** `RelayCommand` and `ViewModelBase` (`INotifyPropertyChanged` + `SetProperty`) written
by hand in `src/UavMissionControl.App/ViewModels/`.

**Why:** The dependency surface stays small, and being able to explain exactly what
`RaiseCanExecuteChanged()` does and why it's called explicitly (rather than relying on
`CommandManager.RequerySuggested`, which only re-queries on UI input events, not on
state-machine-driven changes) is a more defensible interview answer than "the source generator
handles it."

**Alternatives:** `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]` source
generators) — the more common real-world choice for anything beyond a small app.

**Trade-off:** More boilerplate per ViewModel than the toolkit would require. Acceptable at this
project's scale (5 ViewModels); would reconsider for a larger app.

## D6. Performance/reliability tooling: hand-written, not BenchmarkDotNet/NBomber

**What:** `Stopwatch`-based relative timing comparisons (`EventLogViewModelPerformanceTests`) and
plain loops for soak testing (`SoakTests`), not a dedicated benchmarking framework.

**Why:** BenchmarkDotNet is built for micro-benchmark precision (nanosecond-level, statistically
rigorous) that this project doesn't need — the actual question ("does this degrade badly as the
collection grows?") is answerable with a relative before/after comparison on the same run.
NBomber targets load/throughput testing against services; this is a single-user desktop app with
no server to load-test.

**How:** See `docs/performance-report.md` for the actual measurements and methodology (relative
ratio assertions, not fixed millisecond budgets, to stay stable across different machines).

**Trade-off:** Less statistically rigorous than a real benchmarking tool would provide — accepted
because the questions being asked ("O(1) or O(n)?", "does 100k ticks survive?") don't need that
rigor.

## D7. Defect #06: tried auto-scroll, reverted it

**What:** After fixing the O(n) `Insert(0, ...)` performance defect by switching to `Add()`
(append), the display order changed from newest-first to oldest-first. A first attempt to
preserve newest-first *and* fix performance added a `ListView.ScrollIntoView` call in
`EventLogView`'s code-behind, triggered from the `ObservableCollection`'s `CollectionChanged`
event.

**What happened:** This crashed the app — calling `ScrollIntoView` synchronously from inside the
same `CollectionChanged` notification the `ListView`'s own `ItemContainerGenerator` was still
processing is a WPF reentrancy hazard. The UI automation suite caught this immediately (9 of 11
tests failed with "element not found" because the window had disappeared). Deferring the call via
`Dispatcher.BeginInvoke(..., DispatcherPriority.Background)` stopped the crash, but a live
screenshot check afterward showed the scroll still wasn't reliably landing at the bottom.

**Decision:** Removed the auto-scroll behavior entirely rather than keep layering complexity onto
a cosmetic feature. Oldest-first with no auto-scroll was accepted as the simpler, honestly-worse
(but correct and non-crashing) outcome. Full detail in `docs/defects/06-eventlog-insert-performance.md`.

**Why this is worth recording as a decision, not just a bug fix:** the choice to *stop* iterating
on a UI polish feature once it proved fragile — rather than keep patching it — was deliberate.
Knowing when a refinement isn't worth its complexity is itself a judgment call worth being able to
explain.

## D8. Push to a real, public GitHub remote

**What:** The repository is pushed to `https://github.com/vcordev/qa-challenge`, public, with
GitHub Actions CI actually executing (not just an authored-but-unrun workflow file).

**Why:** A real green Actions run is strictly more useful to reference in an interview than an
unexecuted YAML file — it's verifiable, not just claimed.

**How:** No `gh` CLI or stored git credentials were available in the local environment; the
repository was created manually via the GitHub web UI, and `git push` used Git Credential
Manager (already installed system-wide with Git for Windows) for authentication.

**Trade-off:** None significant — this is a portfolio piece built specifically to be shared.
