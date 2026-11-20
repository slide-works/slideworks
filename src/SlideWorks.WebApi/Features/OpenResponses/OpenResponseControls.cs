using Agoda.IoC.Core;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The presenter's control view of a word-cloud or open-text slide and its open, close and new-round actions
// (open-response-control.json). A deck the caller can't see is 404 and one they can't present is 403, before anything is
// written.
[RegisterPerRequest]
public class OpenResponseControls(IDeckAccess access, ICurrentActor actor)
{
    private static readonly OpenResponseControlView WORD_CLOUD = new(
        OpenResponseKind.WordCloud,
        OpenResponsePhase.Open,
        "6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63",
        1,
        10,
        OpenResponseResults.Live,
        OpenResponseOpens.Arrival,
        true,
        12,
        [new ControlEntry(1, "Focused", 5, false), new ControlEntry(2, "Busy", 4, false), new ControlEntry(3, "Calm", 2, false), new ControlEntry(4, "Rubbish", 1, true)]);

    public async Task<OpenResponseControlView> ViewAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        // TODO: read the slide's round in the live session and its entries from the rows.
        return WORD_CLOUD;
    }

    // TODO: open the slide's round in the live session, or return it when it is already open.
    public Task<OpenResponseControlView> OpenAsync(string deckId, int slide) => ViewAsync(deckId, slide);

    public async Task<OpenResponseControlView> CloseAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        // TODO: close the slide's open round, keeping its entries.
        return WORD_CLOUD with { Phase = OpenResponsePhase.Closed, Revision = WORD_CLOUD.Revision + 1 };
    }

    public async Task<OpenResponseControlView> NewRoundAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        // TODO: close the slide's round and open the next number, with no entries.
        return WORD_CLOUD with { Number = WORD_CLOUD.Number + 1, Revision = 0, Guests = 0, Entries = [] };
    }

    private async Task RequirePresenterAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new ForbiddenException("Only someone who can present the deck can run its open responses");
        }
    }
}
