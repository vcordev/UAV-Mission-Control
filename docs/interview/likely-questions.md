# Interview prep: likely questions and honest answers

Answers are grounded only in what this project actually contains — no invented metrics, no
claimed production experience beyond this project. Where a real number exists (test counts,
timing measurements), it's the actual measured number.

## "Walk me through this project."

I built an original WPF desktop app — a UAV Mission Control simulator — specifically to
demonstrate the QA skills your posting asks for: manual and automated test case design, defect
tracking, CI/CD, and picking the right automation tooling for a desktop app rather than defaulting
to web tools. It's not a copy of anything real — connection management, mission
start/pause/stop, live telemetry, threshold-based warnings, and an event log, backed by an
explicit state machine. Around that I built a three-layer automated test suite (106 tests: 55
domain-level, 39 ViewModel-level, 12 full UI automation), planted and documented 6 realistic
defects with full repro/root-cause/fix writeups, ran basic performance and reliability testing,
and wired up GitHub Actions CI that actually runs and passes.

## "The posting mentions Selenium and Appium. Why did you use FlaUI instead?"

Because neither actually applies to a native WPF app. Selenium speaks the WebDriver protocol
against a browser DOM — a WPF window has no DOM at all, so it's not a configuration problem, it's
the wrong protocol. Appium's Windows support is a wrapper around WinAppDriver, which hasn't had a
stable release since 2020 and has over a thousand open issues — effectively unmaintained. FlaUI
is built directly on Windows UI Automation (the same accessibility API those tools ultimately sit
on top of), is MIT-licensed, actively maintained, and plugs straight into xUnit without a
separate driver process. I wrote the full comparison up in `docs/decisions.md` because I think
correctly identifying that the posting's literal suggestion doesn't fit the target platform — and
picking the tool that does — is itself a useful signal.

## "Tell me about a bug you found and how you found it."

The most interesting one: `EventLogViewModel` was mutating a UI-bound `ObservableCollection` from
a background thread, because a later feature (auto-logging warnings when telemetry crosses a
threshold) started calling into event-logging code from the same background timer that drives
telemetry. That's invisible in a quick manual test — it only crashes under sustained real use. I
caught it two ways: a fast, deterministic unit test that asserts every log mutation routes through
a dispatcher-marshaling seam (no threading involved in the test itself — I'm testing "is the safety
mechanism used," not trying to race a real thread), and a live reproduction where I stress-cycled
warning triggers against the actual running app and got the real crash with a real stack trace.
Both are documented in `docs/defects/03-eventlog-cross-thread-crash.md`, and the live repro became
a permanent regression test afterward.

## "How do you decide what to test, given limited time?"

Risk-based prioritization, not uniform coverage. The state machine got exhaustive
transition-matrix testing because a bug there could put the "aircraft" in a nonsensical or unsafe
state — the highest-consequence code in the project. Every place telemetry or logging crosses from
a background thread into UI-bound state got a dedicated test proving it marshals correctly,
because that exact class of bug is invisible in casual testing and only shows up under load.
Performance testing went specifically to the one place with unbounded growth (the event log), not
everywhere, because a bounded value has no analogous risk. `docs/test-strategy.md` section 1.3
has the full reasoning.

## "What's a mistake you made on this project, and what did you do about it?"

Fixing the event-log performance defect, I first tried to keep the newest-entry-on-top display
while fixing the O(n) insert cost, by auto-scrolling a `ListView` to the bottom whenever a new
entry arrived. That crashed the app — I was calling `ScrollIntoView` synchronously from inside the
same `CollectionChanged` notification the `ListView`'s own internal item generator was still
processing, a real WPF reentrancy hazard. My UI automation suite caught it immediately (most of
the UI tests failed with "element not found" because the window had disappeared). I deferred the
call to a background-priority dispatcher operation, which stopped the crash, but a live visual
check afterward showed the scroll still wasn't reliably landing at the bottom. Rather than keep
adding complexity to a cosmetic feature, I reverted it and accepted oldest-first display with no
auto-scroll as the simpler, correct, if less polished, outcome — documented as its own decision in
`docs/decisions.md` (D7), including why I think the decision to *stop* was itself the right call.

## "How would you test performance for a desktop app like this?"

I didn't reach for a load-testing tool like NBomber (that's for testing services under concurrent
user load, and this is a single-user desktop app) or a micro-benchmarking framework like
BenchmarkDotNet (more statistical rigor than the actual question needed). Instead I asked a
narrower, real question — "does the cost of an operation degrade badly as data grows?" — and
answered it with a relative comparison on the same run: time a batch of adds early, grow the
collection, time an identical batch again, and compare the ratio. That's stable across different
machines (an absolute millisecond assertion isn't) and it's exactly what caught the O(n) event-log
defect: a 20x cost increase for an 11x size increase, dropping to roughly 0x after the fix. Full
numbers in `docs/performance-report.md`.

## "What would you do differently with more time, or for a bigger project?"

`CommunityToolkit.Mvvm`'s source generators over the hand-rolled `RelayCommand`/`ViewModelBase` —
the boilerplate cost is fine at 5 ViewModels but wouldn't scale gracefully to a much larger app.
I'd also want a real profiler (dotMemory/PerfView) rather than the coarse `GC.GetTotalMemory`
before/after check I used for the soak test — that's a sanity bound against gross duplication or
retention, not a precision leak detector, and I say so explicitly in `docs/performance-report.md`
rather than overstating what it proves.

## "What don't you know yet that you'd want to learn before starting?"

Real-world Agile/Scrum ceremonies and defect-tracking tooling (Jira, Azure DevOps) specifically at
TEKEVER — everything in this project's defect docs follows a format I designed myself for clarity,
not a specific tool's template, and I'd want to adapt quickly to whatever the team actually uses.
I'd also want to understand what CI infrastructure TEKEVER already has (self-hosted vs.
GitHub/Azure-hosted runners) since that materially affects whether UI automation "just works" the
way it does on GitHub-hosted `windows-latest` here.
