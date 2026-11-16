using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// RFC 6749 sections 3.2 and 3.1 (and RFC 7009 section 2.1): the parameters come as application/x-www-form-urlencoded,
// each at most once. Anything else (multipart/form-data, JSON, no body, a repeated parameter) is 400 invalid_request
// before model binding, which would otherwise take multipart and quietly keep the first of two values. Ordered after
// NoStore and TokenRateLimit, so a refusal is no-store and a flood is refused before its body is read.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class FormEncodedOnlyAttribute : Attribute, IAsyncResourceFilter, IOrderedFilter
{
    private const string FORM_URL_ENCODED = "application/x-www-form-urlencoded";

    public int Order => 2;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !string.Equals(contentType.MediaType.Value, FORM_URL_ENCODED, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = Refuse($"Send the parameters as {FORM_URL_ENCODED}.");
            return;
        }
        var form = await request.ReadFormAsync(context.HttpContext.RequestAborted);
        var repeated = form.FirstOrDefault(f => f.Value.Count > 1).Key;
        if (repeated != null)
        {
            context.Result = Refuse($"{repeated} was sent more than once; send each parameter at most once.");
            return;
        }
        await next();
    }

    private static ObjectResult Refuse(string description)
        => new(new OAuthTokenErrorView("invalid_request", description)) { StatusCode = StatusCodes.Status400BadRequest };
}
