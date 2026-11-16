using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// The PUT /api/decks/{id}/sharing body. Level is "private", "link" or "named" on the wire; Viewers are email addresses.
public sealed record SharingUpdate(DeckSharing Level, IReadOnlyList<string> Viewers);
