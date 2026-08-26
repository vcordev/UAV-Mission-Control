# GitHub Actions / CI-CD, explained from this project

## What CI actually buys you

Continuous Integration means: every time code changes, an automated, clean-environment build and
test run happens — not "it worked on my machine," but "it worked on a fresh checkout, from
scratch, with no leftover local state." The value isn't the workflow file itself; it's that a
reviewer (or a future you) can trust a green checkmark without re-running everything by hand.

## Anatomy of this project's workflow

`.github/workflows/ci.yml` is YAML describing *when* to run and *what* to run:

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

This means: run on every push to `main`, and on every pull request targeting `main` (so a PR
shows its checks before it's ever merged).

```yaml
jobs:
  build-and-unit-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore UavMissionControl.slnx
      - run: dotnet build UavMissionControl.slnx --no-restore -c Release
      - run: dotnet test tests/UavMissionControl.Core.Tests --no-build -c Release ...
```

Each `step` is one action in a fresh virtual machine: `actions/checkout` clones the repo,
`actions/setup-dotnet` installs the requested SDK, then plain `dotnet` CLI commands — the exact
same commands used locally throughout this project, which is why they were verified locally in
Release configuration first (see `docs/ci-cd.md`) rather than discovered to be broken only after
pushing.

## Jobs run in parallel by default; `needs` makes them sequential

This workflow has two jobs. Without any relationship declared, GitHub Actions would run them
concurrently on separate VMs. `ui-automation-test` declares `needs: build-and-unit-test`,
meaning: don't even start until the first job succeeds. This project uses that specifically so a
basic build/unit-test failure is reported fast, without waiting for (or wasting minutes on) the
slower UI automation job.

## Artifacts: getting results out of a throwaway VM

```yaml
- uses: actions/upload-artifact@v4
  if: always()
  with: { name: unit-test-results, path: TestResults }
```

The VM a job runs on is destroyed after the job finishes — anything not explicitly saved is gone.
`upload-artifact` copies the `.trx` test result files somewhere downloadable from the Actions run
page. `if: always()` matters: without it, the upload step would be skipped whenever a *previous*
step (the test run itself) failed — exactly the case where you most want to see the results.

## Why this matters for a WPF app specifically

Desktop UI apps are often assumed to be "impossible to test in CI" because CI runners are
headless. That's true for a *self-hosted* Windows runner configured as a background service (no
logged-in desktop session) — but GitHub-hosted `windows-latest` runners execute under a real
interactive session, so UI Automation (and therefore FlaUI) works without any special
configuration. This is a decision worth explaining out loud in an interview: knowing *why*
`windows-latest` specifically makes this possible, not just that it happens to work.

## A concurrency gotcha CI surfaced

xUnit parallelizes test classes by default; running two UI-automation test classes concurrently
(each launching and driving its own real app window) produced intermittent COM interop failures
locally before this was ever pushed. Fixed with
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` — see `docs/ci-cd.md` and
`docs/learning/flaui-ui-automation.md` for the full story. This is exactly the kind of problem
"basic performance testing... under various workloads" thinking is meant to catch: parallel test
execution is itself a workload, and this suite wasn't safe under it until fixed.
