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
// a minute (one with lookups in flight after two).
//
// A lookup can straddle a window boundary: Allow in one window, Hit or Miss after the next began. So a new window inherits the
// old one's in-flight count, and a settle always decrements what some Allow registered, whichever window it lands in. A window
// with lookups in flight is never dropped, and its own misses can't be lost. An inherited count that is still unsettled after
// a whole further window (a lookup that threw and settled with neither) is dropped at the next boundary, so a lost lookup
// can't hold a slot for good. A straddling miss counts in the window it settles in, which is conservative.
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
                if (_windows.TryAdd(callerKey, new Window(now, 0, 1, 0)))
                {
                    return true;
                }
                continue;
            }
            if (now - current.Start >= WINDOW)
            {
                // Lookups still in flight carry over, except those already carried over once and still unsettled.
                var carried = Math.Min(Math.Max(0, current.InFlight - current.Carried), MAX_IN_FLIGHT - 1);
                if (_windows.TryUpdate(callerKey, new Window(now, 0, carried + 1, carried), current))
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

    // True once the caller's misses in the current window are used up, as Allow decides it; registers nothing in flight.
    public bool OverMissLimit(string callerKey)
    {
        var now = clock.GetUtcNow();
        SweepEndedWindows(now);
        return _windows.TryGetValue(callerKey, out var current) && now - current.Start < WINDOW && current.Misses >= MAX_MISSES;
    }

    // Counts a miss with no lookup in flight to settle. A window that has ended starts a new one, carrying lookups still in
    // flight as Allow does, but adding none.
    public void RecordMiss(string callerKey)
    {
        var now = clock.GetUtcNow();
        while (true)
        {
            if (!_windows.TryGetValue(callerKey, out var current))
            {
                if (_windows.TryAdd(callerKey, new Window(now, 1, 0, 0)))
                {
                    return;
                }
                continue;
            }
            var next = now - current.Start >= WINDOW
                ? CarriedInto(now, current) with { Misses = 1 }
                : current with { Misses = current.Misses + 1 };
            if (_windows.TryUpdate(callerKey, next, current))
            {
                return;
            }
        }
    }

    // A new window from one that has ended, with its lookups still in flight carried over, except those already carried once.
    private static Window CarriedInto(DateTimeOffset now, Window ended)
    {
        var carried = Math.Min(Math.Max(0, ended.InFlight - ended.Carried), MAX_IN_FLIGHT);
        return new Window(now, 0, carried, carried);
    }

    private void Settle(string callerKey, int misses)
    {
        while (_windows.TryGetValue(callerKey, out var current))
        {
            // A settle can't say which window registered it. The total stays exact; the carried part is only used to age out
            // a lost lookup, so a settle is charged to the window's own lookups first and to the carried ones when none remain.
            var next = current with
            {
                Misses = current.Misses + misses,
                InFlight = Math.Max(0, current.InFlight - 1),
                Carried = current.InFlight > current.Carried ? current.Carried : Math.Max(0, current.Carried - 1),
            };
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
            // A window with lookups in flight stays for one more window, so the next Allow can carry them over.
            var age = now - entry.Value.Start;
            if (age >= WINDOW * 2 || (age >= WINDOW && entry.Value.InFlight == 0))
            {
                _windows.TryRemove(entry);
            }
        }
    }

    // Carried is the part of InFlight inherited from the previous window.
    private sealed record Window(DateTimeOffset Start, int Misses, int InFlight, int Carried);
}
