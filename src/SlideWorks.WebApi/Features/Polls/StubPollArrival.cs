using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Polls;

// TODO: replace with the real arrival rule that closes a round left behind and opens round 1 on an "onArrival" poll.
// Reaching a slide does nothing to any poll yet.
[RegisterPerRequest(For = typeof(IPollArrival))]
public sealed class StubPollArrival : IPollArrival
{
    public Task ArrivedAsync(string deckId, int slide) => Task.CompletedTask;
}
