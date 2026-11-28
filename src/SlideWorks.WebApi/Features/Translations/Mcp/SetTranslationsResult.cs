namespace SlideWorks.WebApi.Features.Translations.Mcp;

// What set_translations answers: a sentence, and the settings after the change.
public sealed record SetTranslationsResult(string Text, TranslationsSettingsView Translations);
