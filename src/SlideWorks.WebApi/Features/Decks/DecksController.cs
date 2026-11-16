using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Decks;

// The presenter's decks. Signed out, the fallback policy answers 401 before this runs.
[Route("api/decks")]
public class DecksController(DeckCommands commands) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateDeckResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ObjectResult> Create(CreateDeckRequest request)
        => StatusCode(StatusCodes.Status201Created, CreateDeckResult.From(await commands.CreateAsync(request, tool: null)));
}
