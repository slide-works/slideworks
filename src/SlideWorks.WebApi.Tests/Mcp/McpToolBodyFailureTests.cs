using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// McpArgumentCheck turns a binding failure into "arguments: don't fit the tool's parameters", but only a binding
// failure. A JsonException from a tool's body, after its write may have committed, is a bug: the SDK logs it and reports
// it as one. This host adds one test-only tool whose body throws a JsonException inside McpToolErrors.Run, like a result
// that can't be serialised would.
[TestFixture]
[CancelAfter(60000)]
public class McpToolBodyFailureTests : ApiTestFixture
{
    private const string TOOL = "fails_after_binding";

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton(
        McpServerTool.Create(
            (string deck) => McpToolErrors.Run<object>(() => throw new JsonException($"{deck} could not be written out")),
            new McpServerToolCreateOptions { Name = TOOL }));

    [Test]
    public async Task Call_WhenToolBodyThrowsJsonException_ShouldNotBeReportedAsAnArgumentError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, TOOL, """{"deck":"d"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldNotBe(McpArgumentCheck.BINDING_FAILED, result.GetRawText());
        McpWire.TextOf(result).ShouldBe($"An error occurred invoking '{TOOL}'.", result.GetRawText());
    }
}
