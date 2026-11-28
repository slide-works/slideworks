using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// The one publisher of ReadingIn (SW-886 K11): the first change on a deck goes at once, then at most one event per deck per
// second, always with the latest counts.
[RegisterSingleton(For = typeof(IReadingIn))]
public sealed class ReadingInPump : IReadingIn
{
    // TODO: mark the deck and publish its latest counts through the audience bus, at most once a second per deck.
    public void Changed(string deckId)
    {
    }

    // TODO: read the counts from IAudienceRoster.ReadingAsync and order them by the deck's translation settings.
    public Task<IReadOnlyList<ReadingInCount>?> CurrentAsync(string deckId) => Task.FromResult<IReadOnlyList<ReadingInCount>?>(null);
}
