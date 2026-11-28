namespace SlideWorks.WebApi.Features.Translations.Mcp;

// The translations settings set_translations answers: whether they are on, the primary language and each language's coverage.
public sealed record TranslationsSettingsView(bool Enabled, string Primary, IReadOnlyList<LanguageCoverage> Languages);
