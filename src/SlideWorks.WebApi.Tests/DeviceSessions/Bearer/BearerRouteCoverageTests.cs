using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.DeviceSessions.Bearer;

// Every /api endpoint treats a phone's device bearer exactly as the same person's cookie (K5), so a new endpoint can't
// forget the bearer. The endpoints come from the app's endpoint data source, each called with made-up route values and an
// empty JSON body: the cookie's answer and the bearer's must be the same status. Each endpoint gets a fresh person, so one
// endpoint's writes can't change another's answer. The three exemptions answer a bearer differently on purpose and are
// asserted on their own.
[TestFixture]
public class BearerRouteCoverageTests : ApiTestFixture
{
    // K5's route-coverage exemptions, as "METHOD pattern".
    private const string MINT_CODE = "POST /api/auth/device-sessions/codes";

    private const string DELETE_CURRENT = "DELETE /api/auth/device-sessions/current";

    private const string SIGN_OUT = "POST /api/signout";

    private static readonly string[] EXEMPTIONS = [MINT_CODE, DELETE_CURRENT, SIGN_OUT];

    private static readonly string[] UNCALLED_METHODS = [HttpMethods.Options, HttpMethods.Trace, HttpMethods.Connect];

    private DeviceSessionRowSeed Seed => new(Factory.Services);

    [Test]
    public async Task BearerRouteCoverage_WhenEveryApiRouteIsCalled_ShouldAnswerAsTheCookieDoes()
    {
        var routes = ApiRoutes().Where(route => !EXEMPTIONS.Contains(route.Name, StringComparer.Ordinal)).ToList();
        var mismatches = new List<string>();

        foreach (var route in routes)
        {
            var person = TestUser.NewApproved();
            var (_, secret) = await Seed.CreateAsync(person);
            using var cookie = ClientFor(person);
            using var bearer = BearerClient(secret);
            var byBearer = await StatusAsync(bearer, route);
            var byCookie = await StatusAsync(cookie, route);
            if (byBearer != byCookie)
            {
                mismatches.Add($"{route.Name}: cookie {(int)byCookie}, bearer {(int)byBearer}");
            }
        }

        routes.Count.ShouldBeGreaterThan(50, "the endpoint data source should list every /api endpoint");
        mismatches.ShouldBeEmpty();
    }

    [Test]
    public async Task BearerRouteCoverage_WhenSessionEnded_ShouldAnswerSessionEndedEverywhere()
    {
        var (session, secret) = await Seed.CreateAsync(TestUser.NewApproved());
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().EndAsync(session.Id, DeviceSessionEndReason.WebSignOut);
        }
        using var bearer = BearerClient(secret);
        var routes = ApiRoutes();
        var answers = new List<string>();

        foreach (var route in routes)
        {
            using var response = await bearer.SendAsync(RequestFor(route));
            var body = await response.Content.ReadAsStringAsync();
            answers.Add($"{route.Name}: {(int)response.StatusCode} {body}");
        }

        routes.Count.ShouldBeGreaterThan(50);
        answers.Where(answer => !answer.EndsWith(": 401 {\"error\":\"session_ended\"}", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    // POST /api/signout: the bearer gets the cookie's 204, but the phone stays signed in (it signs out with DELETE current).
    [Test]
    public async Task BearerRouteCoverage_WhenSignOutWithBearer_ShouldAnswer204AndKeepTheSession()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.NewApproved());
        using var bearer = BearerClient(secret);

        using var signOut = await bearer.PostAsync("/api/signout", content: null);
        using var me = await bearer.GetAsync("/api/me");

        (signOut.StatusCode, me.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.OK));
    }

    // DELETE /api/auth/device-sessions/current: a cookie has no device session (404 no_device_session); a bearer ends its own.
    [Test]
    public async Task BearerRouteCoverage_WhenDeleteCurrent_ShouldAnswerTheCookie404AndTheBearer204()
    {
        var person = TestUser.NewApproved();
        var (_, secret) = await Seed.CreateAsync(person);
        using var cookie = ClientFor(person);
        using var bearer = BearerClient(secret);

        using var byCookie = await cookie.DeleteAsync("/api/auth/device-sessions/current");
        using var byBearer = await bearer.DeleteAsync("/api/auth/device-sessions/current");

        (byCookie.StatusCode, byBearer.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NoContent));
        (await byCookie.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("no_device_session");
    }

    // POST /api/auth/device-sessions/codes: codes are minted with the cookie only, so a bearer gets 403 cookie_required.
    // Red until SW-958 maps the codes endpoint (it answers the /api fallback's 404 until then).
    [Test]
    public async Task BearerRouteCoverage_WhenCodeMintedWithBearer_ShouldAnswerCookieRequired()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.NewApproved());
        using var bearer = BearerClient(secret);

        using var response = await bearer.PostAsJsonAsync(
            "/api/auth/device-sessions/codes",
            new { challenge = new string('a', 43), state = new string('s', 16), deviceName = "Phone" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("cookie_required");
    }

    private HttpClient BearerClient(string secret)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return client;
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, ApiRoute route)
    {
        using var response = await client.SendAsync(RequestFor(route));
        return response.StatusCode;
    }

    private static HttpRequestMessage RequestFor(ApiRoute route)
    {
        var request = new HttpRequestMessage(new HttpMethod(route.Method), route.Path);
        if (!HttpMethods.IsGet(route.Method) && !HttpMethods.IsHead(route.Method) && !HttpMethods.IsDelete(route.Method))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }
        return request;
    }

    // Every method of every endpoint under /api, with each route parameter filled by a value its constraints accept.
    private List<ApiRoute> ApiRoutes()
        => [.. Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => ("/" + (endpoint.RoutePattern.RawText ?? "").TrimStart('/')).StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            .SelectMany(endpoint => MethodsOf(endpoint).Select(method => new ApiRoute(
                method,
                "/" + (endpoint.RoutePattern.RawText ?? "").TrimStart('/'),
                PathOf(endpoint.RoutePattern))))
            .DistinctBy(route => route.Name)
            .OrderBy(route => route.Name, StringComparer.Ordinal)];

    // An endpoint with no method metadata (the /api fallback) answers any method; GET stands for it.
    private static IEnumerable<string> MethodsOf(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        return methods is { Count: > 0 }
            ? methods.Where(method => !UNCALLED_METHODS.Contains(method, StringComparer.OrdinalIgnoreCase))
            : [HttpMethods.Get];
    }

    private static string PathOf(RoutePattern pattern)
        => "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(PartOf))));

    private static string PartOf(RoutePatternPart part) => part switch
    {
        RoutePatternLiteralPart literal => literal.Content,
        RoutePatternSeparatorPart separator => separator.Content,
        RoutePatternParameterPart parameter => ValueFor(parameter),
        _ => "",
    };

    private static string ValueFor(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(policy => policy.Content ?? "").ToList();
        if (constraints.Any(c => c.StartsWith("guid", StringComparison.OrdinalIgnoreCase)))
        {
            return "4b5e6a3c-9d1f-4e2a-8b7c-0a1b2c3d4e5f";
        }
        return constraints.Any(c => c.StartsWith("int", StringComparison.OrdinalIgnoreCase) || c.StartsWith("min", StringComparison.OrdinalIgnoreCase))
            ? "1"
            : "x1";
    }

    private sealed record ApiRoute(string Method, string Pattern, string Path)
    {
        public string Name => $"{Method} {Pattern}";
    }
}
