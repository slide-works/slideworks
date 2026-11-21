using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
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

    // Calls a tool and returns its JSON result, failing the test with the tool's text when it is a tool error.
    public static async Task<JsonElement> CallAsync(McpClient agent, string tool, Dictionary<string, object?> args)
    {
        var result = await agent.CallToolAsync(tool, args);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        (result.IsError ?? false).ShouldBeFalse($"{tool} failed: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    // Follows a preview_deck result the way its instructions tell an agent to, as the equivalent Playwright calls: a
    // fresh anonymous context at the result's viewport, render_url with {slide} filled in for each number until wait_for matches, then a viewport
    // screenshot. Fails on a page that shows data-error. Returns each slide's PNG and the URL the page ended on, by its number;
    // the test asserts on the URL (for example, that it has no fragment).
    public static async Task<Dictionary<int, PreviewShot>> ScreenshotPreviewAsync(IBrowser browser, JsonElement preview)
    {
        var viewport = preview.GetProperty("viewport");
        var waitFor = preview.GetProperty("wait_for").GetString()!;
        var instructions = preview.GetProperty("instructions").GetString()!;
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = viewport.GetProperty("width").GetInt32(), Height = viewport.GetProperty("height").GetInt32() },
        });
        var template = preview.GetProperty("render_url").GetString()!;
        instructions.ShouldContain($"\"{template.Replace("{slide}", "$n")}\"");
        var shots = new Dictionary<int, PreviewShot>();
        foreach (var number in preview.GetProperty("slides").EnumerateArray().Select(s => s.GetInt32()))
        {
            var url = template.Replace("{slide}", number.ToString());
            var page = await context.NewPageAsync();
            await page.GotoAsync(url);
            await page.WaitForSelectorAsync(waitFor);
            (await page.GetByTestId("render-slide").GetAttributeAsync("data-error")).ShouldBeNull();
            shots[number] = new PreviewShot(await page.ScreenshotAsync(), page.Url);
        }
        return shots;
    }

    // A PNG's width and height, from its IHDR chunk.
    public static (int Width, int Height) PngSize(byte[] png)
        => (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
}
