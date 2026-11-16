using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Authorize;

// The consent API behind /oauth/consent: only the presenter who started the request sees and answers it, once, within
// ten minutes; the answer goes back to the client with its state. Under /api, so the approval gate and the cross-site
// write guard apply.
[TestFixture]
public class ConsentTests : ApiTestFixture
{
    private string _clientId = null!;

    [OneTimeSetUp]
    public async Task RegisterClient()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        _clientId = await OAuthFlow.RegisterAsync(anonymous);
    }

    [Test]
    public async Task Consent_WhenRequestIsTheirs_ShouldShowClientNameAndScope()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);

        var response = await Client.GetAsync(ConsentPath(requestId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        JsonShape.AssertMatches(body, "consent.json");
        body.GetProperty("clientName").GetString().ShouldBe("Claude");
        body.GetProperty("scope").GetString().ShouldBe("decks");
    }

    [Test]
    public async Task Consent_WhenApproved_ShouldRedirectWithCodeAndState()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);

        var response = await AnswerAsync(Client, requestId, approve: true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var query = OAuthFlow.CallbackQuery(await RedirectAsync(response));
        query["state"].ShouldBe(OAuthFlow.STATE);
        query.ContainsKey("error").ShouldBeFalse();
        var grant = await RedeemAsync(query["code"]);
        grant.ShouldNotBeNull();
        grant.ClientId.ShouldBe(_clientId);
        grant.Scope.ShouldBe("decks");
        grant.Resource.ShouldBe(OAuthFlow.Resource);
    }

    [Test]
    public async Task Consent_WhenDenied_ShouldRedirectWithAccessDenied()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);

        var response = await AnswerAsync(Client, requestId, approve: false);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var query = OAuthFlow.CallbackQuery(await RedirectAsync(response));
        query["error"].ShouldBe("access_denied");
        query["state"].ShouldBe(OAuthFlow.STATE);
        query.ContainsKey("code").ShouldBeFalse();
    }

    [Test]
    public async Task Consent_WhenExpired_ShouldReturn410()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);
        Clock.Advance(TimeSpan.FromMinutes(10));

        var shown = await Client.GetAsync(ConsentPath(requestId));
        var answered = await AnswerAsync(Client, requestId, approve: true);

        shown.StatusCode.ShouldBe(HttpStatusCode.Gone);
        answered.StatusCode.ShouldBe(HttpStatusCode.Gone);
        (await shown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Consent_WhenAnsweredTwice_ShouldReturn410()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);
        (await AnswerAsync(Client, requestId, approve: true)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await AnswerAsync(Client, requestId, approve: true);
        var shown = await Client.GetAsync(ConsentPath(requestId));

        again.StatusCode.ShouldBe(HttpStatusCode.Gone);
        shown.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    // Another presenter can neither see nor answer Sam's request, and trying leaves it for Sam.
    [Test]
    public async Task Consent_WhenOtherPresenter_ShouldReturn410()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);
        using var alex = ClientFor(TestUser.Alex);

        var shown = await alex.GetAsync(ConsentPath(requestId));
        var answered = await AnswerAsync(alex, requestId, approve: true);

        shown.StatusCode.ShouldBe(HttpStatusCode.Gone);
        answered.StatusCode.ShouldBe(HttpStatusCode.Gone);
        (await AnswerAsync(Client, requestId, approve: true)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Consent_WhenUnknownRequest_ShouldReturn410()
    {
        var response = await Client.GetAsync(ConsentPath("no-such-request"));

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Test]
    public async Task Consent_WhenForeignOrigin_ShouldReturn403()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);
        using var request = new HttpRequestMessage(HttpMethod.Post, ConsentPath(requestId)) { Content = JsonContent.Create(new { approve = true }) };
        request.Headers.Remove("Origin");
        request.Headers.Add("Origin", "https://attacker.example");
        using var sam = ClientWithoutOrigin(TestUser.Sam);

        var response = await sam.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await AnswerAsync(Client, requestId, approve: true)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Consent_WhenPending_ShouldReturn403PendingApproval()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);
        using var pending = ClientFor(TestUser.NewPending());

        var shown = await pending.GetAsync(ConsentPath(requestId));
        var answered = await AnswerAsync(pending, requestId, approve: true);

        shown.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await shown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("pending_approval");
        answered.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Consent_WhenSignedOut_ShouldReturn401()
    {
        var requestId = await OAuthFlow.StartAsync(Client, _clientId);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync(ConsentPath(requestId));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string ConsentPath(string requestId) => "/api/oauth/consent/" + Uri.EscapeDataString(requestId);

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, string requestId, bool approve)
        => client.PostAsJsonAsync(ConsentPath(requestId), new { approve });

    private static async Task<string> RedirectAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirect").GetString().ShouldNotBeNull();

    // The code is checked by redeeming it as the token endpoint would, with the client's verifier.
    private async Task<CodeGrant?> RedeemAsync(string code)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IOAuthCodes>()
            .RedeemAsync(code, _clientId, OAuthFlow.REDIRECT, OAuthFlow.VERIFIER, OAuthFlow.Resource);
    }

    private HttpClient ClientWithoutOrigin(TestUser user)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, user.Email);
        return client;
    }
}
