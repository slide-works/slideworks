using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Infrastructure.Identity.Google;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// Without both SLIDEWORKS_GOOGLE_CLIENT_ID and _SECRET the server still starts and serves everything else; only sign-in
// is off, and it says so.
[TestFixture(null, null)]
[TestFixture("client-id-only", null)]
[TestFixture(null, "secret-only")]
public class GoogleSettingsTests(string? clientId, string? clientSecret) : ApiTestFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Dictionary<string, string?> ExtraConfig => new()
    {
        [GoogleSignInSettings.CLIENT_ID_KEY] = clientId,
        [GoogleSignInSettings.CLIENT_SECRET_KEY] = clientSecret,
    };

    [Test]
    public async Task SignIn_WhenNotConfigured_ShouldReturn503()
    {
        using var response = await Client.GetAsync("/signin/google?returnTo=%2Fdecks");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Sign-in isn't set up on this server");
    }

    [Test]
    public async Task Options_WhenNotConfigured_ShouldNotOfferGoogle()
    {
        var options = JsonDocument.Parse(await Client.GetStringAsync("/api/signin/options")).RootElement;

        options.GetProperty("google").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Health_WhenNotConfigured_ShouldStillServe()
    {
        using var response = await Client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
