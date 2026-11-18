using System.Collections.Concurrent;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Qa;

// How often one guest may change its upvotes (Q4, D20): PER_MINUTE vote changes in a fixed one-minute window on the app's
// clock, keyed by the guest's sw_guest id, whatever the change turns out to be. SW-9's own limiter, separate from SW-6's
// AnswerLimiter, so upvoting never uses up a guest's poll answers. Per process, like the rest of SlideWorks' in-memory
// state. Windows that have ended are swept at most once a window, so a vote never scans every guest.
[RegisterSingleton]
public sealed class VoteLimiter(TimeProvider clock)
{
    public const int PER_MINUTE = 30;

    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    // True when the guest may change its votes now, and counts the change.
    public bool TryAcquire(string guestId)
    {
        var now = clock.GetUtcNow();
        SweepEndedWindows(now);
        var window = _windows.AddOrUpdate(
            guestId,
            _ => new Window(now, 1),
            (_, current) => now - current.Start >= WINDOW ? new Window(now, 1) : current with { Count = current.Count + 1 });
        return window.Count <= PER_MINUTE;
    }

    // One call per window wins the sweep; the others go straight on. TryRemove removes only the exact entry it read, so a
    // window updated during the sweep is kept.
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
