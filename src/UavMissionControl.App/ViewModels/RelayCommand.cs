using System.Windows.Input;

namespace UavMissionControl.App.ViewModels;

/// <summary>
/// Hand-rolled ICommand with an explicit <see cref="RaiseCanExecuteChanged"/> rather than
/// relying on WPF's <c>CommandManager.RequerySuggested</c> — that mechanism only re-queries
/// on UI input events, not on our own state-machine-driven changes, so it isn't reliable here.
/// ViewModels call RaiseCanExecuteChanged explicitly after every change that affects a guard.
/// </summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
