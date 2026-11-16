using System.Collections.Concurrent;
using System.Net;

namespace SlideWorks.WebApi.Infrastructure.RateLimits;

// A per-caller budget for an anonymous endpoint: each caller (CallerKey, from the remote address) gets perMinute calls
// in a fixed one-minute window on the app's clock. Per process, like the rest of SlideWorks' in-memory state. Windows
// that have ended are swept at most once a window, so a request never scans every caller.
public abstract class FixedWindowLimiter(TimeProvider clock, int perMinute)
{
    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    // True when the caller may call now, and counts the attempt. When refused, retryAfter is how long until the caller's
    // window ends.
    public bool TryAcquire(IPAddress? caller, out TimeSpan retryAfter)
    {
        var now = clock.GetUtcNow();
        SweepEndedWindows(now);
        var window = _windows.AddOrUpdate(
            CallerKey.For(caller),
            _ => new Window(now, 1),
            (_, current) => now - current.Start >= WINDOW ? new Window(now, 1) : current with { Count = current.Count + 1 });
        retryAfter = window.Count <= perMinute ? TimeSpan.Zero : window.Start + WINDOW - now;
        return retryAfter == TimeSpan.Zero;
    }

    // One request per window wins the sweep; the others go straight on. A window updated during the sweep is kept,
    // because TryRemove only removes the exact entry that was read.
    private void SweepEndedWindows(DateTimeOffset now)
    {
        var due = Interlocked.Read(ref _nextSweepTicks);
        if (now.UtcTicks < due || Interlocked.CompareExchange(ref _nextSweepTicks, now.UtcTicks + WINDOW.Ticks, due) != due)
        {
            return;
        }
        foreach (var entry in _windows)
        {
            if (now - entry.Value.Start >= WINDOW)
            {
                _windows.TryRemove(entry);
            }
        }
    }

    private sealed record Window(DateTimeOffset Start, int Count);
}
