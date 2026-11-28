namespace SlideWorks.WebApi.Features.Translations.Mcp;

// The arguments of translate_slides besides the deck: the language and each slide's lines.
public sealed record TranslateSlidesChange(string Language, IReadOnlyList<SlideLinesEntry?> Slides);
