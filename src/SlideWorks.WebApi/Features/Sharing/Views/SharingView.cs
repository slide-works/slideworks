using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// GET and PUT /api/decks/{id}/sharing: the sharing dialog (src/contracts/screens/sharing.json). Level goes on the wire as
// "private", "link" or "named" (every enum is camelCase on the wire), and Link is the absolute share link from
// IPublicUrls.
public sealed record SharingView(DeckSharing Level, IReadOnlyList<string> Viewers, string Link, SharingLastChange? LastChange);
