using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.Mcp;

// AGENTS.md asks for one raw JSON-RPC call per MCP tool, because McpTestClient tolerates bad shapes. Every tool tools/list
// returns runs one case from McpWireCases and its CallToolResult is read as the wire carries it.
[TestFixture]
[CancelAfter(60000)]
public class McpWireCoverageTests : ApiTestFixture
{
    public static IEnumerable<string> CaseNames => McpWireCases.All.Keys.Order(StringComparer.Ordinal);

    [Test]
    public async Task ToolList_WhenToolsListed_ShouldHaveExactlyOneWireCaseEach()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var schemas = await McpWire.InputSchemasAsync(Factory, agent.Tokens.AccessToken);

        var listed = schemas.Keys.ToList();
        var noCase = listed.Except(McpWireCases.All.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var noTool = McpWireCases.All.Keys.Except(listed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        noCase.ShouldBeEmpty($"tools/list returns tools with no case in McpWireCases: [{string.Join(", ", noCase)}]");
        noTool.ShouldBeEmpty($"McpWireCases names tools tools/list no longer returns: [{string.Join(", ", noTool)}]");
    }

    [Test]
    public void ExpectedErrorOnly_WhenListed_ShouldBeAtMostFiveWithAReasonEach()
    {
        McpWireCases.ExpectedErrorOnly.Count.ShouldBeLessThanOrEqualTo(5);
        McpWireCases.ExpectedErrorOnly.Keys.Order(StringComparer.Ordinal)
            .ShouldBe(McpWireCases.All.Where(c => c.Value.ErrorContains != null).Select(c => c.Key).Order(StringComparer.Ordinal));
        McpWireCases.ExpectedErrorOnly.Values.ShouldAllBe(reason => !string.IsNullOrWhiteSpace(reason));
    }

    [Test]
    public async Task ToolCase_WhenCalledOnTheWire_ShouldMatchTheCallToolResultShape([ValueSource(nameof(CaseNames))] string tool)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);
        var wireCase = McpWireCases.All[tool];

        var result = await seed.CallOnWireAsync(tool, await wireCase.Arguments(seed));

        var raw = $"{tool}: {result.GetRawText()}";
        result.GetProperty("content").ValueKind.ShouldBe(JsonValueKind.Array, raw);
        var isError = result.TryGetProperty("isError", out var flag) && flag.GetBoolean();
        if (wireCase.ErrorContains is { } expected)
        {
            isError.ShouldBeTrue(raw);
            McpWire.TextOf(result).ShouldContain(expected, Case.Insensitive, raw);
        }
        else
        {
            isError.ShouldBeFalse(raw);
            result.TryGetProperty("structuredContent", out var structured).ShouldBeTrue(raw);
            structured.ValueKind.ShouldBe(JsonValueKind.Object, raw);
            var failure = wireCase.ResultFailure?.Invoke(structured);
            failure.ShouldBeNull(raw);
        }
    }
}
