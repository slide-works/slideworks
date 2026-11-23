using SlideWorks.WebApi.Infrastructure.RequestLimits;

namespace SlideWorks.WebApi.Infrastructure.Setup;

// The request-body limit for /mcp and /api (K6): Program.cs calls this one hook, and the rules live in
// RequestBodyLimitMiddleware.
public static class RequestLimitsSetup
{
    // After the trusted proxies and before authentication, so an oversized body is refused before anything reads it. It
    // reads the endpoint's attributes, which routing (added first by the host) has already matched.
    public static WebApplication UseSlideWorksRequestLimits(this WebApplication app)
    {
        app.UseMiddleware<RequestBodyLimitMiddleware>();
        return app;
    }
}
