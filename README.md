# UAV Mission Control — QA Engineering Portfolio Project

An original, independent WPF desktop app simulating a UAV Mission Control system, built as a
portfolio project demonstrating QA engineering skills for desktop/WPF applications — test
strategy and planning, functional/negative/boundary/regression/reliability/performance testing,
automated UI testing with tooling appropriate to the platform, defect tracking, and CI/CD. It is
not a copy of, or affiliated with, any real company's product.

## What it is

Connection management, mission control (start/pause/resume/stop), live telemetry (battery,
signal, GPS, altitude, speed), threshold-based warnings, and an event log — all governed by an
explicit, unit-tested state machine. Around it: a 111-test automated suite across three layers, 6
documented-and-fixed defects, 2 documented-and-deliberately-unresolved defects (kept as permanent,
intentionally-failing regression proofs — see "Known, unresolved defects" below), basic
performance/reliability testing, and a green GitHub Actions CI pipeline (the 2 intentional
failures are excluded from the CI gate by category filter, not hidden — see `docs/test-strategy.md`).

## Requirements

- .NET 10 SDK
- Windows (WPF is Windows-only)

## Build, run, test

```
dotnet build UavMissionControl.slnx
dotnet run --project src/UavMissionControl.App
dotnet test UavMissionControl.slnx
```

`dotnet test` runs all three test projects: `UavMissionControl.Core.Tests` (56 tests, all
passing), `UavMissionControl.App.Tests` (41 tests — 39 passing + 2 intentionally failing),
and `UavMissionControl.UiAutomation.Tests` (14 tests — 12 passing + 2 intentionally failing;
these launch the real built app via FlaUI, so build the App project first if running them alone).

To get the clean "all green" run (matching the CI gate), exclude the known-defect category:

```
dotnet test UavMissionControl.slnx --filter "Category!=KnownDefect"
```

To see the 4 intentional failures on demand (proof that the 2 unresolved defects below are
still detectable):

```
dotnet test UavMissionControl.slnx --filter "Category=KnownDefect"
```

## Known, unresolved defects

Two defects (`docs/defects/07`, `docs/defects/08`) are deliberately left unfixed in this
codebase, each with regression tests at the App and UI-automation layers that are expected to
FAIL — proof the defect is real and automatically detectable, not just anecdotally observed
during manual testing. See each defect's doc for full root-cause detail, and
`docs/test-strategy.md` for how they're excluded from the CI gate without being hidden.

## Documentation

- [`docs/architecture.md`](docs/architecture.md) — solution layout, state machine, telemetry pipeline, MVVM composition
- [`docs/test-strategy.md`](docs/test-strategy.md) — test strategy, test plan, technique→defect mapping
- [`docs/decisions.md`](docs/decisions.md) — technology choices with alternatives and trade-offs
- [`docs/ci-cd.md`](docs/ci-cd.md) — GitHub Actions workflow and why it works for WPF UI automation
- [`docs/performance-report.md`](docs/performance-report.md) — performance & reliability findings
- [`docs/defects/`](docs/defects/) — 6 defects, each with repro/root-cause/fix/regression-coverage
- [`docs/test-cases/`](docs/test-cases/) — manual/exploratory test case documentation
- [`docs/learning/`](docs/learning/) — beginner-level explainers grounded in this actual codebase (WPF/MVVM, state machines, xUnit, FlaUI, GitHub Actions)
- [`docs/interview/`](docs/interview/) — interview prep, honest answers grounded only in this project
- [`docs/final-report.md`](docs/final-report.md) — summary tying everything together

## CI

[![CI](https://github.com/vcordev/qa-challenge/actions/workflows/ci.yml/badge.svg)](https://github.com/vcordev/qa-challenge/actions/workflows/ci.yml)
