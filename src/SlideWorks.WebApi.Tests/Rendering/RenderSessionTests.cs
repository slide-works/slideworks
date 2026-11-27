using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Rendering;

// POST /api/render/{id}/session (L9): a valid render token in the X-Render-Token header becomes the sw_render cookie,
// HttpOnly, SameSite=Strict, on /api/images and on /api/templates, expiring with the token. Anything else is 404 "Deck not found" with
// no cookie. Anonymous: the token is the only credential.
[TestFixture]
public class RenderSessionTests : ApiTestFixture
{
    private string _deck = null!;

    [OneTimeSetUp]
    public async Task CreateDeck()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        _deck = await DeckSeed.CreateDeckAsync(agent);
    }

    [Test]
    public async Task PostSession_WhenTokenIsForPrint_ShouldSetTheImageCookie()
    {
        var token = Issue(_deck, RenderPurpose.Print);

        using var response = await PostSessionAsync(_deck, token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cookies = SetCookies(response);
        cookies.Select(c => c.Path.ToString()).ShouldBe(["/api/images", "/api/templates"], ignoreOrder: true);
        cookies.ShouldAllBe(cookie => cookie.Value.ToString() == token);
    }

    [TestCase(RenderPurpose.Preview)]
    [TestCase(RenderPurpose.Card)]
    public async Task Post_WhenTokenValid_ShouldSetTheRenderCookieOnBothPaths(RenderPurpose purpose)
    {
        var token = Issue(_deck, purpose);

        using var response = await PostSessionAsync(_deck, token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cookies = SetCookies(response);
        cookies.Select(c => c.Path.ToString()).ShouldBe(["/api/images", "/api/templates"], ignoreOrder: true);
        foreach (var cookie in cookies)
        {
            cookie.Name.ToString().ShouldBe(RenderSessions.COOKIE);
            cookie.Value.ToString().ShouldBe(token);
            cookie.HttpOnly.ShouldBeTrue();
            cookie.SameSite.ShouldBe(Microsoft.Net.Http.Headers.SameSiteMode.Strict);
            cookie.Expires.ShouldBe(Clock.GetUtcNow().Add(RenderTokens.LifetimeOf(purpose)));
        }
    }

    // No token, a token for another deck, a tampered token, and an expired one.
    [TestCase("none")]
    [TestCase("another deck")]
    [TestCase("tampered")]
    [TestCase("expired")]
    public async Task Post_WhenTokenMissingOrInvalid_ShouldBe404AndSetNothing(string token)
    {
        var sent = token switch
        {
            "none" => null,
            "another deck" => Issue("dk_OTHER01", RenderPurpose.Preview),
            "tampered" => Issue(_deck, RenderPurpose.Preview) + "x",
            _ => Issue(_deck, RenderPurpose.Card),
        };
        if (token == "expired")
        {
            Clock.Advance(RenderTokens.LifetimeOf(RenderPurpose.Card) + TimeSpan.FromSeconds(1));
        }

        using var response = await PostSessionAsync(_deck, sent);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Deck not found");
        SetCookies(response).ShouldBeEmpty();
    }

    // The server's own headless renderer (check_deck) opens the render page at its loopback address, not the public URL, so
    // this POST arrives with an Origin that isn't SlideWorks' public one. The token header is its only credential, and the
    // cross-site guard must let it through, or every measured slide is a renderFailed "Cross-site request refused".
    [Test]
    public async Task Post_WhenOriginIsTheRenderersLoopbackAddress_ShouldSetTheRenderCookie()
    {
        using var response = await PostSessionAsync(_deck, Issue(_deck, RenderPurpose.Preview), origin: "http://127.0.0.1:8080");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        SetCookies(response).ShouldNotBeEmpty();
    }

    private string Issue(string deck, RenderPurpose purpose) =>
        Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, purpose);

    private async Task<HttpResponseMessage> PostSessionAsync(string deck, string? token, string? origin = null)
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/render/{deck}/session");
        if (token != null)
        {
            request.Headers.Add(RenderSessions.HEADER, token);
        }
        if (origin != null)
        {
            request.Headers.Remove("Origin");
            request.Headers.Add("Origin", origin);
        }
        return await anonymous.SendAsync(request);
    }

    private static IList<SetCookieHeaderValue> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values) ? SetCookieHeaderValue.ParseList([.. values]) : [];
}
