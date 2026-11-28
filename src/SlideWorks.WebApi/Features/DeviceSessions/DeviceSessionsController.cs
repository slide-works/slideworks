using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.DeviceSessions.Views;
using SlideWorks.WebApi.Features.OAuth.Token;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Features.DeviceSessions;

// A phone's sign-in and the person's signed-in devices (K4). Minting is for a cookie session, pending included (the confirm
// page); the exchange is anonymous, rate-limited and never cached; listing and revoking follow the approved gate, by cookie
// or by a device bearer. DELETE current (the app signing itself out) is the bearer scheme's.
[Route("api/auth/device-sessions")]
public class DeviceSessionsController(DeviceSignIn signIn) : ApiControllerBase
{
    [HttpPost("codes")]
    [Authorize(Policy = SignedInPolicy.NAME)]
    [ProducesResponseType<DeviceSignInCodeView>(StatusCodes.Status200OK)]
    [ProducesResponseType<RefusalResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<RefusalResponse>(StatusCodes.Status403Forbidden)]
    public Task<DeviceSignInCodeView> Mint(DeviceSignInCodeRequest request) => signIn.MintAsync(request, User);

    [HttpPost("exchange")]
    [AllowAnonymous]
    [NoStore]
    [ProducesResponseType<DeviceSessionExchangeView>(StatusCodes.Status201Created)]
    [ProducesResponseType<RefusalResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ObjectResult> Exchange(DeviceSessionExchangeRequest request)
        => StatusCode(StatusCodes.Status201Created, await signIn.ExchangeAsync(request, HttpContext.Connection.RemoteIpAddress));

    [HttpGet]
    [ProducesResponseType<DeviceSessionsView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<DeviceSessionsView> List() => signIn.ListAsync(User);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Revoke(Guid id) => NoContentAfter(signIn.RevokeAsync(id, User));
}
