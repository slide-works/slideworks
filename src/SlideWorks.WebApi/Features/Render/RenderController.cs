using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Render.Print;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render;

// The render API (L9), anonymous: a headless browser has no sign-in, only a render token in the X-Render-Token header.
// The token is never read from a query string or a cookie. A missing, invalid or wrong token is 404 "Deck not found".
[Route("api/render")]
[AllowAnonymous]
public class RenderController(RenderViews views, PrintViews prints) : ApiControllerBase
{
    public const string TOKEN_HEADER = "X-Render-Token";

    // The deck-play shape, for the agent's per-slide render page (a Preview token).
    [HttpGet("{id}")]
    [ProducesResponseType<DeckPlayView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<DeckPlayView> Get(string id, [FromHeader(Name = TOKEN_HEADER)] string? token) => views.PlayAsync(id, token);

    // card.json, for the server's share card page (a Card token).
    [HttpGet("{id}/card")]
    [ProducesResponseType<CardView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<CardView> Card(string id, [FromHeader(Name = TOKEN_HEADER)] string? token) => views.CardAsync(id, token);

    // deck-print.json, for the print page (a Print token): the deck and the last talk's results.
    [HttpGet("{id}/print")]
    [ProducesResponseType<DeckPrintView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<DeckPrintView> Print(string id, [FromHeader(Name = TOKEN_HEADER)] string? token, CancellationToken ct) => prints.PrintAsync(id, token, ct);
}
