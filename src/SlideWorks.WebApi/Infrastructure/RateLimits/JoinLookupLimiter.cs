using System.Collections.Concurrent;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Infrastructure.RateLimits;

// The rate limit on guessing join codes (J3, D20). Only misses count: a caller gets MAX_MISSES misses in a fixed one-minute
// window on the app's clock, and once they are used up Allow is false for every lookup until the window ends, a real code
// included. A hit is never counted, but is refused all the same while the caller is over the limit, so the limit can't be
// walked around by interleaving guesses with a known code. Callers are keyed by CallerKey, so everyone behind one NAT shares
// a budget. Per process, like the rest of SlideWorks' in-memory state; windows that have ended are swept at most once a
// minute.
[RegisterSingleton(For = typeof(IJoinLookupLimiter))]
public sealed class JoinLookupLimiter(TimeProvider clock) : IJoinLookupLimiter
{
    public const int MAX_MISSES = 10;

    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    public bool Allow(string callerKey)
        => !_windows.TryGetValue(callerKey, out var window) || clock.GetUtcNow() - window.Start >= WINDOW || window.Misses < MAX_MISSES;

    public void Miss(string callerKey)
    {
        var now = clock.GetUtcNow();
        SweepEndedWindows(now);
        _windows.AddOrUpdate(
            callerKey,
            _ => new Window(now, 1),
            (_, current) => now - current.Start >= WINDOW ? new Window(now, 1) : current with { Misses = current.Misses + 1 });
    }

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

    private sealed record Window(DateTimeOffset Start, int Misses);
}
