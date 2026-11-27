using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;

namespace SlideWorks.WebApi.Features.Render.Print;

// GET /api/render/{id}/print (deck-print.json): the play view of the deck and the last talk's results for the print page.
public record DeckPrintView(
    DeckPlayView Deck,
    IReadOnlyList<PrintPoll> Polls,
    IReadOnlyList<WordCloudTally> WordClouds,
    IReadOnlyList<OpenTextTally> OpenTexts,
    PrintQa? Qa);
