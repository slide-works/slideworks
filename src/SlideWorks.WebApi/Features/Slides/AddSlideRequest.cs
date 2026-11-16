using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// add_slide's arguments (K6). Position is the 1-based number the new slide takes; null adds it at the end.
public sealed record AddSlideRequest(string Deck, string Layout, JsonElement Slots, int? Part, int? Position, string? Notes);
