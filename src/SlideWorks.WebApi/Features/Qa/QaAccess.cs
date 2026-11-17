using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Qa;

// Who may moderate a deck's questions and change its Q&A settings (Q6, D16, D17): a person who can present the deck, as
// PollAccess decides for a poll. A deck the caller can't view is 404, one they can view but not present is 403. Every check
// runs before the action, so a refused call reaches no seam and changes nothing.
[RegisterPerRequest]
public class QaAccess(IDeckAccess access, ICurrentActor actor, IQuestionModeration moderation)
{
    public const string FORBIDDEN = "Only a person who can present the deck can run its Q&A";

    // Runs a Q&A action once the caller may present the deck.
    public async Task<T> RunAsync<T>(string deckId, Func<Task<T>> action)
    {
        await RequirePresenterAsync(deckId);
        return await action();
    }

    // Runs a settings change once the caller may present the deck, then answers the presenter's view after it: IQaSettings
    // never builds a QaControlView, because moderation reads its settings there.
    public async Task<QaControlView> ChangeAsync<T>(string deckId, Func<Task<T>> change)
    {
        await RequirePresenterAsync(deckId);
        await change();
        return await moderation.ControlAsync(deckId);
    }

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
