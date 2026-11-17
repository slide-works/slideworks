using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.LoadHost;

// A deck for a load run, made the way a person and an agent make one: POST /api/decks as the owner, add_slide over MCP
// (slide 1 a quote, slide 2 a poll with its default settings: results live, opens on arrival), then Go live. The harness
// then joins the presentation as the owner (--owner-cookie with OWNER_COOKIE) and moves to the poll slide, so the presentation
// registry and the poll arrival rule open round 1 as they do for a person in the presenter window.
public sealed record LoadSeed(string DeckId, string Code)
{
    public const int POLL_SLIDE = 2;

    // The test sign-in scheme reads this cookie as well as its header, so the harness can act as the owner.
    public static string OwnerCookie => $"{TestAuthScheme.COOKIE}={OWNER.Email}";

    public static readonly TestUser OWNER = TestUser.Ada;

    private const string REDIRECT = "http://127.0.0.1:33418/callback";

    public static async Task<LoadSeed> CreateAsync(LoadHostApp app)
    {
        using var owner = new HttpClient { BaseAddress = new Uri(app.BaseUrl) };
        owner.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, OWNER.Email);
        // A signed-in write needs the app's own Origin (CrossSiteWriteGuard), as the browser sends it.
        owner.DefaultRequestHeaders.Add("Origin", app.BaseUrl);
        var created = await owner.PostAsJsonAsync("/api/decks", new { title = "Load run" });
        created.EnsureSuccessStatusCode();
        var deckId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await using (var agent = await ConnectAgentAsync(app))
        {
            await AddSlideAsync(agent, deckId, "quote", new { quote = "Small batches ship sooner", attribution = OWNER.Name });
            await AddSlideAsync(agent, deckId, "poll", new
            {
                question = "How big was your last merge request?",
                options = new[] { "Under 50 lines", "50 to 200 lines", "Over 200 lines", "I don't remember" },
            });
        }

        var live = await owner.PostAsync($"/api/decks/{deckId}/live", null);
        live.EnsureSuccessStatusCode();
        var code = (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        return new LoadSeed(deckId, code);
    }

    private static async Task AddSlideAsync(McpClient agent, string deckId, string layout, object slots)
    {
        var result = await agent.CallToolAsync("add_slide", new Dictionary<string, object?>
        {
            ["deck"] = deckId,
            ["layout"] = layout,
            ["slots"] = JsonSerializer.SerializeToElement(slots),
        });
        if (result.IsError ?? false)
        {
            throw new InvalidOperationException($"add_slide {layout} failed.");
        }
    }

    // The agent's token comes from the seams an OAuth flow ends in, as AgentSeed does for the acceptance app.
    private static async Task<McpClient> ConnectAgentAsync(LoadHostApp app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var person = await TestPeople.SignInAsync(services, OWNER);
        var client = await services.GetRequiredService<IOAuthClients>().RegisterAsync(new ClientRegistration("Load", [REDIRECT]));
        var mcpUrl = services.GetRequiredService<IPublicUrls>().Mcp;
        var tokens = await services.GetRequiredService<IAgentTokens>().IssueAsync(new CodeGrant(client.ClientId, person.Id, "decks", mcpUrl));
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(mcpUrl),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {tokens.AccessToken}" },
        });
        return await McpClient.CreateAsync(transport);
    }
}
