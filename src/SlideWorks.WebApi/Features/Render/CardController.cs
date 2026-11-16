using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Render;

// The share card image link previews fetch (L9, D12), anonymous: a link-shared deck's card, the generic card otherwise.
[Route("api/cards")]
[AllowAnonymous]
public class CardController(ShareCards cards) : ApiControllerBase
{
    [HttpGet("{id}.png")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "image/png")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Png(string id, CancellationToken ct) => cards.PngAsync(id, ct);
}
