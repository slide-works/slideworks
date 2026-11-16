using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// K6: a service exception in a tool comes back as a tool result with isError and the API's message, and a tool sees the
// agent as the actor (the bearer's claims read through ICurrentActor).
[TestFixture]
public class McpToolErrorsTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddMcpServer().WithTools<McpProbeTools>();

    [TestCase("not_found", "Deck not found")]
    [TestCase("bad_request", "slots.title: required")]
    [TestCase("conflict", "The slide changed since revision 3.")]
    public async Task Run_WhenServiceThrows_ShouldReturnToolErrorWithTheMessage(string kind, string message)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await agent.CallToolAsync("probe_throw", new { kind });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe(message);
    }

    [Test]
    public async Task Run_WhenWorkSucceeds_ShouldReturnItsJson()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await agent.CallToolAsync("probe_throw", new { kind = "none" });

        isError.ShouldBeFalse();
        json.GetInt32().ShouldBe(42);
    }

    [Test]
    public async Task Tool_WhenCalledWithAgentToken_ShouldSeeTheAgentActingForThePresenter()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Mira, "Review Bot");

        var (isError, json) = await agent.CallToolAsync("probe_whoami");

        isError.ShouldBeFalse();
        json.GetProperty("presenterId").GetGuid().ShouldBe(agent.PresenterId);
        json.GetProperty("email").GetString().ShouldBe(TestUser.Mira.Email);
        json.GetProperty("name").GetString().ShouldBe("Review Bot");
        json.GetProperty("kind").GetString().ShouldBe("Agent");
        json.GetProperty("via").GetString().ShouldBe("Mcp");
    }
}
