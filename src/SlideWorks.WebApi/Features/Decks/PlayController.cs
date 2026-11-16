using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Decks;

// The player's one request (deck-play.json). Open to anonymous callers (K1); IDeckAccess decides, and an unknown or
// forbidden deck is 404 "Deck not found".
[Route("api/play")]
public class PlayController(DeckReader reader) : ApiControllerBase
{
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType<DeckPlayView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<DeckPlayView> Get(string id) => reader.GetPlayAsync(id);
}
