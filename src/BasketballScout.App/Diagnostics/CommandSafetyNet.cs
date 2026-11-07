using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.Diagnostics;

/// <summary>
/// Safety net for a view model's async commands. By default CommunityToolkit rethrows a failed
/// async command's exception on the UI thread, which on iOS closes the app — mid-game. Commands
/// declared with <c>[RelayCommand(FlowExceptionsToTaskScheduler = true)]</c> don't rethrow; this
/// watches every async command on the view model so a failure is still logged and reported via
/// <c>onFailed</c>. It also leaves a breadcrumb per command and flags commands that run long or
/// never finish, since a stuck command makes its buttons stop responding (a "freeze").
/// </summary>
public static class CommandSafetyNet
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromSeconds(10);

    public static void Watch(object viewModel, Action<string> onFailed)
    {
        foreach (var property in viewModel.GetType().GetProperties())
        {
            if (!typeof(IAsyncRelayCommand).IsAssignableFrom(property.PropertyType)) continue;
            if (property.GetValue(viewModel) is not IAsyncRelayCommand command) continue;

            string name = property.Name.EndsWith("Command")
                ? property.Name[..^"Command".Length] : property.Name;

            // ExecutionTask is announced when a run starts and again when it completes — observe
            // each run once.
            Task? observed = null;
            command.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(IAsyncRelayCommand.ExecutionTask)) return;
                if (command.ExecutionTask is not { } task || ReferenceEquals(task, observed)) return;
                observed = task;
                _ = ObserveAsync(task, name, onFailed);
            };
        }
    }

    private static async Task ObserveAsync(Task task, string name, Action<string> onFailed)
    {
        DiagnosticsLog.Info($"> {name}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (await Task.WhenAny(task, Task.Delay(StuckThreshold)) != task)
                DiagnosticsLog.Warn($"{name} still running after {StuckThreshold.TotalSeconds:0}s");
            await task;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Error(name, ex);
            onFailed(name);
            return;
        }

        if (stopwatch.Elapsed >= SlowThreshold)
            DiagnosticsLog.Warn($"{name} took {stopwatch.Elapsed.TotalSeconds:0.0}s");
    }
}
