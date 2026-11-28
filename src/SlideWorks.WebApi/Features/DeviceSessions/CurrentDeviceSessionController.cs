using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Features.DeviceSessions;

// DELETE /api/auth/device-sessions/current (K4, K5): the app signs itself out by ending the device session its bearer
// names, with reason appSignOut. A cookie caller has no device session and gets 404 no_device_session. SignedInPolicy,
// so a pending person's phone can sign out too.
[Route("api/auth/device-sessions/current")]
public class CurrentDeviceSessionController(IDeviceSessions sessions) : ApiControllerBase
{
    public const string NO_DEVICE_SESSION = "no_device_session";

    public const string NO_DEVICE_SESSION_MESSAGE = "This request isn't from a signed-in phone.";

    [HttpDelete]
    [Authorize(Policy = SignedInPolicy.NAME)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<RefusalResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Delete() => NoContentAfter(sessions.EndAsync(CallerSessionId(), DeviceSessionEndReason.AppSignOut));

    private Guid CallerSessionId()
        => Guid.TryParse(User.FindFirstValue(SlideWorksClaims.DEVICE_SESSION_ID), out var id)
            ? id
            : throw new RefusalException(StatusCodes.Status404NotFound, NO_DEVICE_SESSION, NO_DEVICE_SESSION_MESSAGE);
}
