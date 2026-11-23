using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// One entry of the PUT body's people: a full email and its access, "read" or "write" on the wire. Both are required.
public sealed record SharingPersonUpdate(
    [property: JsonRequired] string Email,
    [property: JsonRequired] DeckViewerAccess Access);
