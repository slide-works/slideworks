using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// L7 and D21: preview_deck returns one render URL per slide, each carrying a Preview token for that deck in its fragment,
// plus Markdown instructions for the agent to screenshot the URLs in its own headless Chromium. The server renders nothing.
[TestFixture]
public class PreviewDeckToolTests : ApiTestFixture
{
    private const string PLAYWRIGHT_VERSION = "1.62.0";

    // The render tokens with a test key. Registering them here holds whether or not the app registers them too.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton(new RenderTokenOptions { Key = new string('k', RenderTokenOptions.MINIMUM_KEY_LENGTH) });
        services.AddSingleton<IRenderTokens, RenderTokens>();
    };

    [Test]
    public async Task PreviewDeck_WhenNoSlidesGiven_ShouldReturnEverySlide()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "preview_deck", $$"""{"deck":"{{deck}}"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("content").EnumerateArray().Select(c => c.GetProperty("type").GetString()).ShouldBe(["text"]);
        var structured = result.GetProperty("structuredContent");
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["slides", "viewport", "wait_for", "expires_in_seconds", "player_url", "share_card_url", "instructions"]);
        Numbers(structured).ShouldBe([1, 2, 3]);
        structured.GetProperty("viewport").GetRawText().ShouldBe("""{"width":1920,"height":1080}""");
        structured.GetProperty("wait_for").GetString().ShouldBe("[data-ready=true]");
        structured.GetProperty("expires_in_seconds").GetInt32().ShouldBe(600);
        structured.GetProperty("player_url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller");
        structured.GetProperty("share_card_url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/api/cards/{deck}.png");
        foreach (var (slide, n) in structured.GetProperty("slides").EnumerateArray().Select((s, i) => (s, i + 1)))
        {
            slide.GetProperty("render_url").GetString().ShouldStartWith($"{TestAppFactory.PUBLIC_URL}/render/{deck}/{n}#token=");
        }
    }

    [Test]
    public async Task PreviewDeck_WhenSlidesGiven_ShouldReturnOnlyThose()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (isError, json) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 3, 1 } });

        isError.ShouldBeFalse(json.GetRawText());
        Numbers(json).ShouldBe([3, 1]);
        json.GetProperty("slides")[0].GetProperty("render_url").GetString().ShouldStartWith($"{TestAppFactory.PUBLIC_URL}/render/{deck}/3#token=");
    }

    [Test]
    public async Task PreviewDeck_WhenUnknownSlide_ShouldSayWhich()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (isError, json) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 2, 40 } });
        var (zeroIsError, zero) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 0 } });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("slides: no slide 40");
        zeroIsError.ShouldBeTrue();
        zero.GetString().ShouldBe("slides: no slide 0");
    }

    [Test]
    public async Task PreviewDeck_WhenOtherPresentersDeck_ShouldSayDeckNotFound()
    {
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var alexDeck = await DeckSeed.CreateDeckAsync(alexAgent);
        await DeckSeed.AddQuoteAsync(alexAgent, alexDeck, "Small batches.");
        await using var samAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await samAgent.CallToolAsync("preview_deck", new { deck = alexDeck });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("Deck not found");
    }

    [Test]
    public async Task PreviewDeck_WhenIssued_ShouldPutAPreviewTokenForThatDeckOnlyInTheFragment()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        var other = await DeckSeed.CreateDeckAsync(agent, "Other");
        var tokens = Factory.Services.GetRequiredService<IRenderTokens>();

        var (_, json) = await agent.CallToolAsync("preview_deck", new { deck });

        foreach (var slide in json.GetProperty("slides").EnumerateArray())
        {
            var url = new Uri(slide.GetProperty("render_url").GetString()!);
            url.Query.ShouldBeEmpty();
            url.Fragment.ShouldStartWith("#token=");
            var token = Uri.UnescapeDataString(url.Fragment["#token=".Length..]);
            tokens.Validate(token, deck, RenderPurpose.Preview).ShouldBeTrue();
            tokens.Validate(token, deck, RenderPurpose.Card).ShouldBeFalse();
            tokens.Validate(token, other, RenderPurpose.Preview).ShouldBeFalse();
        }
        json.GetProperty("player_url").GetString()!.ShouldNotContain("token");
        json.GetProperty("share_card_url").GetString()!.ShouldNotContain("token");
    }

    [Test]
    public async Task PreviewDeck_WhenCalled_ShouldFillInstructionsWithTheUrlsAndThePinnedPlaywright()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (_, json) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 1, 2 } });

        var instructions = json.GetProperty("instructions").GetString()!;
        instructions.ShouldContain($"npx -y playwright@{PLAYWRIGHT_VERSION} --version");
        instructions.ShouldContain($"npx -y playwright@{PLAYWRIGHT_VERSION} install chromium");
        instructions.ShouldContain($"npx -y playwright@{PLAYWRIGHT_VERSION} install-deps chromium");
        instructions.ShouldContain($"pip install playwright=={PLAYWRIGHT_VERSION}");
        foreach (var slide in json.GetProperty("slides").EnumerateArray())
        {
            var n = slide.GetProperty("number").GetInt32();
            instructions.ShouldContain(
                $"npx -y playwright@{PLAYWRIGHT_VERSION} screenshot --viewport-size=1920,1080 --wait-for-selector=\"[data-ready=true]\" \"{slide.GetProperty("render_url").GetString()}\" slide-{n}.png");
        }
        instructions.ShouldNotContain("slide-3.png");
        instructions.ShouldContain(json.GetProperty("player_url").GetString()!);
        instructions.ShouldContain("10 minutes");
        instructions.ShouldContain("data-error");
        instructions.ShouldNotContain("{{");
    }

    private static async Task<string> ThreeSlideDeckAsync(McpTestClient agent)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent, "Ship smaller");
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches.");
        await DeckSeed.AddQuoteAsync(agent, deck, "Ship daily.");
        await DeckSeed.AddQuoteAsync(agent, deck, "Measure it.");
        return deck;
    }

    private static int[] Numbers(JsonElement result)
        => result.GetProperty("slides").EnumerateArray().Select(s => s.GetProperty("number").GetInt32()).ToArray();
}
