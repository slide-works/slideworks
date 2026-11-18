using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Features.Render;

// 204 and the sw_render cookie (L9): HttpOnly, so the page's script never reads it back; SameSite=Strict, so no other
// site's page sends it; on /api/images and /api/templates only (one cookie per path), so no other route ever sees it; and gone when the token expires. Secure
// whenever the request is HTTPS, like the session cookie.
public sealed class RenderSessionResult(string token, DateTimeOffset expires) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        // S2092 wants Secure set unconditionally. It is set whenever the request is HTTPS, which PublicOriginMiddleware
        // takes from the public URL, the same rule as the session cookie; a plain-HTTP local run still gets the cookie.
#pragma warning disable S2092
        foreach (var path in RenderSessions.COOKIE_PATHS)
        {
            response.Cookies.Append(RenderSessions.COOKIE, token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = path,
                Expires = expires,
                Secure = context.HttpContext.Request.IsHttps,
                IsEssential = true,
            });
        }
#pragma warning restore S2092
        response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }
}
