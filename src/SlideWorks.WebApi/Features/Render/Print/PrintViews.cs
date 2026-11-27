using Agoda.IoC.Core;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render.Print;

// What the print page reads (P1): the deck and the last talk's results. PrintAsync checks the Print token; ViewAsync is
// the whole view without one, for the PDF cache, and is the same for unchanged data. The results come only from the
// three seams, one after another, and only when the deck has a session.
[RegisterPerRequest]
public class PrintViews(
    DeckReader reader,
    IRenderTokens tokens,
    PrintSessions sessions,
    IPrintPollResults polls,
    IPrintOpenResponseResults openResponses,
    IPrintQaResults qa)
{
    public Task<DeckPrintView> PrintAsync(string deckId, string? token, CancellationToken ct)
    {
        if (!tokens.Validate(token, deckId, RenderPurpose.Print))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }

        return ViewAsync(deckId, ct);
    }

    public async Task<DeckPrintView> ViewAsync(string deckId, CancellationToken ct)
    {
        var deck = (await reader.ReadForRenderAsync(deckId)).Play;
        if (await sessions.NewestAsync(deckId, ct) is not { } session)
        {
            return new DeckPrintView(deck, [], [], [], null);
        }

        var pollResults = await polls.ReadAsync(deckId, session, ct);
        var open = await openResponses.ReadAsync(deckId, session, ct);
        var questions = await qa.ReadAsync(deckId, session, ct);
        return new DeckPrintView(deck, pollResults, open.WordClouds, open.OpenTexts, questions);
    }
}
