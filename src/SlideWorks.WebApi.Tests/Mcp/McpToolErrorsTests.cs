using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        json.GetProperty("answer").GetInt32().ShouldBe(42);
        json.GetProperty("items").EnumerateArray().Select(item => item.GetString()).ShouldBe(["a", "b"]);
    }

    // The raw JSON-RPC response, not the C# client's view of it: the C# client accepts a structuredContent that is not an
    // object, but the MCP spec types it as an object and clients built on the TypeScript SDK refuse the whole result.
    [Test]
    public async Task Run_WhenResultIsAnObject_ShouldSendItAsStructuredContentAndText()
    {
        var result = await CallToolOnTheWireAsync("probe_throw", """{"kind":"none"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse();
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.GetRawText().ShouldBe("""{"answer":42,"items":["a","b"]}""");
        TextOf(result).ShouldBe("""{"answer":42,"items":["a","b"]}""");
    }

    [TestCase("array", """["a","b"]""")]
    [TestCase("number", "42")]
    [TestCase("string", "\"forty-two\"")]
    [TestCase("null", "null")]
    public async Task Run_WhenResultIsNotAnObject_ShouldSendItAsTextOnly(string kind, string text)
    {
        var result = await CallToolOnTheWireAsync("probe_non_object", $$"""{"kind":"{{kind}}"}""");

        result.TryGetProperty("structuredContent", out _).ShouldBeFalse(result.GetRawText());
        TextOf(result).ShouldBe(text);
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

    // POSTs tools/call to /mcp as an agent and returns the JSON-RPC result exactly as it came over the wire.
    private async Task<JsonElement> CallToolOnTheWireAsync(string tool, string argumentsJson)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $$$"""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{argumentsJson}}}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.Tokens.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        await TestContext.Out.WriteLineAsync(body);

        // A streamable HTTP response is either one JSON body or an event stream whose data line holds the message.
        var message = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
            ? body.Split('\n').Select(line => line.TrimEnd('\r')).Last(line => line.StartsWith("data:", StringComparison.Ordinal))["data:".Length..]
            : body;
        var root = JsonDocument.Parse(message).RootElement.Clone();
        root.GetProperty("id").GetInt32().ShouldBe(7);
        return root.GetProperty("result");
    }

    private static string TextOf(JsonElement result)
        => result.GetProperty("content").EnumerateArray().Single(block => block.GetProperty("type").GetString() == "text").GetProperty("text").GetString()!;
}
