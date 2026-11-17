using Microsoft.AspNetCore.Mvc.Filters;

namespace SlideWorks.WebApi.Features.Join;

// Gives a guest who has no sw_guest cookie one, on a successful response only (J3): a refused code leaves nothing behind.
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
