using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Translations.Mcp;

// What an agent may do with a deck's translations: it needs edit access to the deck, like update_qa; a deck the caller
// can't edit is 404 "Deck not found". The tools answer fixed results until the real service replaces them.
[RegisterPerRequest]
public class TranslationsAgentAccess(IDeckAccess access, ICurrentActor actor)
{
    public const string NOT_BUILT = "translations are not built yet";

    // TODO: replace with the translations service, which changes the deck's settings and returns its coverage.
    public async Task<SetTranslationsResult> SetAsync(string deckId, SetTranslationsChange change)
    {
        await RequireEditorAsync(deckId);
        return new SetTranslationsResult(NOT_BUILT, new TranslationsSettingsView(false, change.Primary ?? "en", []));
    }

    // TODO: replace with the translations service, which fit-checks and stores each language's lines.
    public async Task<TranslateSlidesResult> TranslateAsync(string deckId, TranslateSlidesChange change)
    {
        await RequireEditorAsync(deckId);
        return new TranslateSlidesResult(change.Language, 0, [], [NOT_BUILT]);
    }

    private async Task RequireEditorAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }
}
