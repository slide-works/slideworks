using System.Threading.Channels;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Support.Timing;

// The marker-event pattern: the way to assert that no event, or exactly N events, were sent for a deck. Subscribe before
// the act; after it, BeforeMarkerAsync publishes a marker DeckChanged through the same IDeckEvents and returns every
// event read before the marker came through. Delivery keeps publish order per subscriber, so everything the finished
// act published is in the list. The assertion stays in the test, against the list. Never TryRead with a timeout: a
// late event would pass it. Records compare by value, so the marker is found by reference.
public sealed class DeckEventsUntilMarker : IDisposable
{
    private readonly IDeckEvents _events;
    private readonly string _deckId;
    private readonly Channel<DeckEvent> _received = Channel.CreateUnbounded<DeckEvent>();
    private readonly IDisposable _subscription;

    private DeckEventsUntilMarker(IDeckEvents events, string deckId)
    {
        _events = events;
        _deckId = deckId;
        _subscription = events.Subscribe(deckId, e => _received.Writer.WriteAsync(e).AsTask());
    }

    public static DeckEventsUntilMarker Subscribe(IDeckEvents events, string deckId) => new(events, deckId);

    // Every event received before the marker; each read waits at most 10 seconds.
    public async Task<IReadOnlyList<DeckEvent>> BeforeMarkerAsync()
    {
        var marker = new DeckChanged(_deckId);
        await _events.PublishAsync(marker);
        var before = new List<DeckEvent>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var next = await _received.Reader.ReadAsync(timeout.Token); !ReferenceEquals(next, marker); next = await _received.Reader.ReadAsync(timeout.Token))
        {
            before.Add(next);
        }
        return before;
    }

    public void Dispose() => _subscription.Dispose();
}
