namespace SlideWorks.WebApi.Features.Sharing.Views;

// GET and PUT /api/decks/{id}/sharing: the sharing dialog (src/contracts/screens/sharing.json). Level is the wire name
// of DeckSharing ("private", "link" or "named"), and Link is the absolute share link from IPublicUrls.
public sealed record SharingView(string Level, IReadOnlyList<string> Viewers, string Link, SharingLastChange? LastChange);
