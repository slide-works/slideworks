using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Templates.Views;

// GET and PUT /api/templates/{id}/sharing (src/contracts/screens/template-sharing.json). Link is the absolute address
// of the template's page, {base}/t/{id}.
public sealed record TemplateSharingView(DeckSharing Level, IReadOnlyList<string> Viewers, string Link);
