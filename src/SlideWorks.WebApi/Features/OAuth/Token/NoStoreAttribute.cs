using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// RFC 6749 section 5.1: a response that may carry a token is never cached. Set before model binding and the action, so
// it is on every response, the error bodies included.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoStoreAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        context.HttpContext.Response.Headers[HeaderNames.CacheControl] = "no-store";
        context.HttpContext.Response.Headers[HeaderNames.Pragma] = "no-cache";
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nothing to do after: the headers were set before the response started.
    }
}
