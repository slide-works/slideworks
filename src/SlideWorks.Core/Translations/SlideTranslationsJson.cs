using System.Text.Json;

namespace SlideWorks.Core.Translations;

// Reads and writes Slide.TranslationsJson (K2): {"th": {"/title": {"text": "…", "from": "…"}}}, by
// language code, then by line path. Null means the slide has no lines.
public static class SlideTranslationsJson
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslationEntry>> _none =
        new Dictionary<string, IReadOnlyDictionary<string, TranslationEntry>>(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslationEntry>> Read(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return _none;
        }

        var stored = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, TranslationEntry>>>(json, JsonSerializerOptions.Web)
            ?? throw new JsonException("Slide translations must be an object, not null");
        return stored.ToDictionary(
            language => language.Key,
            language => (IReadOnlyDictionary<string, TranslationEntry>)new Dictionary<string, TranslationEntry>(language.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    // The stored form, or null when there are no languages.
    public static string? Write(IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslationEntry>> translations) =>
        translations.Count == 0 ? null : JsonSerializer.Serialize(translations, JsonSerializerOptions.Web);
}
