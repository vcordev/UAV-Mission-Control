# WPF and MVVM, explained from this project

## What WPF actually is

WPF (Windows Presentation Foundation) is Microsoft's UI framework for native Windows desktop
apps. Unlike a web page, there's no DOM and no browser — a WPF window is a tree of native
`.NET` objects (`Button`, `TextBlock`, `Grid`, ...) that WPF itself renders directly to the
screen using DirectX. Layout and appearance are described in XAML, an XML dialect — see
`src/UavMissionControl.App/MainWindow.xaml` for a real example: it's a `Grid` with six rows, each
row hosting one `UserControl` (a reusable, self-contained chunk of UI).

## What MVVM solves

MVVM (Model-View-ViewModel) is a pattern for keeping UI code testable. Without it, the natural
place to put "what happens when the Connect button is clicked" is directly in the button's
click-handler, in the `.xaml.cs` code-behind file — but code-behind is tightly coupled to the
actual `Window`/`UserControl` object, which makes it painful to unit test (you'd need to spin up
a real window just to test button logic).

MVVM splits this into three pieces:

- **Model** — the actual data/business logic. In this project, that's everything in
  `UavMissionControl.Core` (the state machine, the telemetry simulator) — plain C# classes with
  no UI dependency at all.
- **ViewModel** — a class that exposes the Model in a UI-friendly shape: properties a `View` can
  bind to, and `ICommand`s a `View` can invoke. See
  `src/UavMissionControl.App/ViewModels/ConnectionPanelViewModel.cs`: it exposes a `StatusText`
  property and a `ConnectCommand`, and it's a plain class — you can `new` one up in a unit test
  with no WPF window involved at all (see `ConnectionPanelViewModelTests.cs`).
  - `ViewModelBase` (`ViewModels/ViewModelBase.cs`) implements `INotifyPropertyChanged`, the
    interface WPF's binding engine watches. Calling `OnPropertyChanged(nameof(StatusText))` tells
    any bound `View` "re-read this property, it changed."
  - `RelayCommand` (`ViewModels/RelayCommand.cs`) implements `ICommand`. A `Button`'s
    `Command="{Binding ConnectCommand}"` binding calls `Execute` when clicked and asks
    `CanExecute` to decide whether the button should even be enabled.
- **View** — the XAML + minimal code-behind. See `src/UavMissionControl.App/Views/ConnectionPanelView.xaml`:
  its code-behind (`ConnectionPanelView.xaml.cs`) contains nothing but
  `InitializeComponent();` — all the actual logic lives in the ViewModel it's bound to.

## How binding connects them

`MainWindow.xaml` sets each panel's `DataContext` to a specific ViewModel property:

```xml
<views:ConnectionPanelView DataContext="{Binding ConnectionPanel}" .../>
```

`ConnectionPanel` is a property on `MainViewModel` (the "composition root" — see
`ViewModels/MainViewModel.cs` — the one place that constructs all the ViewModels and wires them
together). Once `DataContext` is set, every `{Binding SomeProperty}` inside that `View` resolves
against that ViewModel's properties automatically — no manual wiring per control.

## A real gotcha this project hit: `Border` has no default automation peer

While building the warning banners (`WarningsBannerView.xaml`), giving a `Border` an `x:Name` and
expecting FlaUI (UI Automation) to find it by that name didn't work. The reason: WPF only
automatically creates an "automation peer" (the thing UI Automation actually queries) for
`Control`-derived elements and a few special cases like `TextBlock`. A plain `Border` — not a
`Control` — gets none by default, so it's invisible to UI Automation entirely, regardless of its
`Visibility`. The fix was to put the `AutomationId` on the `TextBlock` *inside* the `Border`
instead, since `TextBlock` does get a peer. See `docs/test-cases/TC-001-happy-path-mission-lifecycle.md`
for where this was found, and `WarningsBannerView.xaml` for the fix.

## A real gotcha this project hit: `PropertyChanged`/collection mutation and threads

WPF's data-binding infrastructure expects to be touched from the UI thread. Raising
`PropertyChanged` or mutating an `ObservableCollection` from a background thread can throw or
(worse) silently misbehave depending on exactly what's bound to it. This project's `IUiDispatcher`
seam (`Services/IUiDispatcher.cs`, explained in `docs/architecture.md`) exists specifically to
make that marshaling explicit and testable — and `docs/defects/03-eventlog-cross-thread-crash.md`
documents exactly what goes wrong when that marshaling is accidentally skipped, including the
real crash stack trace this project reproduced.

## Building without Visual Studio

This entire project was built via the `dotnet` CLI and VS Code, no Visual Studio. Two real
consequences worth knowing about:

- **No live XAML designer/preview.** Layout was verified by actually running the app
  (`dotnet run --project src/UavMissionControl.App`) and looking at it, not by a design-time
  preview pane.
- **WPF binding errors fail silently at runtime, not at compile time.** A typo in a `Binding` path
  (e.g. `{Binding Baterry}`) doesn't produce a build error — it produces a binding failure written
  to the debug output at runtime, and the bound control just shows nothing/default. This is why
  manual verification (actually running the app and looking at real values, as in
  `docs/test-cases/TC-001-*.md`) matters even with a full automated test suite: tests exercise
  ViewModel logic directly and wouldn't catch a broken *binding path* in the XAML itself.
