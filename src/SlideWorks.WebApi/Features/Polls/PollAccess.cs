using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Polls;

// Who may run a poll and read its counts (V4, D20): a person who can present the deck, SW-4's rule. A deck the caller can't
// view is 404, one they can view but not present is 403. Every check runs before the poll action, so a refused call reaches
// no seam and changes nothing.
[RegisterPerRequest]
public class PollAccess(IDeckAccess access, ICurrentActor actor)
{
    public const string FORBIDDEN = "Only a person who can present the deck can run its poll";

    // The slide is not a poll (ControlAsync answered null). Clients go by the status, not this text.
    public const string NOT_A_POLL = "Poll not found";

    // Runs a poll action once the caller may present the deck.
    public async Task<T> RunAsync<T>(string deckId, Func<Task<T>> action)
    {
        await RequirePresenterAsync(deckId);
        return await action();
    }

    // Runs a read once the caller may present the deck; null (a slide that is not a poll) is 404.
    public async Task<T> ReadAsync<T>(string deckId, Func<Task<T?>> read) where T : class
        => await RunAsync(deckId, read) ?? throw new NotFoundException(NOT_A_POLL);

    private async Task RequirePresenterAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new ForbiddenException(FORBIDDEN);
        }
    }
}
