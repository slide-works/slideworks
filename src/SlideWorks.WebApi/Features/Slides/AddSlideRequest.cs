using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// add_slide's arguments (K6, L7). Position is the 1-based number the new slide takes; null adds it at the end. A null
// Slots is refused as "slots: required". DeepDive true marks the slide as a deep dive (D4); null or false leaves it unmarked.
public sealed record AddSlideRequest(string Deck, string Layout, JsonElement? Slots, int? Part, int? Position, string? Notes, bool? DeepDive = null);
