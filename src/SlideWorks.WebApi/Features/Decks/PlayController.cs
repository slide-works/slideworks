using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Decks;

// The player's one request (deck-play.json). Open to anonymous callers (K1); IDeckAccess decides, and an unknown or
// forbidden deck is 404 "Deck not found". ?language= reads the slides in that language (K8); one the deck doesn't offer reads as
// the primary.
[Route("api/play")]
public class PlayController(DeckReader reader) : ApiControllerBase
{
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType<DeckPlayView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<DeckPlayView> Get(string id, [FromQuery] string? language) => reader.GetPlayAsync(id, language);
}
