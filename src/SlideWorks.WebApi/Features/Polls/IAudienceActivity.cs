using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// The activity on the slide on screen (V3): the only place a PollActivity is built, from the live session, the presenter's
// slide and the poll's round. Null on a slide with no poll and in every phase but live. Per request, because it uses the
// database.
public interface IAudienceActivity
{
    Task<PollActivity?> ForDeckAsync(string deckId);

    Task<PollActivity?> ForCodeAsync(string code);
}
