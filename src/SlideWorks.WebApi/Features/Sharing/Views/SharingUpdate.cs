using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// The PUT /api/decks/{id}/sharing body. Level is "private", "link" or "named" on the wire, and is required: a body without
// it fails binding instead of reading as private. Viewers are email addresses.
public sealed record SharingUpdate([property: JsonRequired] DeckSharing Level, IReadOnlyList<string> Viewers);
