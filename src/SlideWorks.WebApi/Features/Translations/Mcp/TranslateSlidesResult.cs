namespace SlideWorks.WebApi.Features.Translations.Mcp;

// What translate_slides answers: the language, the deck's revision, each slide written and any warnings.
public sealed record TranslateSlidesResult(string Language, long Revision, IReadOnlyList<TranslatedSlide> Slides, IReadOnlyList<string> Warnings);
