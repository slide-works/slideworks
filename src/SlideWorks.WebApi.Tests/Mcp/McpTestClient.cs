using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// An MCP client connected to a test host's /mcp as an agent acting for a TestUser. It takes the seams an OAuth flow would
// end in: the person signs in through TestPeople, a client registered through IOAuthClients, and a token issued
// through IAgentTokens.IssueAsync, all from the app's own container. Requests go through the factory's in-memory handler.
public sealed class McpTestClient : IAsyncDisposable
{
    private const string SCOPE = "decks";
    private const string REDIRECT = "http://127.0.0.1:33418/callback";

    private static readonly JsonSerializerOptions ARGS_JSON = new(JsonSerializerDefaults.Web);

    private McpTestClient(McpClient client, Guid presenterId, OAuthClient oauthClient, AgentTokenPair tokens)
    {
        Client = client;
        PresenterId = presenterId;
        OAuthClient = oauthClient;
        Tokens = tokens;
    }

    public McpClient Client { get; }

    // The User.Id of the person the agent acts for.
    public Guid PresenterId { get; }

    public OAuthClient OAuthClient { get; }

    public AgentTokenPair Tokens { get; }

    public static async Task<McpTestClient> ConnectAsAsync(WebApplicationFactory<ApiMarker> factory, TestUser user, string clientName = "Claude")
    {
        if (user.IsAnonymous)
        {
            throw new ArgumentException("An agent always acts for a signed-in person; pick a TestUser other than Anonymous.", nameof(user));
        }
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var person = await TestPeople.SignInAsync(services, user);
        var oauthClient = await services.GetRequiredService<IOAuthClients>().RegisterAsync(new ClientRegistration(clientName, [REDIRECT]));
        var mcpUrl = services.GetRequiredService<IPublicUrls>().Mcp;
        var tokens = await services.GetRequiredService<IAgentTokens>().IssueAsync(new CodeGrant(oauthClient.ClientId, person.Id, SCOPE, mcpUrl));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(mcpUrl),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {tokens.AccessToken}" },
            },
            factory.CreateClient(),
            ownsHttpClient: true);
        var client = await McpClient.CreateAsync(transport);
        return new McpTestClient(client, person.Id, oauthClient, tokens);
    }

    // Calls a tool with args given as an object (an anonymous object is fine) and returns whether it was a tool error, plus
    // its JSON: the structured result on success, or the error message as a JSON string on an error.
    public async Task<(bool IsError, JsonElement Json)> CallToolAsync(string name, object? args = null)
    {
        var arguments = args == null
            ? new Dictionary<string, object?>()
            : JsonSerializer.SerializeToElement(args, ARGS_JSON).EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        var result = await Client.CallToolAsync(name, arguments);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        if (result.IsError == true)
        {
            return (true, JsonSerializer.SerializeToElement(text));
        }
        return (false, result.StructuredContent is { } structured ? structured : JsonDocument.Parse(text).RootElement.Clone());
    }

    public ValueTask DisposeAsync() => Client.DisposeAsync();
}
