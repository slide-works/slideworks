using Agoda.IoC.Core;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Render;

// GET /api/cards/{id}.png (L9, D12): a link-shared deck's own card, and the generic card for any other id (unknown,
// private or named-viewer), which never starts a deck render. A renderer that can't run or times out is 503 with its
// text, never the generic card in disguise.
[RegisterPerRequest]
public class ShareCards(LinkDecks links, ICardRenderer cards)
{
    public async Task<IActionResult> PngAsync(string deckId, CancellationToken ct)
    {
        try
        {
            var card = await links.IsLinkSharedAsync(deckId)
                ? await cards.RenderCardAsync(deckId, ct)
                : await cards.RenderGenericCardAsync(ct);
            return new CardPngResult(card);
        }
        catch (Exception e) when (e is RendererUnavailableException or TimeoutException)
        {
            return new ObjectResult(new ErrorResponse(e.Message)) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}
