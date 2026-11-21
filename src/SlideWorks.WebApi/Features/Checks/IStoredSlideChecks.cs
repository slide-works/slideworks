namespace SlideWorks.WebApi.Features.Checks;

// The checks that need no render: what a slide stores, re-checked against the deck as it is now.
public interface IStoredSlideChecks
{
    Task<IReadOnlyList<SlideFinding>> CheckAsync(string deckId, IReadOnlyList<int> slides, CancellationToken ct);
}
