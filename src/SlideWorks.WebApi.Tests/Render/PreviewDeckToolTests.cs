using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// L7, D21 and D26: preview_deck returns one render URL template ({slide} where the number goes) carrying a Preview token
// for that deck in its fragment, the slide numbers, plus Markdown instructions for the agent to screenshot the URLs in its own headless Chromium. The server renders nothing.
[TestFixture]
public class PreviewDeckToolTests : ApiTestFixture
{
    private const string PLAYWRIGHT_VERSION = "1.62.0";
    private const string WAIT_FOR = "[data-ready=true], [data-error]";

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeStoredSlideChecks.Register;

    [SetUp]
    public void ResetFakes()
    {
        Factory.Services.GetRequiredService<FakeRenderEngine>().Reset();
        Factory.Services.GetRequiredService<FakeStoredSlideChecks>().Reset();
    }

    [Test]
    public async Task PreviewDeck_WhenBothSourcesReportFindings_ShouldReturnThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Factory.Services.GetRequiredService<FakeStoredSlideChecks>().FindingsFor(1, new SlideFinding(1, FindingRule.Fit, FindingSeverity.Error, FindingSource.Stored, "items", "Shorten it."));
        Factory.Services.GetRequiredService<FakeStoredSlideChecks>().FindingsFor(3, new SlideFinding(3, FindingRule.GenericAlt, FindingSeverity.Warning, FindingSource.Stored, "image", "Describe it."));
        Factory.Services.GetRequiredService<FakeRenderEngine>().FindingsFor(1, new SlideFinding(1, FindingRule.Overflow, FindingSeverity.Error, FindingSource.Measured, "text", "Cut it."));
        Factory.Services.GetRequiredService<FakeRenderEngine>().FindingsFor(2, new SlideFinding(2, FindingRule.TextOnImage, FindingSeverity.Note, FindingSource.Measured, "text", "Look."));

        var (isError, json) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 1, 2 } });

        isError.ShouldBeFalse(json.GetRawText());
        json.GetProperty("findings").EnumerateArray().Select(f => $"{f.GetProperty("slide").GetInt32()} {f.GetProperty("rule").GetString()} {f.GetProperty("source").GetString()}")
            .ShouldBe(["1 overflow measured", "1 fit stored", "2 textOnImage measured"]);
    }

    [Test]
    public async Task PreviewDeck_OnTheRawWire_ShouldCarryFindings()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Factory.Services.GetRequiredService<FakeRenderEngine>().FindingsFor(2, new SlideFinding(2, FindingRule.LowContrast, FindingSeverity.Warning, FindingSource.Measured, "text", "Darken it."));

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "preview_deck", $$"""{"deck":"{{deck}}"}""");

        var findings = result.GetProperty("structuredContent").GetProperty("findings");
        findings.ValueKind.ShouldBe(JsonValueKind.Array);
        findings[0].GetProperty("rule").GetString().ShouldBe("lowContrast");
        findings[0].GetProperty("severity").GetString().ShouldBe("warning");
    }

    [Test]
    public async Task PreviewDeck_WhenCalled_ShouldGiveOneUrlTemplateAndTheNumbers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "preview_deck", $$"""{"deck":"{{deck}}"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        result.GetProperty("content").EnumerateArray().Select(c => c.GetProperty("type").GetString()).ShouldBe(["text", "image", "image", "image"]);
        var structured = result.GetProperty("structuredContent");
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["render_url", "slides", "viewport", "wait_for", "expires_in_seconds", "player_url", "share_card_url", "instructions", "images_included", "findings"]);
        structured.GetProperty("images_included").GetInt32().ShouldBe(3);
        Numbers(structured).ShouldBe([1, 2, 3]);
        structured.GetProperty("viewport").GetRawText().ShouldBe("""{"width":1920,"height":1080}""");
        structured.GetProperty("wait_for").GetString().ShouldBe(WAIT_FOR);
        structured.GetProperty("expires_in_seconds").GetInt32().ShouldBe(600);
        structured.GetProperty("player_url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller");
        structured.GetProperty("share_card_url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/api/cards/{deck}.png");
        structured.GetProperty("render_url").GetString().ShouldStartWith($"{TestAppFactory.PUBLIC_URL}/render/{deck}/{{slide}}#token=");
    }

    [Test]
    public async Task PreviewDeck_WhenSlidesAreGiven_ShouldLoopOverThoseNumbers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (isError, json) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 3, 1 } });

        isError.ShouldBeFalse(json.GetRawText());
        Numbers(json).ShouldBe([3, 1]);
        json.GetProperty("instructions").GetString()!.ShouldContain("for n in 3 1; do ");
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
    public async Task PreviewDeck_WhenIssued_ShouldPutThePreviewTokenInTheFragmentOnce()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        var other = await DeckSeed.CreateDeckAsync(agent, "Other");
        var tokens = Factory.Services.GetRequiredService<IRenderTokens>();

        var (_, json) = await agent.CallToolAsync("preview_deck", new { deck });

        var template = json.GetProperty("render_url").GetString()!;
        template.Split("#token=").Length.ShouldBe(2);
        template.ShouldNotContain("?");
        var url = new Uri(template.Replace("{slide}", "1"));
        url.Query.ShouldBeEmpty();
        url.Fragment.ShouldStartWith("#token=");
        var token = Uri.UnescapeDataString(url.Fragment["#token=".Length..]);
        tokens.Validate(token, deck, RenderPurpose.Preview).ShouldBeTrue();
        tokens.Validate(token, deck, RenderPurpose.Card).ShouldBeFalse();
        tokens.Validate(token, other, RenderPurpose.Preview).ShouldBeFalse();
        json.GetProperty("instructions").GetString()!.Split(token).Length.ShouldBe(3, "The token should be in the loop and the script, each once.");
        json.GetProperty("player_url").GetString()!.ShouldNotContain("token");
        json.GetProperty("share_card_url").GetString()!.ShouldNotContain("token");
    }

    [Test]
    public async Task PreviewDeck_WhenCalledOnTheWire_ShouldReturnAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "preview_deck", $$"""{"deck":"{{deck}}"}""");

        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.GetProperty("render_url").ValueKind.ShouldBe(JsonValueKind.String);
        structured.GetProperty("slides").ValueKind.ShouldBe(JsonValueKind.Array);
        structured.GetProperty("slides").EnumerateArray().Select(n => n.ValueKind).ShouldBe([JsonValueKind.Number, JsonValueKind.Number, JsonValueKind.Number]);
    }

    [Test]
    public async Task PreviewDeck_WhenTheDeckHas44Slides_ShouldStayUnder4Kb()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Big deck");
        for (var i = 0; i < 44; i++)
        {
            await DeckSeed.AddQuoteAsync(agent, deck, $"Quote {i}.");
        }

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "preview_deck", $$"""{"deck":"{{deck}}"}""");

        var structured = result.GetProperty("structuredContent");
        structured.GetProperty("slides").GetArrayLength().ShouldBe(44);
        Encoding.UTF8.GetByteCount(structured.GetRawText()).ShouldBeLessThan(4096);
        structured.GetProperty("instructions").GetString()!.ShouldContain("for n in $(seq 1 44); do ");
    }

    [Test]
    public async Task PreviewDeck_WhenCalled_ShouldSayAReloadLosesTheToken()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (_, json) = await agent.CallToolAsync("preview_deck", new { deck });

        var instructions = json.GetProperty("instructions").GetString()!;
        instructions.ShouldContain("removes it from the address");
        instructions.ShouldContain("`Render token missing`");
        instructions.ShouldContain("open the full URL again");
    }

    [Test]
    public async Task PreviewDeck_WhenDescribed_ShouldSayOneUrlTemplate()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var tool = (await agent.Client.ListToolsAsync()).Single(t => t.Name == "preview_deck");

        tool.Description.ShouldContain("one render URL template with {slide} in it");
        tool.Description.ShouldNotContain("per slide");
    }

    [Test]
    public async Task PreviewDeck_WhenCalled_ShouldFillInstructionsWithTheLoopAndThePinnedPlaywright()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (_, json) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 1, 2 } });

        var instructions = json.GetProperty("instructions").GetString()!;
        instructions.ShouldContain($"npx -y playwright@{PLAYWRIGHT_VERSION} --version");
        instructions.ShouldContain($"npx -y playwright@{PLAYWRIGHT_VERSION} install --only-shell chromium");
        instructions.ShouldContain($"npx -y playwright@{PLAYWRIGHT_VERSION} install-deps chromium");
        instructions.ShouldContain("python3 -m venv .pw && .pw/bin/pip install 'playwright==1.62.*'");
        var template = json.GetProperty("render_url").GetString()!;
        instructions.ShouldContain(
            $"for n in $(seq 1 2); do npx -y playwright@{PLAYWRIGHT_VERSION} screenshot --viewport-size=1920,1080 --wait-for-selector=\"{WAIT_FOR}\" --timeout=60000 \"{template.Replace("{slide}", "$n")}\" slide-$n.png; done");
        instructions.ShouldContain("npm install --no-save playwright@" + PLAYWRIGHT_VERSION);
        instructions.ShouldContain("shots.mjs");
        instructions.ShouldContain("for (const n of [1, 2])");
                instructions.ShouldContain(json.GetProperty("player_url").GetString()!);
        instructions.ShouldContain("10 minutes");
        instructions.ShouldContain("Deck not found");
        instructions.ShouldContain("call `preview_deck` again");
        instructions.ShouldContain("timeout after 60 seconds");
        instructions.ShouldNotContain("{{");
    }

    [Test]
    public async Task PreviewDeck_WhenDeckDoesNotExist_ShouldSayDeckNotFound()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await agent.CallToolAsync("preview_deck", new { deck = "dk_no_such_deck" });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("Deck not found");
    }

    // D2: repeats are dropped and the first-seen order is kept; an empty list means every slide.
    [Test]
    public async Task PreviewDeck_WhenSlidesRepeatOrAreEmpty_ShouldKeepFirstSeenOrderOrReturnEverySlide()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (repeatedIsError, repeated) = await agent.CallToolAsync("preview_deck", new { deck, slides = new[] { 2, 2, 1, 2 } });
        var (emptyIsError, empty) = await agent.CallToolAsync("preview_deck", new { deck, slides = Array.Empty<int>() });

        repeatedIsError.ShouldBeFalse(repeated.GetRawText());
        Numbers(repeated).ShouldBe([2, 1]);
        emptyIsError.ShouldBeFalse(empty.GetRawText());
        Numbers(empty).ShouldBe([1, 2, 3]);
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
        => result.GetProperty("slides").EnumerateArray().Select(s => s.GetInt32()).ToArray();
}
