using Microsoft.AspNetCore.Authentication;

namespace SlideWorks.WebApi.Infrastructure.Identity.Device;

// Refuses a request whose device secret was refused (K5), on every /api and /hubs endpoint, anonymous ones included: an
// app holding an ended secret learns it at once (401 session_ended), and the deck hub never opens a signed-out connection
// for it. Runs right after authentication, so the refusal comes before the write guard and any endpoint.
public sealed class DeviceBearerGate(RequestDelegate next)
{
    // AG0003 is about helpers taking the whole HttpContext; a middleware's InvokeAsync has to, it is the shape ASP.NET
    // calls.
#pragma warning disable AG0003
    public async Task InvokeAsync(HttpContext context)
#pragma warning restore AG0003
    {
        if (DeviceBearer.SecretOf(context.Request) != null && !context.User.HasClaim(c => c.Type == SlideWorksClaims.DEVICE_SESSION_ID))
        {
            await context.ChallengeAsync(DeviceBearer.SCHEME);
            return;
        }
        await next(context);
    }
}
