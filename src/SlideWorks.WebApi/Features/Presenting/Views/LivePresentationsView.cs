namespace SlideWorks.WebApi.Features.Presenting.Views;

// GET /api/presentations (P4): the signed-in person's decks with at least one presenter or audience connection, newest
// first. The sample is src/contracts/screens/presentations.json.
public sealed record LivePresentationsView(IReadOnlyList<LivePresentation> Presentations);
