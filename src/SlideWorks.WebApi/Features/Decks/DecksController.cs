using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Decks;

// The presenter's decks. Signed out, the fallback policy answers 401 before this runs.
[Route("api/decks")]
public class DecksController(DeckCommands commands, DeckQueries queries) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<DecksLibraryView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<DecksLibraryView> Library() => queries.LibraryAsync();

    // The owner's deck page. Anyone else, a viewer included, gets 404: viewers open the player.
    [HttpGet("{id}")]
    [ProducesResponseType<DeckPageView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<DeckPageView> Get(string id) => queries.PageAsync(id);

    [HttpPost]
    [ProducesResponseType<CreateDeckResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ObjectResult> Create(CreateDeckRequest request)
        => StatusCode(StatusCodes.Status201Created, CreateDeckResult.From(await commands.CreateAsync(request, tool: null)));
}
