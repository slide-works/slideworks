using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// The PUT /api/decks/{id}/sharing body. Level is "private", "link" or "named" on the wire, and is required: a body without
// it fails binding instead of reading as private. People replaces the whole list with each person's access (C4). Viewers
// is the older body: email addresses that each get read; reading stops past ViewerEmails.MAX_ENTRIES. A body gives people
// or viewers; with both, people wins.
public sealed record SharingUpdate(
    [property: JsonRequired] DeckSharing Level,
    [property: JsonConverter(typeof(ViewerListConverter))] IReadOnlyList<string>? Viewers = null,
    [property: JsonConverter(typeof(PeopleListConverter))] IReadOnlyList<SharingPersonUpdate>? People = null);
