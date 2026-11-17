using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// An admin revokes a person while that person's agent redeems a code. The revoke ends the agent tokens, then makes the
// person Rejected, then ends their tokens again: a redeem that lands between the first two steps still sees them
// Approved and is issued tokens, and without the second step approving the person again would bring those tokens back.
// The redeem is placed in that gap by InterleavedRevoke, on this fixture's own host.
[TestFixture]
public class AdminRevokeRaceTests : ApiTestFixture
{
    private HttpClient _anonymous = null!;
    private TokenEndpoint _endpoint = null!;
    private string _clientId = null!;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => InterleavedRevoke.AddTo;

    [OneTimeSetUp]
    public async Task RegisterClient()
    {
        _anonymous = ClientFor(TestUser.Anonymous);
        _endpoint = new TokenEndpoint(Factory, _anonymous);
        _clientId = await OAuthFlow.RegisterAsync(_anonymous, "Claude");
    }

    [OneTimeTearDown]
    public void DisposeAnonymous() => _anonymous.Dispose();

    [Test]
    public async Task Revoke_WhenCodeRedeemedDuringTheRevoke_ShouldLeaveNoTokensToRevive()
    {
        var presenter = TestUser.NewPending();
        using (var signedIn = ClientFor(presenter))
        {
            (await signedIn.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using var ada = ClientFor(TestUser.Ada);
        var id = await AdminUserIdAsync(ada, presenter.Email);
        (await ada.PostAsync($"/api/admin/users/{id}/approve", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var code = await _endpoint.CodeAsync(_clientId, presenter);
        string? access = null;
        Factory.Services.GetRequiredService<RevokeInterleave>().Arm(async () =>
        {
            var body = await TokenEndpoint.OkBodyAsync(await _endpoint.RedeemAsync(_clientId, code));
            access = body.GetProperty("access_token").GetString();
        });

        (await ada.PostAsync($"/api/admin/users/{id}/revoke", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        access.ShouldNotBeNull("the redeem should have run inside the revoke, while the person was still approved");
        (await ada.PostAsync($"/api/admin/users/{id}/approve", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
        using var approvedAgain = ClientFor(presenter);
        var library = await TokenEndpoint.OkBodyAsync(await approvedAgain.GetAsync("/api/decks"));
        library.GetProperty("connect").GetProperty("show").GetBoolean().ShouldBeTrue();
    }

    private static async Task<Guid> AdminUserIdAsync(HttpClient admin, string email)
    {
        var view = await admin.GetFromJsonAsync<JsonElement>("/api/admin/users");
        return view.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();
    }
}
