using System.Net;
using System.Net.Http.Headers;

namespace SlideWorks.WebApi.Tests;

// SLIDEWORKS_API_TOKEN guards /api; SLIDEWORKS_API_EXTRA_TOKENS are further tokens
// accepted alongside it, so another app can have its own.
[TestFixture]
public class TokenAuthTests : ApiTestFixture
{
    protected override Dictionary<string, string?> ExtraConfig => new()
    {
        ["SLIDEWORKS_API_EXTRA_TOKENS"] = "dashboard-token, other-token,",
    };

    // No /api route is mapped between the Notes sample's removal and the first deck routes, so an accepted token
    // reaches the /api fallback's 404; a refused one stops at the guard's 401.
    [TestCase(TEST_TOKEN)]
    [TestCase("dashboard-token")]
    [TestCase("other-token")]
    public async Task Api_WhenPresentedAnyConfiguredToken_PassesTheGuard(string token)
    {
        var response = await GetApiAsync(token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-token")]
    [TestCase("dashboard-token, other-token")]
    public async Task Api_WhenPresentedAnythingElse_Returns401(string? token)
    {
        var response = await GetApiAsync(token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // The query-string token exists for the WebSocket upgrade on /hubs only.
    [Test]
    public async Task Api_WhenTokenOnlyInQueryString_Returns401()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/anything?access_token={TEST_TOKEN}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> GetApiAsync(string? token)
    {
        using var client = Factory.CreateClient();
        if (token != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await client.GetAsync("/api/anything");
    }
}
