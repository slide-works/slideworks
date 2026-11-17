using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Polls;

// SW-4's registry tells this about every accepted change to a presentation (P10); it hands the slide in the state to the
// arrival rule (V4). IPollArrival decides from the deck's live session whether there is anything to do, so a deck that is
// not live costs one read. It is a singleton because it is an observer, so the per-request seam is reached through a scope
// per call, as AudienceFeed does. The registry awaits observers inline: this never throws into it. A failure the arrival
// rule does not expect is logged here as an error and dropped, so the presenter's move still lands; the next state for the
// deck tries again.
[RegisterSingleton(For = typeof(IPresentationObserver), OfCollection = true)]
public sealed class PollArrivalObserver(IServiceScopeFactory scopes, ILogger<PollArrivalObserver> logger) : IPresentationObserver
{
    public async Task OnChangedAsync(PresentationState state)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPollArrival>().ArrivedAsync(state.DeckId, state.Slide);
        }
        catch (Exception e)
        {
            logger.LogError(e, "The poll arrival rule failed for slide {Slide} of deck {DeckId}", state.Slide, state.DeckId);
        }
    }
}
