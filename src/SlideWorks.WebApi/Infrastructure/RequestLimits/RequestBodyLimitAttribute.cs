namespace SlideWorks.WebApi.Infrastructure.RequestLimits;

// A tighter cap on one action's request body than the shared default (RequestBodyLimitMiddleware.DEFAULT_BYTES). A body over
// it is 413 { "error": "body: at most N bytes" } before the action or model binding reads any of it.
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequestBodyLimitAttribute(int maxBytes) : Attribute
{
    public int MaxBytes => maxBytes;
}
