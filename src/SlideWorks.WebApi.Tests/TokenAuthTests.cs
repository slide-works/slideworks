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

    [TestCase(TEST_TOKEN)]
    [TestCase("dashboard-token")]
    [TestCase("other-token")]
    public async Task Api_WhenPresentedAnyConfiguredToken_Returns200(string token)
    {
        var response = await GetNotesAsync(token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-token")]
    [TestCase("dashboard-token, other-token")]
    public async Task Api_WhenPresentedAnythingElse_Returns401(string? token)
    {
        var response = await GetNotesAsync(token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // The query-string token exists for the WebSocket upgrade on /hubs only.
    [Test]
    public async Task Api_WhenTokenOnlyInQueryString_Returns401()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/notes?access_token={TEST_TOKEN}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> GetNotesAsync(string? token)
    {
        using var client = Factory.CreateClient();
        if (token != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await client.GetAsync("/api/notes");
    }
}
