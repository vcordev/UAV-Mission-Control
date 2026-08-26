# UAV Mission Control — TEKEVER QA Portfolio Project

An original, independent WPF desktop app simulating a UAV Mission Control system, built as a
portfolio project demonstrating the QA skills asked for in TEKEVER's Mid-Level QA Engineer
posting — not a copy of, or affiliated with, any TEKEVER product.

## What it is

Connection management, mission control (start/pause/resume/stop), live telemetry (battery,
signal, GPS, altitude, speed), threshold-based warnings, and an event log — all governed by an
explicit, unit-tested state machine. Around it: a 106-test automated suite across three layers, 6
documented and fixed defects, basic performance/reliability testing, and a green GitHub Actions
CI pipeline.

## Requirements

- .NET 10 SDK
- Windows (WPF is Windows-only)

## Build, run, test

```
dotnet build UavMissionControl.slnx
dotnet run --project src/UavMissionControl.App
dotnet test UavMissionControl.slnx
```

`dotnet test` runs all three test projects: `UavMissionControl.Core.Tests` (55 tests),
`UavMissionControl.App.Tests` (39 tests), and `UavMissionControl.UiAutomation.Tests` (12 tests —
these launch the real built app via FlaUI, so build the App project first if running them alone).

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
