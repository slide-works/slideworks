using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Checks;

[RegisterPerRequest(For = typeof(IStoredSlideChecks))]
public sealed class StoredSlideChecks : IStoredSlideChecks
{
    // TODO: re-check stored slides against the current template, flag layouts outside it and generic alt text
    public Task<IReadOnlyList<SlideFinding>> CheckAsync(string deckId, IReadOnlyList<int> slides, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SlideFinding>>([]);
}
