using Agoda.IoC.Core;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Translations.Mcp;

// What an agent may do with a deck's translations: it needs edit access to the deck, like update_qa; a deck the caller
// can't edit is 404 "Deck not found".
[RegisterPerRequest]
public class TranslationsAgentAccess(IDeckAccess access, ICurrentActor actor, TranslationSettingsCommands settings, TranslationsSummary summary)
{
    // Changes the deck's settings, then answers a sentence and the settings with their coverage (null while translations are off).
    public async Task<SetTranslationsResult> SetAsync(string deckId, SetTranslationsChange change)
    {
        await RequireEditorAsync(deckId);
        await settings.ChangeAsync(deckId, change);
        var translations = await summary.ForDeckAsync(deckId);
        return new SetTranslationsResult(translations == null ? "Translations are off. Stored translations are kept." : Describe(translations), translations);
    }

    // "Translations are on: English is the primary language, offered in Thai and Japanese."
    private static string Describe(TranslationsSettingsView view)
        => $"Translations are on: {LanguageCatalogue.Find(view.Primary)?.EnglishName ?? view.Primary} is the primary language, offered in {(view.Languages.Count == 0 ? "no other languages yet" : string.Join(", ", view.Languages.Select(l => LanguageCatalogue.Find(l.Code)?.EnglishName ?? l.Code)))}.";

    private async Task RequireEditorAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }
}
