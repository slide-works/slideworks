using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Decks;
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
        json.GetProperty("answer").GetInt32().ShouldBe(42);
        json.GetProperty("items").EnumerateArray().Select(item => item.GetString()).ShouldBe(["a", "b"]);
    }

    // The raw JSON-RPC response, not the C# client's view of it: the C# client accepts a structuredContent that is not an
    // object, but the MCP spec types it as an object and clients built on the TypeScript SDK refuse the whole result.
    [Test]
    public async Task Run_WhenResultIsAnObject_ShouldSendItAsStructuredContentAndText()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "probe_throw", """{"kind":"none"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse();
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.GetRawText().ShouldBe("""{"answer":42,"items":["a","b"]}""");
        McpWire.TextOf(result).ShouldBe("""{"answer":42,"items":["a","b"]}""");
    }

    [TestCase("array", """["a","b"]""")]
    [TestCase("number", "42")]
    [TestCase("string", "\"forty-two\"")]
    [TestCase("null", "null")]
    public async Task Run_WhenResultIsNotAnObject_ShouldSendItAsTextOnly(string kind, string text)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "probe_non_object", $$"""{"kind":"{{kind}}"}""");

        result.TryGetProperty("structuredContent", out _).ShouldBeFalse(result.GetRawText());
        McpWire.TextOf(result).ShouldBe(text);
    }

    [Test]
    public async Task Run_WhenResultIsAnArray_ShouldStillReachTheCSharpClientAsJson()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await agent.CallToolAsync("probe_non_object", new { kind = "array" });

        isError.ShouldBeFalse();
        json.EnumerateArray().Select(item => item.GetString()).ShouldBe(["a", "b"]);
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
        json.GetProperty("kind").GetString().ShouldBe("agent");
        json.GetProperty("via").GetString().ShouldBe("mcp");
    }
}
