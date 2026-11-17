using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Polls;

// SW-4's registry tells this about every accepted change to a presentation (P10); it runs the arrival rule (V4) for the
// presenter's position. The registry notifies after it releases its per-deck lock, and changes come from several
// connections at once (presenter window, phone remote, HTTP), so states can land here out of order. The state passed in is
// therefore only a signal for the deck, as it is for AudienceFeed: under a per-deck gate held here, each turn reads the
// position the registry holds now (IPresentations.GetAsync) and hands that slide to IPollArrival, so arrivals for one deck
// run one at a time and each acts on the position as it is when its turn comes. The last notification after the last
// move always reads that move, so the rule converges on the presenter's actual slide. No presentation (expired, or dropped
// by a restart) does nothing.
//
// Lock order: arrival gate, then the feed's gate (IPolls calls IAudienceRelay.RefreshAsync) and the publisher's gate, then
// the registry's per-deck lock (GetAsync, also under the feed's compose). Nothing may take the arrival gate while holding the
// feed's gate, the publisher's gate or the registry's lock: only the registry's observer loop takes it, outside its lock,
// and IPolls, the feed and the publisher never notify presentation observers. Keep it that way, or two decks' moves can
// deadlock.
//
// Order among observers: the registry calls this and AudienceRelayObserver in the container's registration order, which
// nothing pins. Either order is correct: every IPolls phase change refreshes the audience state, so guests converge.
//
// A singleton because it is an observer, so the per-request seam and the registry (which takes the observers in its own
// constructor) are resolved through a scope per call. The registry awaits observers inline: this never throws into it. A
// failure the arrival rule does not expect is logged here as an error and dropped, so the presenter's move still lands; the
// next state for the deck tries again.
[RegisterSingleton(For = typeof(IPresentationObserver), OfCollection = true)]
public sealed class PollArrivalObserver(IServiceScopeFactory scopes, ILogger<PollArrivalObserver> logger) : IPresentationObserver
{
    private readonly Lock _gatesLock = new();

    // A deck's gate and how many turns hold or wait on it; the entry goes when the last one leaves, so gates don't pile up.
    private readonly Dictionary<string, DeckGate> _gates = new(StringComparer.Ordinal);

    public async Task OnChangedAsync(PresentationState state)
    {
        var gate = Enter(state.DeckId);
        try
        {
            await gate.Semaphore.WaitAsync();
            try
            {
                await ArriveAsync(state.DeckId);
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "The poll arrival rule failed for deck {DeckId}", state.DeckId);
        }
        finally
        {
            Leave(state.DeckId, gate);
        }
    }

    private async Task ArriveAsync(string deckId)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (await scope.ServiceProvider.GetRequiredService<IPresentations>().GetAsync(deckId) is not { } current)
        {
            return;
        }
        await scope.ServiceProvider.GetRequiredService<IPollArrival>().ArrivedAsync(deckId, current.Slide);
    }

    private DeckGate Enter(string deckId)
    {
        lock (_gatesLock)
        {
            if (!_gates.TryGetValue(deckId, out var gate))
            {
                gate = new DeckGate();
                _gates[deckId] = gate;
            }
            gate.Users++;
            return gate;
        }
    }

    private void Leave(string deckId, DeckGate gate)
    {
        lock (_gatesLock)
        {
            gate.Users--;
            if (gate.Users == 0)
            {
                _gates.Remove(deckId);
                gate.Semaphore.Dispose();
            }
        }
    }

    private sealed class DeckGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Users { get; set; }
    }
}
