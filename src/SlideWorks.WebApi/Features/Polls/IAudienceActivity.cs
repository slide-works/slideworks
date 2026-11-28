using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// The activity on the slide on screen (V3): the only place a PollActivity is built, from the live session, the presenter's
// slide and the poll's round. Null on a slide with no poll and in every phase but live. Per request, because it uses the
// database. Language is the guest's pick (SW-883 K5): the activity is in it when the deck offers it, and in the primary
// language otherwise, null included.
public interface IAudienceActivity
{
    Task<PollActivity?> ForDeckAsync(string deckId, string? language);

    Task<PollActivity?> ForCodeAsync(string code, string? language);
}
