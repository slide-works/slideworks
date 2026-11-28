using System.Text.Json.Nodes;

namespace SlideWorks.WebApi.Features.Translations.Mcp;

// One slide of translate_slides' answer: its stored lines and how many are current, stale or missing in that language.
public sealed record TranslatedSlide(int Slide, long Revision, JsonObject Lines, int Current, int Stale, int Missing);
