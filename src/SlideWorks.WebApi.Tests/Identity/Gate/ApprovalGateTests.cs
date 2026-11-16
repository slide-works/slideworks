using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;
using SlideWorks.WebApi.Infrastructure.Mcp;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Gate;

// The approval gate (S3): a pending person can use /api/me, sign-out and the anonymous routes, and gets 403
// pending_approval everywhere else, including behind a bare [Authorize]. Agents on /mcp never meet it.
[TestFixture]
public class ApprovalGateTests : ApiTestFixture
{
    private static readonly string[] KNOWN_POLICIES = [AgentPolicy.NAME, SignedInPolicy.NAME, AdminPolicy.NAME];

    protected override Action<IServiceCollection>? ConfigureTestServices => GateProbeController.AddTo;

    [Test]
    public async Task Api_WhenPending_ShouldReturn403PendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync("/api/decks");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    [Test]
    public async Task Me_WhenPending_ShouldReturn200()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Api_WhenApproved_ShouldPass()
    {
        var response = await Client.GetAsync("/api/decks");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Api_WhenSignedOut_ShouldStillReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/decks");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Not signed in");
    }

    [Test]
    public async Task BareAuthorize_WhenPending_ShouldReturn403PendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync(GateProbeController.BARE_PATH);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    // D2: the player is anonymous, so a pending person is a signed-in viewer there and IDeckAccess decides.
    [Test]
    public async Task Play_WhenPending_ShouldNotReturnPendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync("/api/play/unknown00deck");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
    }

    // An agent still gets in with its token, and a pending person's sign-in is no way into /mcp: the Agent policy's own
    // 401 challenge, never pending_approval.
    [Test]
    public async Task Mcp_ShouldBeUnaffected()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using var pending = ClientFor(TestUser.NewPending());

        var tools = await agent.Client.ListToolsAsync();
        using var response = await pending.PostAsync("/mcp", new StringContent("{}", Encoding.UTF8, "application/json"));

        tools.ShouldNotBeNull();
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer resource_metadata=");
    }

    // Every endpoint is AllowAnonymous, one of the known policies, or the gated default or fallback. A policy given by
    // roles, schemes or an inline AuthorizationPolicy could let a pending person past the gate unnoticed. Categories only,
    // never route names: new anonymous routes keep this green.
    [Test]
    public void EveryEndpoint_ShouldHaveAKnownPolicy()
    {
        var endpoints = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var unknown = endpoints.Where(endpoint => Category(endpoint) == null).Select(endpoint => endpoint.DisplayName).ToList();

        endpoints.ShouldNotBeEmpty();
        unknown.ShouldBeEmpty();
    }

    private static string? Category(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            return "AllowAnonymous";
        }
        if (endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count > 0)
        {
            return null;
        }
        var authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (authorize.Count == 0)
        {
            return "Fallback";
        }
        if (authorize.Any(data => !string.IsNullOrEmpty(data.Roles) || !string.IsNullOrEmpty(data.AuthenticationSchemes)))
        {
            return null;
        }
        var policies = authorize.Select(data => data.Policy).ToList();
        if (policies.All(policy => policy == null))
        {
            return "Default";
        }
        return policies.All(policy => policy == null || KNOWN_POLICIES.Contains(policy)) ? string.Join("+", policies.OfType<string>().Distinct()) : null;
    }
}
