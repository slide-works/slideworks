using System.Text.Json;

namespace SlideWorks.WebApi.Features.Translations.Data;

// A deck's translation settings (SW-883 K2), keyed by the deck. Made the first time a setting is changed; a deck with no row
// has TranslationSettings.Default. Changed in place, never deleted. Only Features/Translations writes it.
public class DeckTranslationSettingsRow
{
    public required string DeckId { get; set; }

    public bool Enabled { get; set; }

    public required string Primary { get; set; }

    // A JSON array of language codes, in the order the presenter listed them.
    public required string LanguagesJson { get; set; }

    public static DeckTranslationSettingsRow From(string deckId, TranslationSettings settings) => new()
    {
        DeckId = deckId,
        Enabled = settings.Enabled,
        Primary = settings.Primary,
        LanguagesJson = JsonSerializer.Serialize(settings.Languages),
    };

    public TranslationSettings ToSettings() => new(Enabled, Primary, JsonSerializer.Deserialize<string[]>(LanguagesJson) ?? throw new InvalidOperationException($"Deck {DeckId} has a null LanguagesJson."));
}
