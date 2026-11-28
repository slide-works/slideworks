using Agoda.IoC.Core;
using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.Core.Rendering;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Render;

// What a headless browser reads (L9): the play view for the agent's render page, and the card view for the share card.
// The render token is the only key: it must be valid for this deck and this purpose (Preview for the play view, Card for
// the card), or the deck is not found, so a private deck is readable only through its own token.
[RegisterPerRequest]
public class RenderViews(DeckReader reader, IRenderTokens tokens, IDeckLooks looks, IPublicUrls urls, TimeProvider clock)
{
    // The play view in the language, or the primary for none (K9). The token is checked before anything is read.
    public async Task<DeckPlayView> PlayAsync(string deckId, string? token, string? language)
    {
        Require(token, deckId, RenderPurpose.Preview);
        return (await reader.ReadForRenderAsync(deckId, language)).Play;
    }

    // A deck whose sharing isn't link gets the generic card (D12): it names nothing about the deck.
    public async Task<CardView> CardAsync(string deckId, string? token)
    {
        Require(token, deckId, RenderPurpose.Card);
        var deck = await reader.ReadForRenderAsync(deckId);
        var host = new Uri(urls.Base, UriKind.Absolute).Authority;
        return deck.Sharing == DeckSharing.Link
            ? CardView.ForDeck(KickerOf(deck.Play.Slides), deck.Play.Title, deck.Subtitle, deck.Play.Author, deck.Play.Slides.Count, host, deck.Play.Look)
            : CardView.GenericWith(host, await looks.DefaultAsync(clock.GetUtcNow(), CancellationToken.None));
    }

    // The card's kicker (mockups section 09): the kicker of the deck's first title slide, or null.
    private static string? KickerOf(IEnumerable<DeckPlaySlide> slides)
    {
        var opening = slides.FirstOrDefault(slide => slide.Layout == TitleLayout.Definition.Name);
        return opening is not null
            && opening.Slots.ValueKind == JsonValueKind.Object
            && opening.Slots.TryGetProperty("kicker", out var kicker)
            && kicker.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(kicker.GetString())
            ? kicker.GetString()
            : null;
    }

    private void Require(string? token, string deckId, RenderPurpose purpose)
    {
        if (!tokens.Validate(token, deckId, purpose))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }
}
