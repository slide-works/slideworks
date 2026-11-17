using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Presenting;

// Sets or clears a deck's talk length from the web (P7). The deck's owner only; anyone else gets 404.
[Route("api/decks/{id}/talk-length")]
public class TalkLengthController(TalkLengthCommands commands) : ApiControllerBase
{
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Put(string id, [FromBody] JsonElement body) => NoContentAfter(commands.SetAsync(id, TalkLengthRequest.Read(body), tool: null));
}
