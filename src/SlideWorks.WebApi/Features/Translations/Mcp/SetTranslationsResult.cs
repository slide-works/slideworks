namespace SlideWorks.WebApi.Features.Translations.Mcp;

// What set_translations answers: a sentence, and the settings after the change, which are null while translations are off
// (the same object get_deck shows).
public sealed record SetTranslationsResult(string Text, TranslationsSettingsView? Translations);
