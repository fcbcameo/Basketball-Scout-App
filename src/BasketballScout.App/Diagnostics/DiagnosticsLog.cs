namespace BasketballScout.App.Diagnostics;

/// <summary>
/// Safety net + flight recorder. Unexpected errors, UI freezes, app lifecycle changes and the
/// live-scoring timeline are appended to a small text file on the device, so a crash or freeze
/// courtside can be diagnosed afterwards — a freeze leaves no crash report of its own. The file
/// never leaves the device unless the scorer shares it (About → Share diagnostic log).
/// Logging is best-effort and never throws: it must not become the thing that breaks scoring.
/// </summary>
public static class DiagnosticsLog
{
    // Roughly the last few games; when exceeded, the oldest half is dropped.
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(FileSystem.AppDataDirectory, "diagnostics.log");

    /// <summary>Hooks the global last-chance exception handlers and starts the freeze watchdog.
    /// Call once, first thing at startup.</summary>
    public static void Install()
    {
        // Logged just before the runtime terminates the app — the one trace a crash leaves us.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Error("Unhandled exception (app terminating)",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        // Fire-and-forget tasks that failed without anyone awaiting them.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        try
        {
            Info($"App started — version {AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString}), " +
                 $"{DeviceInfo.Current.Model}, {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
        }
        catch
        {
            Info("App started"); // device/app info unavailable this early — never block startup on it
        }

        UiStallWatchdog.Start();
    }

    /// <summary>Logs the window's lifecycle, so the log shows whether a problem followed the iPad
    /// locking or the app being backgrounded.</summary>
    public static void TrackLifecycle(Window window)
    {
        window.Activated += (_, _) => Info("App activated");
        window.Deactivated += (_, _) => Info("App deactivated");
        window.Stopped += (_, _) => Info("App went to background");
        window.Resumed += (_, _) => Info("App returned from background");
    }

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string context, Exception ex) => Write("ERROR", $"{context}: {ex}");

    /// <summary>Offers the log through the native share sheet (AirDrop, mail, …).</summary>
    public static async Task ShareAsync()
    {
        Info("Diagnostic log shared");

        // Share a timestamped copy, so the live file isn't held open by the share sheet.
        var copyPath = Path.Combine(FileSystem.CacheDirectory,
            $"BasketballScout-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.txt");
        lock (Gate)
            File.Copy(FilePath, copyPath, overwrite: true);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Basketball Scout diagnostic log",
            File = new ShareFile(copyPath)
        });
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                TrimIfTooLarge();
                File.AppendAllText(FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Best-effort: a full disk or locked file must never take scoring down with it.
        }
    }

    private static void TrimIfTooLarge()
    {
        var info = new FileInfo(FilePath);
        if (!info.Exists || info.Length < MaxBytes) return;

        var text = File.ReadAllText(FilePath);
        var keepFrom = text.IndexOf('\n', text.Length / 2) + 1; // cut on a line boundary
        File.WriteAllText(FilePath, "… older entries trimmed …" + Environment.NewLine + text[keepFrom..]);
    }
}

public static class TaskDiagnosticsExtensions
{
    /// <summary>For fire-and-forget calls (<c>_ = SaveStateAsync()</c>): runs the task without
    /// awaiting it, but logs a failure instead of losing it silently.</summary>
    public static void LogFailures(this Task task, string context)
    {
        _ = ObserveAsync(task, context);

        static async Task ObserveAsync(Task task, string context)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Error(context, ex);
            }
        }
    }
}
