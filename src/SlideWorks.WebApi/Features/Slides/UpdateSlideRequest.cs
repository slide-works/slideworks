using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// update_slide's arguments (K6, L7). Slide is the 1-based number and Revision the one get_deck gave for it. A null Layout,
// Part or DeepDive keeps the slide's own; a null Slots is refused as "slots: required".
public sealed record UpdateSlideRequest(string Deck, int Slide, long Revision, JsonElement? Slots, string? Layout, int? Part, bool? DeepDive = null);
