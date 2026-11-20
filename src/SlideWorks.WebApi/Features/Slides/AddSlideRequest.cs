using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// add_slide's arguments (K6, L7). Position is the 1-based number the new slide takes; null adds it at the end. A missing or
// null slots never gets here: McpArgumentCheck refuses it on the wire as "slots: required". DeepDive true marks the slide
// as a deep dive (D4); null or false leaves it unmarked. Surface is "light" or "dark" where the layout lets the
// slide pick; null is the layout's own. Poll is the poll slide's settings ({pick, max, correct, results, opens}); null
// keeps the defaults. It is not slots.
public sealed record AddSlideRequest(string Deck, string Layout, JsonElement Slots, int? Part, int? Position, string? Notes, bool? DeepDive = null, string? Surface = null, JsonElement? Poll = null);
