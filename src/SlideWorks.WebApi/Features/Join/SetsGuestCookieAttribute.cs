using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Features.Join;

// Gives a guest who has no guest id one, on a successful response only (J3): the sw_guest cookie, or for the app the
// X-SlideWorks-Guest response header (K7, D10). A refused code leaves nothing behind.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SetsGuestCookieAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is Microsoft.AspNetCore.Mvc.ObjectResult { StatusCode: null or 200 })
        {
            GuestCookie.EnsureOn(context.HttpContext.Request, context.HttpContext.Response);
        }
    }
}
