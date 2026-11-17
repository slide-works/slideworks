using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// TODO: returns a sample until the real service lands: it moves the actor's decks to the template's latest version.
// Moves nothing and answers 0.
[RegisterPerRequest(For = typeof(IUpdateMyDecks))]
public sealed class StubUpdateMyDecks : IUpdateMyDecks
{
    public Task<int> RunAsync(ICurrentActor actor, string templateId, CancellationToken ct) => Task.FromResult(0);
}
