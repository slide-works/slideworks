namespace SlideWorks.WebApi.Features.Translations.Mcp;

// The arguments of set_translations besides the deck; each is left as it is when null.
public sealed record SetTranslationsChange(bool? Enabled, string? Primary, IReadOnlyList<string>? Languages);
