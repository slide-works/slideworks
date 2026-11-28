using System.Text.Json.Nodes;

namespace SlideWorks.WebApi.Features.Translations.Mcp;

// One entry of translate_slides: a slide's 1-based number, the revision it was read at and its lines, keyed by slot path.
// A value is the translated text, or null to clear that line.
public sealed record SlideLinesEntry(int Slide, long Revision, JsonObject Lines);
