namespace BasketballScout.App.Diagnostics;

/// <summary>
/// Freeze detector. A frozen app leaves no crash report, so a background thread pings the UI
/// thread once a second and logs when a ping stays unanswered for <see cref="StallThresholdMs"/>,
/// then again when the UI recovers, with how long it was stuck. If it never recovers (the scorer
/// force-quits), the log simply ends on the "unresponsive" line — timestamped.
/// </summary>
internal static class UiStallWatchdog
{
    private const int PingIntervalMs = 1000;
    private const int StallThresholdMs = 3000;

    private static int _started;
    private static long _pingSentAt;    // Environment.TickCount64 when the pending ping was posted
    private static long _pingAnsweredAt;
    private static int _pingPending;    // 1 until the UI thread has run the ping

    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        new Thread(Run) { IsBackground = true, Name = "UiStallWatchdog" }.Start();
    }

    private static void Run()
    {
        long lastLoop = Environment.TickCount64;
        bool reported = false;

        while (true)
        {
            Thread.Sleep(PingIntervalMs);
            long now = Environment.TickCount64;

            // iOS suspends the whole app while it's in the background (iPad locked, app switcher),
            // this thread included. A gap far beyond our own sleep means we were suspended, not
            // frozen — start over instead of reporting the suspension as a freeze.
            bool wasSuspended = now - lastLoop > PingIntervalMs * 3;
            lastLoop = now;
            if (wasSuspended)
            {
                reported = false;
                Volatile.Write(ref _pingPending, 0);
            }

            if (Volatile.Read(ref _pingPending) == 1)
            {
                long stuckMs = now - Volatile.Read(ref _pingSentAt);
                if (!reported && stuckMs >= StallThresholdMs)
                {
                    reported = true;
                    DiagnosticsLog.Warn($"UI thread unresponsive for {stuckMs / 1000.0:0.0}s");
                }
                continue; // one ping at a time
            }

            if (reported)
            {
                reported = false;
                long stuckMs = Volatile.Read(ref _pingAnsweredAt) - Volatile.Read(ref _pingSentAt);
                DiagnosticsLog.Warn($"UI thread responsive again after {stuckMs / 1000.0:0.0}s");
            }

            Volatile.Write(ref _pingSentAt, now);
            Volatile.Write(ref _pingPending, 1);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Volatile.Write(ref _pingAnsweredAt, Environment.TickCount64);
                Volatile.Write(ref _pingPending, 0);
            });
        }
    }
}
