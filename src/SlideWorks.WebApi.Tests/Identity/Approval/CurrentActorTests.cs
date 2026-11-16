using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Approval;

// ICurrentActor's Access and IsAdmin (S2) for an agent: always approved (its presenter has to be, S4), never an admin,
// even when it acts for one.
[TestFixture]
public class CurrentActorTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => ActorProbeController.AddTo;

    [Test]
    public async Task Agent_ShouldBeApprovedNotAdmin()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Ada);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ActorProbeController.AGENT_PATH);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.Tokens.AccessToken);

        using var response = await http.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var actor = (await response.Content.ReadFromJsonAsync<ActorDto>())!;
        actor.Kind.ShouldBe("Agent");
        actor.Email.ShouldBe(TestUser.Ada.Email);
        actor.Access.ShouldBe("Approved");
        actor.IsAdmin.ShouldBeFalse();
    }
}
