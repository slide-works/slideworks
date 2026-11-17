using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// TODO: replace with the real activity built from the live session, the presenter's slide and the poll's round.
// No slide has an activity yet.
[RegisterPerRequest(For = typeof(IAudienceActivity))]
public sealed class StubAudienceActivity : IAudienceActivity
{
    public Task<PollActivity?> ForDeckAsync(string deckId) => Task.FromResult<PollActivity?>(null);

    public Task<PollActivity?> ForCodeAsync(string code) => Task.FromResult<PollActivity?>(null);
}
