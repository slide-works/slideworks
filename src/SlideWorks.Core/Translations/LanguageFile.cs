namespace SlideWorks.Core.Translations;

// The root of src/contracts/languages.json.
internal sealed record LanguageFile(
    IReadOnlyDictionary<string, LanguageScript> Scripts,
    IReadOnlyList<Language> Languages,
    IReadOnlyList<string> RightToLeft);
