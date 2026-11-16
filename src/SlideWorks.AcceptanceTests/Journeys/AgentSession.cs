using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// An agent connected to the running app's /mcp over Streamable HTTP with the access token its OAuth client got, acting
// for one presenter. Tools are called with plain JSON arguments, as an agent's model writes them.
public sealed class AgentSession : IAsyncDisposable
{
    private static readonly JsonSerializerOptions ARGS_JSON = new(JsonSerializerDefaults.Web);

    private readonly McpClient _client;

    private AgentSession(McpClient client) => _client = client;

    // Sends the presenter's browser through the OAuth flow for oauthClient, then connects to /mcp with the token.
    public static async Task<AgentSession> ConnectAsync(AgentOAuthClient oauthClient, IPage browser, TestUser presenter)
    {
        var accessToken = await oauthClient.AuthorizeAsync(browser, presenter);
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(oauthClient.Resource),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" },
        });
        return new AgentSession(await McpClient.CreateAsync(transport));
    }

    public async Task<IReadOnlyList<string>> ToolNamesAsync() =>
        (await _client.ListToolsAsync()).Select(tool => tool.Name).ToList();

    // Calls a tool and returns its structured result. A tool error fails the test with the tool's own message.
    public async Task<JsonElement> CallAsync(string tool, object args)
    {
        var arguments = JsonSerializer.SerializeToElement(args, ARGS_JSON)
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value);
        var result = await _client.CallToolAsync(tool, arguments);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        (result.IsError ?? false).ShouldBeFalse($"{tool} failed: {text}");
        return result.StructuredContent.ShouldNotBeNull($"{tool} returned no structured content: {text}");
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
