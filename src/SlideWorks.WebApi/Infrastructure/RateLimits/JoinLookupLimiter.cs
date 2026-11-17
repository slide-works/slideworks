using System.Collections.Concurrent;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Infrastructure.RateLimits;

// The rate limit on guessing join codes (J3, D20). Only misses count: a caller gets MAX_MISSES misses in a fixed one-minute
// window on the app's clock, and once they are used up Allow is false for every lookup until the window ends, a real code
// included. A hit is never counted, but is refused all the same while the caller is over the limit, so the limit can't be
// walked around by interleaving guesses with a known code. Lookups still in flight are tracked apart from misses: Allow
// registers one, and Hit or Miss settles it. A caller can have at most MAX_IN_FLIGHT lookups unsettled, so a parallel guesser
// gets at most MAX_MISSES + MAX_IN_FLIGHT - 1 misses answered per window, while a room of real guests (a synchronised
// re-read, a mass rejoin) is not refused below MAX_IN_FLIGHT. Callers are keyed by CallerKey, so everyone behind one NAT
// shares a budget. Per process, like the rest of SlideWorks' in-memory state; windows that have ended are swept at most once
// a minute, and a settle that arrives after its window ended is ignored, so no slot outlives its window.
[RegisterSingleton(For = typeof(IJoinLookupLimiter))]
public sealed class JoinLookupLimiter(TimeProvider clock) : IJoinLookupLimiter
{
    public const int MAX_MISSES = 10;

    public const int MAX_IN_FLIGHT = 100;

    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    // Registers a lookup in flight. False once the window's misses are used up or too many lookups are unsettled.
    public bool Allow(string callerKey)
    {
        var now = clock.GetUtcNow();
        SweepEndedWindows(now);
        while (true)
        {
            if (!_windows.TryGetValue(callerKey, out var current))
            {
                if (_windows.TryAdd(callerKey, new Window(now, 0, 1)))
                {
                    return true;
                }
                continue;
            }
            if (now - current.Start >= WINDOW)
            {
                if (_windows.TryUpdate(callerKey, new Window(now, 0, 1), current))
                {
                    return true;
                }
                continue;
            }
            if (current.Misses >= MAX_MISSES || current.InFlight >= MAX_IN_FLIGHT)
            {
                return false;
            }
            if (_windows.TryUpdate(callerKey, current with { InFlight = current.InFlight + 1 }, current))
            {
                return true;
            }
        }
    }

    // The lookup found no session: it is settled and counts as a miss.
    public void Miss(string callerKey) => Settle(callerKey, misses: 1);

    // The lookup found a session: it is settled and counts for nothing.
    public void Hit(string callerKey) => Settle(callerKey, misses: 0);

    private void Settle(string callerKey, int misses)
    {
        var now = clock.GetUtcNow();
        while (_windows.TryGetValue(callerKey, out var current) && now - current.Start < WINDOW)
        {
            var next = current with { Misses = current.Misses + misses, InFlight = Math.Max(0, current.InFlight - 1) };
            var done = next.Misses == 0 && next.InFlight == 0
                ? _windows.TryRemove(new KeyValuePair<string, Window>(callerKey, current))
                : _windows.TryUpdate(callerKey, next, current);
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

    private sealed record Window(DateTimeOffset Start, int Misses, int InFlight);
}
