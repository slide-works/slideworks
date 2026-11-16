using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Mcp;

// What McpArgumentCheck answers for a request the SDK can't read outside a tool's arguments: a lone surrogate in the tool
// name, in _meta or another params key, in tools/list's cursor or in ping's params, and a tools/call whose arguments
// isn't an object. Without the check every one of these is JSON-RPC -32603 "An error occurred.", which names nothing. A
// tools/call to a tool that exists gets a tool error naming the field; a call to an unknown tool still gets the SDK's
// unknown-tool error; any other request gets -32602 naming the field. All on the raw JSON-RPC wire, because the C#
// client never sends such requests.
// Not covered: a name in params or in _meta that is itself the bad text. The HTTP transport reads params._meta before
// any filter runs, so that is still the SDK's -32603.
[TestFixture]
[CancelAfter(60000)]
public class McpUnreadableParamsTests : ApiTestFixture
{
    private const int INVALID_PARAMS = -32602;

    private const string ADD = """{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"}}""";

    private const string NOT_AN_OBJECT = "arguments: must be an object";

    // Each case is the params of a tools/call, against a deck holding one quote slide "A".
    [TestCase("""{"name":"add_slide\ud83d","arguments":{{ADD}}}""", "name: text is not valid UTF-16")]
    [TestCase("""{"name":"\ud83d","arguments":{}}""", "name: text is not valid UTF-16")]
    [TestCase("""{"name":"add_slide","arguments":{{ADD}},"_meta":{"progressToken":"\ud83d"}}""", "_meta.progressToken: text is not valid UTF-16")]
    [TestCase("""{"name":"add_slide","arguments":{{ADD}},"extra":"\ud83d"}""", "extra: text is not valid UTF-16")]
    [TestCase("""{"name":"add_slide","arguments":"{\"deck\":\"{deck}\",\"layout\":\"quote\"}"}""", NOT_AN_OBJECT)]
    [TestCase("""{"name":"add_slide","arguments":[{{ADD}}]}""", NOT_AN_OBJECT)]
    [TestCase("""{"name":"add_slide","arguments":7}""", NOT_AN_OBJECT)]
    [TestCase("""{"name":"add_slide","arguments":true}""", NOT_AN_OBJECT)]
    [TestCase("""{"name":"list_decks","arguments":[]}""", NOT_AN_OBJECT)]
    // An unknown argument is refused as unknown even when it also holds text that isn't valid UTF-16.
    [TestCase("""{"name":"add_slide","arguments":{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"bogus":"\ud83d"}}""", "bogus: unknown argument")]
    [TestCase("""{"name":"add_slide","arguments":{"deck":"{deck}","layout":"quote","slots":{"quote":"B","attribution":"Sam"},"bogus":{"x":["\ud83d"]}}}""", "bogus: unknown argument")]
    public async Task Call_WhenParamsUnreadableOrArgumentsNotAnObject_ShouldReturnToolErrorNamingItAndChangeNothing(string paramsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");

        var message = await SendAsync(agent, "tools/call", paramsJson.Replace("{{ADD}}", ADD, StringComparison.Ordinal).Replace("{deck}", deck, StringComparison.Ordinal));

        message.TryGetProperty("result", out var result).ShouldBeTrue(message.GetRawText());
        result.GetProperty("isError").GetBoolean().ShouldBeTrue(message.GetRawText());
        McpWire.TextOf(result).ShouldBe(expected, message.GetRawText());
        McpWire.TextOf(result).ShouldContain(expected, Case.Sensitive);
        var slides = await SlidesAsync(agent, deck);
        Quotes(slides).ShouldBe(["A"]);
        Revisions(slides).ShouldBe([1]);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(2);
    }

    // A tool that doesn't exist is the error to report, whatever else is wrong with the call.
    [TestCase("""{"name":"nope","arguments":{"a":"\ud83d"}}""")]
    [TestCase("""{"name":"nope","arguments":{"\ud83d":1}}""")]
    [TestCase("""{"name":"nope","arguments":"{\"a\":1}"}""")]
    [TestCase("""{"name":"nope","arguments":[1]}""")]
    [TestCase("""{"name":"nope","arguments":{},"_meta":{"progressToken":"\ud83d"}}""")]
    [TestCase("""{"name":"nope","arguments":{},"extra":"\ud83d"}""")]
    public async Task Call_WhenToolUnknownAndParamsUnreadable_ShouldReturnUnknownToolError(string paramsJson)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var message = await SendAsync(agent, "tools/call", paramsJson);

        message.TryGetProperty("result", out _).ShouldBeFalse(message.GetRawText());
        var error = message.GetProperty("error");
        error.GetProperty("code").GetInt32().ShouldBe(INVALID_PARAMS, message.GetRawText());
        error.GetProperty("message").GetString().ShouldBe("Unknown tool: 'nope'", message.GetRawText());
    }

    // Not a tool call, so there's no tool error to give: JSON-RPC -32602 naming the field, not -32603.
    [TestCase("tools/list", """{"cursor":"\ud83d"}""", "cursor: text is not valid UTF-16")]
    [TestCase("tools/list", """{"_meta":{"progressToken":"\ud83d"}}""", "_meta.progressToken: text is not valid UTF-16")]
    [TestCase("ping", """{"x":"\ud83d"}""", "x: text is not valid UTF-16")]
    [TestCase("ping", """{"x":[{"y":"\ud83d"}]}""", "x[0].y: text is not valid UTF-16")]
    public async Task Request_WhenParamsUnreadable_ShouldReturnInvalidParamsNamingIt(string method, string paramsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var message = await SendAsync(agent, method, paramsJson);

        message.TryGetProperty("result", out _).ShouldBeFalse(message.GetRawText());
        var error = message.GetProperty("error");
        error.GetProperty("code").GetInt32().ShouldBe(INVALID_PARAMS, message.GetRawText());
        error.GetProperty("message").GetString().ShouldBe(expected, message.GetRawText());
        error.GetProperty("message").GetString()!.ShouldContain(expected, Case.Sensitive);
    }

    // The check must leave readable requests alone.
    [TestCase("ping", """{}""")]
    [TestCase("ping", """{"_meta":{"progressToken":"p\ud83d\ude80"}}""")]
    [TestCase("tools/list", """{"_meta":{"progressToken":"🚀"}}""")]
    public async Task Request_WhenParamsReadable_ShouldBeAnswered(string method, string paramsJson)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var message = await SendAsync(agent, method, paramsJson);

        message.TryGetProperty("result", out _).ShouldBeTrue(message.GetRawText());
    }

    [Test]
    public async Task Call_WhenMetaReadable_ShouldRunTheTool()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOfQuotesAsync(agent, "A");

        var message = await SendAsync(agent, "tools/call", $$$"""{"name":"add_slide","arguments":{{{ADD.Replace("{deck}", deck, StringComparison.Ordinal)}}},"_meta":{"progressToken":"p\ud83d\ude80"}}""");

        var result = message.GetProperty("result");
        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(message.GetRawText());
        Quotes(await SlidesAsync(agent, deck)).ShouldBe(["A", "B"]);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("private");
    }

    private Task<JsonElement> SendAsync(McpTestClient agent, string method, string paramsJson)
        => McpWire.SendAsync(Factory, agent.Tokens.AccessToken, $$$"""{"jsonrpc":"2.0","id":7,"method":"{{{method}}}","params":{{{paramsJson}}}}""");
}
