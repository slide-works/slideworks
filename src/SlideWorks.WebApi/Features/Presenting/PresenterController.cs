using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Presenting;

// The presenter window's and the phone's one request (presenter.json, P3). Anonymous callers reach the access check, so
// a signed-out caller, a viewer and an agent all get the same 404 "Deck not found" as an unknown deck.
[Route("api/decks")]
public class PresenterController(PresenterQueries queries) : ApiControllerBase
{
    [HttpGet("{id}/presenter")]
    [AllowAnonymous]
    [ProducesResponseType<PresenterView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<PresenterView> Get(string id) => queries.PresenterAsync(id);
}
