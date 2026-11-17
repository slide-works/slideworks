using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// Only an argument a tool declares as an object is advertised and checked as one (slots, a JsonObject parameter). An
// argument whose schema has no type (any JSON, anyOf, enum, $ref) is left as the tool wrote it, both in tools/list and in
// McpArgumentCheck. This host adds one test-only tool with a hand-written schema of each kind.
[TestFixture]
[CancelAfter(60000)]
public class McpUntypedArgumentTests : ApiTestFixture
{
    private const string TOOL = "takes_untyped";

    private static readonly string[] ARGUMENTS = ["anything", "choice", "pick", "linked"];

    private const string SCHEMA = """
        {
          "type": "object",
          "properties": {
            "anything": { "description": "Any JSON." },
            "choice": { "anyOf": [ { "type": "string" }, { "type": "array", "items": { "type": "string" } } ] },
            "pick": { "enum": [ "a", 1, [ "b" ] ] },
            "linked": { "$ref": "#/$defs/linked" }
          },
          "$defs": { "linked": { "type": "string" } }
        }
        """;

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton(_ =>
    {
        var tool = McpServerTool.Create(
            (JsonElement? anything = null, JsonElement? choice = null, JsonElement? pick = null, JsonElement? linked = null)
                => McpToolErrors.Run(() => Task.FromResult(new { kinds = $"{anything?.ValueKind} {choice?.ValueKind} {pick?.ValueKind} {linked?.ValueKind}" })),
            new McpServerToolCreateOptions { Name = TOOL });
        using var schema = JsonDocument.Parse(SCHEMA);
        tool.ProtocolTool.InputSchema = schema.RootElement.Clone();
        return tool;
    });

    [Test]
    public async Task ToolsList_WhenArgumentSchemaHasNoType_ShouldAdvertiseItUnchanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var schemas = await McpWire.InputSchemasAsync(Factory, agent.Tokens.AccessToken);

        using var written = JsonDocument.Parse(SCHEMA);
        var properties = schemas[TOOL].GetProperty("properties");
        foreach (var argument in ARGUMENTS)
        {
            properties.GetProperty(argument).GetRawText().ShouldBe(
                JsonSerializer.Serialize(written.RootElement.GetProperty("properties").GetProperty(argument)),
                argument);
        }
        schemas[TOOL].GetProperty("additionalProperties").ValueKind.ShouldBe(JsonValueKind.False);
    }

    [TestCase("""{"anything":"text","choice":"text","pick":"a","linked":"text"}""", "String String String String")]
    [TestCase("""{"anything":["a",1],"choice":["a","b"],"pick":1,"linked":"text"}""", "Array Array Number String")]
    [TestCase("""{"anything":7,"pick":["b"]}""", "Number  Array ")]
    public async Task Call_WhenArgumentSchemaHasNoType_ShouldNotRefuseAnyKind(string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, TOOL, argumentsJson);

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("structuredContent").GetProperty("kinds").GetString().ShouldBe(expected, result.GetRawText());
    }

    [Test]
    public async Task Call_WhenUntypedToolGetsAnUnknownArgument_ShouldStillRefuseIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, TOOL, """{"anything":"x","bogus":1}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldBe("bogus: unknown argument", result.GetRawText());
    }
}
