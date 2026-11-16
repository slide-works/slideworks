using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The consent page's API. Under /api, so the default policy (an approved person) and the cross-site write guard both
// apply; it never moves under /oauth/, which the guard doesn't cover.
[ConsentGone]
[Route("api/oauth/consent/{id}")]
public class ConsentController(Consent consent) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ConsentView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status410Gone)]
    public Task<ConsentView> Get(string id) => consent.GetAsync(id);

    [HttpPost]
    [ProducesResponseType<ConsentDecisionView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status410Gone)]
    public Task<ConsentDecisionView> Post(string id, [FromBody] ConsentDecisionRequest decision) => consent.DecideAsync(id, decision);
}
