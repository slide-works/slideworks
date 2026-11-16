using Agoda.IoC.Core;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Render;

// GET /api/cards/{id}.png (L9, D12): a link-shared deck's own card, and the generic card for any other id (unknown,
// private or named-viewer), which never starts a deck render. When the card can't be rendered (SW-81 D5):
// - the renderer is unavailable or timed out, which passes: 503, no-store, Retry-After;
// - the render ran and failed (a broken logo, say), which repeats until the deck changes: the generic card, no-store
//   with no ETag, and a warning naming the deck and the failure. RenderingFailedException's text never holds the token.
[RegisterPerRequest]
public class ShareCards(LinkDecks links, ICardRenderer cards, ILogger<ShareCards> logger)
{
    public async Task<IActionResult> PngAsync(string deckId, CancellationToken ct)
    {
        try
        {
            return await CardAsync(deckId, ct);
        }
        catch (Exception e) when (e is RendererUnavailableException or TimeoutException)
        {
            return new CardUnavailableResult(e.Message);
        }
    }

    private async Task<IActionResult> CardAsync(string deckId, CancellationToken ct)
    {
        if (!await links.IsLinkSharedAsync(deckId))
        {
            return new CardPngResult(await cards.RenderGenericCardAsync(ct));
        }
        try
        {
            return new CardPngResult(await cards.RenderCardAsync(deckId, ct));
        }
        catch (RenderingFailedException e)
        {
            // e has no inner exception and its text never holds the token (SW-78 D6), so it is safe to log whole.
            logger.LogWarning(e, "Share card for deck {DeckId} failed, so the generic card was sent: {Error}", deckId, e.Message);
            return new CardPngResult(await cards.RenderGenericCardAsync(ct), cacheable: false);
        }
    }
}
