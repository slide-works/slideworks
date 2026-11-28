namespace SlideWorks.WebApi.Features.Translations;

// A deck's translation settings (SW-883 K2). A deck with no settings row is off, with primary en and no languages. It takes
// no ICurrentActor, so the audience's code can call it. Reads only; the settings are written by Features/Translations.
// Per request, because it uses the database, as the Q&A settings do.
public interface IDeckTranslations
{
    Task<TranslationSettings> ForDeckAsync(string deckId);
}
