using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Qa;

// How often one guest may ask (Q4, D20): PER_MINUTE questions in any one minute on the app's clock, keyed by the guest's
// sw_guest id. A sliding window, so the wait it gives is exact: the time until the oldest of the guest's last PER_MINUTE asks
// is a minute old, in whole seconds rounded up. An ask is taken before the checks that come after the limit (the text, the
// name, the open round) and given back when one of them refuses it, so a refused ask never uses up the limit. Per process,
// like the rest of SlideWorks' in-memory state; guests whose asks are all older than a minute are swept at most once a minute.
[RegisterSingleton]
public sealed class AskLimiter(TimeProvider clock)
{
    public const int PER_MINUTE = 3;

    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _asks = new(StringComparer.Ordinal);
    private DateTimeOffset _nextSweep = DateTimeOffset.MinValue;

    // Takes one of the guest's asks and returns when it was taken, to give back with GiveBack. Null when the guest has asked
    // PER_MINUTE times in the last minute; retryAfter is then the whole seconds until it can ask again, at least 1.
    public DateTimeOffset? TryTake(string guestId, out int retryAfter)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            SweepEnded(now);
            if (!_asks.TryGetValue(guestId, out var asks))
            {
                asks = [];
                _asks[guestId] = asks;
            }
            asks.RemoveAll(at => now - at >= WINDOW);
            if (asks.Count >= PER_MINUTE)
            {
                retryAfter = Math.Max(1, (int)Math.Ceiling((asks[0] + WINDOW - now).TotalSeconds));
                return null;
            }
            asks.Add(now);
            retryAfter = 0;
            return now;
        }
    }

    // Gives back an ask TryTake took, because the ask was refused after it.
    public void GiveBack(string guestId, DateTimeOffset taken)
    {
        lock (_gate)
        {
            if (_asks.TryGetValue(guestId, out var asks))
            {
                asks.Remove(taken);
            }
        }
    }

    private void SweepEnded(DateTimeOffset now)
    {
        if (now < _nextSweep)
        {
            return;
        }
        _nextSweep = now + WINDOW;
        foreach (var guest in _asks.Where(entry => entry.Value.TrueForAll(at => now - at >= WINDOW)).Select(entry => entry.Key).ToList())
        {
            _asks.Remove(guest);
        }
    }
}
