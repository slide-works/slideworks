using System.Collections.Concurrent;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Infrastructure.RateLimits;

// The rate limit on guessing join codes (J3, D20). Only misses count: a caller gets MAX_MISSES misses in a fixed one-minute
// window on the app's clock, and once they are used up Allow is false for every lookup until the window ends, a real code
// included. Allow reserves a slot before the lookup and a hit refunds it, so parallel lookups can't beat the limit and a
// completed hit is never counted; a hit is refused all the same while the caller is over the limit, so the limit can't be
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

    // Reserves a slot: the attempt counts now, before the lookup, so parallel lookups can't all pass before any miss is
    // counted. A hit gives the slot back; a miss keeps it; a lookup that fails with neither keeps it until the window ends.
    public bool Allow(string callerKey)
    {
        var now = clock.GetUtcNow();
        SweepEndedWindows(now);
        while (true)
        {
            if (!_windows.TryGetValue(callerKey, out var current))
            {
                if (_windows.TryAdd(callerKey, new Window(now, 1)))
                {
                    return true;
                }
                continue;
            }
            if (now - current.Start >= WINDOW)
            {
                if (_windows.TryUpdate(callerKey, new Window(now, 1), current))
                {
                    return true;
                }
                continue;
            }
            if (current.Misses >= MAX_MISSES)
            {
                return false;
            }
            if (_windows.TryUpdate(callerKey, current with { Misses = current.Misses + 1 }, current))
            {
                return true;
            }
        }
    }

    // The slot Allow reserved stays counted.
    public void Miss(string callerKey)
    {
    }

    // Gives the reserved slot back. A window left with nothing reserved is dropped. A hit that lands after its window ended
    // can give back a slot of the next window, so the budget can be one more than 10 at a window edge at most.
    public void Hit(string callerKey)
    {
        while (_windows.TryGetValue(callerKey, out var current))
        {
            var done = current.Misses <= 1
                ? _windows.TryRemove(new KeyValuePair<string, Window>(callerKey, current))
                : _windows.TryUpdate(callerKey, current with { Misses = current.Misses - 1 }, current);
            if (done)
            {
                return;
            }
        }
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
