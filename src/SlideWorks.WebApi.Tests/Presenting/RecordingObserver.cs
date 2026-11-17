using System.Collections.Concurrent;
using System.Threading.Channels;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Tests.Presenting;

// A P10 observer a fixture registers with ConfigureTestServices: it keeps every state it is told about, per deck.
public sealed class RecordingObserver : IPresentationObserver
{
    private readonly ConcurrentDictionary<string, Channel<PresentationState>> _states = new(StringComparer.Ordinal);

    public Task OnChangedAsync(PresentationState state)
    {
        For(state.DeckId).Writer.TryWrite(state);
        return Task.CompletedTask;
    }

    public async Task<PresentationState> NextAsync(string deckId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await For(deckId).Reader.ReadAsync(timeout.Token);
    }

    // The states told for the deck and not yet read, without waiting. Read it only after a later state with a known seq
    // has been read: observers are called in order, so everything told before that one is here.
    public IReadOnlyList<PresentationState> Received(string deckId)
    {
        var received = new List<PresentationState>();
        while (For(deckId).Reader.TryRead(out var state))
        {
            received.Add(state);
        }
        return received;
    }

    private Channel<PresentationState> For(string deckId)
        => _states.GetOrAdd(deckId, _ => Channel.CreateUnbounded<PresentationState>());
}
