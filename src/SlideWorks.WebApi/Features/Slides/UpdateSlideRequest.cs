using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// update_slide's arguments (K6). Slide is the 1-based number and Revision the one get_deck gave for it. A null Layout or
// Part keeps the slide's own. A missing or null slots never gets here: McpArgumentCheck refuses it on the wire as
// "slots: required".
public sealed record UpdateSlideRequest(string Deck, int Slide, long Revision, JsonElement Slots, string? Layout, int? Part);
