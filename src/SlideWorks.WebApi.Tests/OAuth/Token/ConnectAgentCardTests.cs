using System.Text.Json;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// The decks library's "connect an agent" card (connect.show) follows the presenter's agent tokens in the database: it hides
// once the presenter has a token that isn't revoked, and comes back after a revoke. A token that has only expired keeps it
// hidden: the card is first-connection help, and an agent that once connected runs OAuth again by itself when its token
// stops working. Its own fixture, because the expiry test moves the clock thirty days.
[TestFixture]
public class ConnectAgentCardTests : ApiTestFixture
{
    private HttpClient _anonymous = null!;
    private TokenEndpoint _endpoint = null!;
    private string _clientId = null!;

    [OneTimeSetUp]
    public async Task RegisterClient()
    {
        _anonymous = ClientFor(TestUser.Anonymous);
        _endpoint = new TokenEndpoint(Factory, _anonymous);
        _clientId = await OAuthFlow.RegisterAsync(_anonymous);
    }

    [OneTimeTearDown]
    public void DisposeAnonymous() => _anonymous.Dispose();

    [Test]
    public async Task Library_WhenAgentConnectedThenRevoked_ShouldHideThenShowConnect()
    {
        (await ConnectShownAsync(TestUser.Mira)).ShouldBeTrue("Mira has no agent yet");
        var (access, _) = await _endpoint.TokensAsync(_clientId, TestUser.Mira);

        (await ConnectShownAsync(TestUser.Mira)).ShouldBeFalse("Mira's agent has a live token");

        await _endpoint.RevokeAsync(access);
        (await ConnectShownAsync(TestUser.Mira)).ShouldBeTrue("Mira's only token family was revoked");
    }

    [Test]
    public async Task Library_WhenTokensExpiredButNotRevoked_ShouldKeepConnectHidden()
    {
        await _endpoint.TokensAsync(_clientId, TestUser.Alex);

        Clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));

        (await ConnectShownAsync(TestUser.Alex)).ShouldBeFalse();
    }

    private async Task<bool> ConnectShownAsync(TestUser person)
    {
        using var client = ClientFor(person);
        var response = await client.GetAsync("/api/decks");
        var library = await TokenEndpoint.OkBodyAsync(response);
        return library.GetProperty("connect").GetProperty("show").ValueKind == JsonValueKind.True;
    }
}
