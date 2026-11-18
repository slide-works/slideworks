using Agoda.IoC.Core;
using SlideWorks.Core.Look;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render;

// What a headless browser reads (L9): the play view for the agent's render page, and the card view for the share card.
// The render token is the only key: it must be valid for this deck and this purpose (Preview for the play view, Card for
// the card), or the deck is not found, so a private deck is readable only through its own token.
[RegisterPerRequest]
public class RenderViews(DeckReader reader, IRenderTokens tokens, IDeckLooks looks, TimeProvider clock)
{
    public async Task<DeckPlayView> PlayAsync(string deckId, string? token)
    {
        Require(token, deckId, RenderPurpose.Preview);
        return (await reader.ReadForRenderAsync(deckId)).Play;
    }

    // A deck whose sharing isn't link gets the generic card (D12): it names nothing about the deck.
    public async Task<CardView> CardAsync(string deckId, string? token)
    {
        Require(token, deckId, RenderPurpose.Card);
        var deck = await reader.ReadForRenderAsync(deckId);
        return deck.Sharing == DeckSharing.Link
            ? CardView.ForDeck(deck.Play.Title, deck.Subtitle, deck.Play.Author, deck.Play.Slides.Count, deck.Play.Look)
            : CardView.GenericWith(await looks.DefaultAsync(clock.GetUtcNow(), CancellationToken.None));
    }

    private void Require(string? token, string deckId, RenderPurpose purpose)
    {
        if (!tokens.Validate(token, deckId, purpose))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }
}
