using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// Slides for a test deck, added the way an agent adds them: MCP add_slide over real HTTP at the running app's /mcp.
// The agent's token comes from the seams an OAuth flow ends in (a registered client and IAgentTokens.IssueAsync), as
// WebApi.Tests' McpTestClient does; the slides themselves only go in through the public tool.
public static class AgentSeed
{
    private const string SCOPE = "decks";
    private const string REDIRECT = "http://127.0.0.1:33418/callback";

    // Adds a quote slide per text to the presenter's deck, in order.
    public static async Task AddQuotesAsync(TestUser presenter, string deckId, params string[] quotes)
    {
        await using var client = await ConnectAsync(presenter);
        foreach (var quote in quotes)
        {
            var slots = JsonSerializer.SerializeToElement(new { quote, attribution = presenter.Name });
            var result = await client.CallToolAsync(
                "add_slide",
                new Dictionary<string, object?> { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = slots });
            (result.IsError ?? false).ShouldBeFalse($"add_slide failed for '{quote}'.");
        }
    }

    public static async Task<McpClient> ConnectAsync(TestUser presenter)
    {
        using var scope = AcceptanceApp.Services.CreateScope();
        var services = scope.ServiceProvider;
        var person = await TestPeople.SignInAsync(services, presenter);
        var oauthClient = await services.GetRequiredService<IOAuthClients>().RegisterAsync(new ClientRegistration("Claude", [REDIRECT]));
        var mcpUrl = services.GetRequiredService<IPublicUrls>().Mcp;
        var tokens = await services.GetRequiredService<IAgentTokens>().IssueAsync(new CodeGrant(oauthClient.ClientId, person.Id, SCOPE, mcpUrl));
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(mcpUrl),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {tokens.AccessToken}" },
        });
        return await McpClient.CreateAsync(transport);
    }
}
