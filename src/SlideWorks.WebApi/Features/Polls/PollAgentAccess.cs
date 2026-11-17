using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Polls;

// What an agent may do to a running poll (D15): open it and close it, nothing else. It needs edit access to the deck, not
// the right to present, because an agent acts for its owner and never presents (IDeckAccess.CanPresentAsync is false for
// it), and a live audience session; both are checked before IPolls is called, so a refused call changes nothing. The
// lifecycle is IPolls', and so are its activity entries, which carry no tool name: open_poll and close_poll log what the
// presenter's buttons log. A phase refusal comes back with its code and a sentence an agent can act on. Per request.
[RegisterPerRequest]
public class PollAgentAccess(IPolls polls, PollReader reader, IDeckAccess access, ICurrentActor actor)
{
    public Task<PollControlView> OpenAsync(string deckId, int slide) => RunAsync(deckId, () => polls.OpenAsync(deckId, slide));

    public Task<PollControlView> CloseAsync(string deckId, int slide) => RunAsync(deckId, () => polls.CloseAsync(deckId, slide));

    private async Task<PollControlView> RunAsync(string deckId, Func<Task<PollControlView>> action)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (await reader.LiveSessionAsync(deckId) == null)
        {
            throw new ConflictException(Explain(PollRefusals.NOT_LIVE));
        }
        try
        {
            return await action();
        }
        catch (ConflictException refused)
        {
            throw new ConflictException(Explain(refused.Message));
        }
    }

    private static string Explain(string code) => code switch
    {
        PollRefusals.NOT_LIVE => $"{code}: the deck is not live; a person starts the audience session with Go live",
        PollRefusals.WRONG_PHASE => $"{code}: the poll is not in a phase that allows this (open needs a poll with no open round, close needs an open round)",
        PollRefusals.ANOTHER_POLL_OPEN => $"{code}: another slide's poll has a round open; close it first",
        _ => code,
    };
}
