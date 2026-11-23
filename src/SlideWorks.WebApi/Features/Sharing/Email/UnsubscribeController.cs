using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing.Email;

public sealed record UnsubscribeRequest([property: JsonRequired] string Token);

// The one-button unsubscribe page's call (C9). Anonymous: the token in the body is the credential.
[Route("api/email/unsubscribe")]
public class UnsubscribeController(IUnsubscribe unsubscribe) : ApiControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public Task<NoContentResult> Post(UnsubscribeRequest request) => NoContentAfter(unsubscribe.UnsubscribeAsync(request.Token));
}
