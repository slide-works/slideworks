using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SlideWorks.WebApi.Tests.Identity;

// CrossSiteWriteGuard covers every write under /api. A write endpoint anywhere else would dodge it, so every endpoint
// that accepts a method other than GET, HEAD, OPTIONS or TRACE must be under /api or named here with the reason it is
// safe without the guard. A new write outside /api fails this test instead of passing quietly.
[TestFixture]
public class WriteRouteTableTests : ApiTestFixture
{
    private static readonly string[] SAFE_METHODS = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace];

    // Route-pattern prefixes outside /api that may take writes.
    private static readonly string[] ALLOWED_PREFIXES_OUTSIDE_API =
    [
        // Agents with bearer tokens; a cookie is never accepted there.
        "/mcp",
        // OAuth form posts from agents, anonymous by design.
        "/oauth/",
    ];

    // Exact route patterns outside /api that may take writes. Nothing under /signin is here: sign-in starts with a GET
    // and its provider callback is handled inside UseAuthentication, so an unguarded write there would be a login-CSRF
    // hole.
    private static readonly string[] ALLOWED_EXACT_OUTSIDE_API =
    [
        // The deck hub (MapHub maps the hub and its negotiate endpoint). JoinDeck decides who joins.
        "/hubs/deck",
        "/hubs/deck/negotiate",
        // The /hubs catch-all that answers 404, so unknown hubs don't get the SPA shell.
        "/hubs/{**path}",
    ];

    [Test]
    public void Endpoints_WhenTheyAcceptWrites_ShouldBeUnderApiOrAllowed()
    {
        var writesOutsideApi = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(AcceptsWrites)
            .Select(endpoint => "/" + (endpoint.RoutePattern.RawText ?? "").TrimStart('/'))
            .Where(pattern => !pattern.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && !pattern.Equals("/api", StringComparison.OrdinalIgnoreCase))
            .Where(pattern => !ALLOWED_PREFIXES_OUTSIDE_API.Any(allowed => pattern.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)))
            .Where(pattern => !ALLOWED_EXACT_OUTSIDE_API.Contains(pattern, StringComparer.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        writesOutsideApi.ShouldBeEmpty();
    }

    // No method metadata means the endpoint takes any method, writes included.
    private static bool AcceptsWrites(Endpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        return methods == null || methods.Count == 0 || methods.Any(method => !SAFE_METHODS.Contains(method, StringComparer.OrdinalIgnoreCase));
    }
}
