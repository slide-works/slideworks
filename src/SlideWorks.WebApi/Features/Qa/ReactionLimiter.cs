using System.Collections.Concurrent;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Qa;

// How often one guest may tap a reaction (Q7, D20): PER_WINDOW taps in any WINDOW, a sliding window on the app's clock, keyed
// by the guest's sw_guest id. A refused tap is not counted, so a guest who keeps tapping gets a tap back as each old one
// leaves the window. Per process, like the rest of SlideWorks' in-memory state. Guests with no tap inside the window are
// swept at most once a window, so a tap never scans every guest.
[RegisterSingleton]
public sealed class ReactionLimiter(TimeProvider clock)
{
    public const int PER_WINDOW = 10;

    public static readonly TimeSpan WINDOW = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, Taps> _guests = new(StringComparer.Ordinal);
    private long _nextSweep = long.MinValue;

    // True when the guest may tap now, and counts the tap.
    public bool TryAcquire(string guestId)
    {
        var now = clock.GetTimestamp();
        SweepIdleGuests(now);
        while (true)
        {
            var taps = _guests.GetOrAdd(guestId, _ => new Taps());
            lock (taps)
            {
                // A sweep removed this entry after it was read: take the one in the dictionary now.
                if (taps.Swept)
                {
                    continue;
                }
                taps.DropOlderThanTheWindow(clock, now);
                if (taps.Count >= PER_WINDOW)
                {
                    return false;
                }
                taps.Add(now);
                return true;
            }
        }
    }

    // One call per window wins the sweep; the others go straight on. An entry is marked swept under its own lock before it is
    // removed, so a tap that already holds it takes a fresh one instead of counting into a removed entry.
    private void SweepIdleGuests(long now)
    {
        var due = Interlocked.Read(ref _nextSweep);
        var next = now + (long)(WINDOW.TotalSeconds * clock.TimestampFrequency);
        if (now < due || Interlocked.CompareExchange(ref _nextSweep, next, due) != due)
        {
            return;
        }
        foreach (var entry in _guests)
        {
            lock (entry.Value)
            {
                entry.Value.DropOlderThanTheWindow(clock, now);
                if (entry.Value.Count == 0)
                {
                    entry.Value.Swept = true;
                    _guests.TryRemove(entry);
                }
            }
        }
    }

    // One guest's taps inside the window, oldest first, as TimeProvider timestamps.
    private sealed class Taps
    {
        private readonly Queue<long> _times = new();

        public bool Swept { get; set; }

        public int Count => _times.Count;

        public void Add(long now) => _times.Enqueue(now);

        public void DropOlderThanTheWindow(TimeProvider clock, long now)
        {
            while (_times.Count > 0 && clock.GetElapsedTime(_times.Peek(), now) >= WINDOW)
            {
                _times.Dequeue();
            }
        }
    }
}
