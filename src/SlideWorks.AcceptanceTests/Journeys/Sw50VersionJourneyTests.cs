using System.Text.Json;
using ModelContextProtocol.Client;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50 versions end to end: an agent publishes a template twice (version 2 adds the top accent bar), and a deck made on
// version 1 is offered the update in its Look panel, or its owner moves all their own decks in one call. Templates are
// made and published over MCP; the visible change between versions is the top-bar element in the player.
public class Sw50VersionJourneyTests : AcceptanceTest
{
    private const string FRAMES = "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))";
    private const string TOP_BAR_COUNT = "() => document.querySelectorAll('[data-testid=\"top-bar\"]').length";

    [Test]
    public async Task Person_WhenANewVersionIsPublished_ShouldBeOfferedTheUpdate()
    {
        var name = $"Versions {Guid.NewGuid():N}"[..24];
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var template = await MakeTemplateAsync(agent, name);
        var deckId = await MakeDeckAsync(agent, template, "Version offer deck");

        await SignIn.AsAsync(Page, TestUser.Sam);
        var deck = new DeckPage(Page);
        var look = deck.Look;
        await deck.GotoAsync(deckId);
        await Expect(look.Root).ToBeVisibleAsync();
        await Expect(look.Template).ToBeEnabledAsync();
        await Page.EvaluateAsync(FRAMES);
        await Expect(look.VersionOffer).ToHaveCountAsync(0);
        await Expect(deck.ActivityRows.First).ToBeVisibleAsync();

        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Page.EvaluateAsync(FRAMES);
        (await Page.EvaluateAsync<int>(TOP_BAR_COUNT)).ShouldBe(0);

        await PublishVersion2Async(agent, template);
        await deck.GotoAsync(deckId);
        await Expect(look.VersionOffer).ToBeVisibleAsync();
        await Expect(look.VersionOffer).ToContainTextAsync("version 2 available");
        await Expect(look.VersionOffer).ToContainTextAsync("version 1");
        await look.VersionUpdate.ClickAsync();

        await Expect(look.VersionMessage).ToHaveTextAsync("Now on version 2.");
        await Expect(look.VersionMessage).ToBeFocusedAsync();
        await Expect(look.VersionOffer).ToHaveCountAsync(0);
        await Expect(look.VersionError).ToHaveCountAsync(0);
        await Expect(deck.ActivityRows.Filter(new() { HasText = "version 1 to 2" })).ToHaveCountAsync(1);

        await player.GotoAsync(deckId);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        (await Eventually.ReadAsync(() => Page.EvaluateAsync<int>(TOP_BAR_COUNT), n => n == 1)).ShouldBe(1);

        var look2 = (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deckId })).GetProperty("look");
        look2.GetProperty("templateVersion").GetInt32().ShouldBe(2);
        look2.GetProperty("updateAvailable").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Owner_WhenUpdatingMyDecks_ShouldMoveOnlyTheirOwn()
    {
        var name = $"Mine only {Guid.NewGuid():N}"[..24];
        await using var sam = await AgentSeed.ConnectAsync(TestUser.Sam);
        await using var alex = await AgentSeed.ConnectAsync(TestUser.Alex);
        var template = await MakeTemplateAsync(sam, name);
        await SignIn.AsAsync(Page, TestUser.Sam);
        var shared = await Page.APIRequest.PutAsync($"/api/templates/{template}/sharing", new APIRequestContextOptions
        {
            DataObject = new { level = "named", viewers = new[] { TestUser.Alex.Email } },
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        shared.Status.ShouldBe(200, await shared.TextAsync());
        var samOne = await MakeDeckAsync(sam, template, "Sam one");
        var samTwo = await MakeDeckAsync(sam, template, "Sam two");
        var alexDeck = await MakeDeckAsync(alex, template, "Alex one");
        await PublishVersion2Async(sam, template);

        var response = await Page.APIRequest.PostAsync($"/api/templates/{template}/update-my-decks", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("count").GetInt32().ShouldBe(2);

        var player = new PlayerPage(Page);
        foreach (var id in new[] { samOne, samTwo })
        {
            await player.GotoAsync(id);
            await Expect(player.Slide(1)).ToBeVisibleAsync();
            (await Eventually.ReadAsync(() => Page.EvaluateAsync<int>(TOP_BAR_COUNT), n => n == 1)).ShouldBe(1);
        }

        await using var alexContext = await Browser.NewContextAsync(ContextOptions());
        var alexPage = await alexContext.NewPageAsync();
        await SignIn.AsAsync(alexPage, TestUser.Alex);
        var alexDeckPage = new DeckPage(alexPage);
        await alexDeckPage.GotoAsync(alexDeck);
        await Expect(alexDeckPage.Look.VersionOffer).ToBeVisibleAsync();
        await Expect(alexDeckPage.Look.VersionOffer).ToContainTextAsync("version 2 available");
        var alexPlayer = new PlayerPage(alexPage);
        await alexPlayer.GotoAsync(alexDeck);
        await Expect(alexPlayer.Slide(1)).ToBeVisibleAsync();
        await alexPage.EvaluateAsync(FRAMES);
        (await alexPage.EvaluateAsync<int>(TOP_BAR_COUNT)).ShouldBe(0);

        var alexLook = (await AgentSeed.CallAsync(alex, "get_deck", new() { ["deck"] = alexDeck })).GetProperty("look");
        alexLook.GetProperty("templateVersion").GetInt32().ShouldBe(1);
        alexLook.GetProperty("updateAvailable").GetBoolean().ShouldBeTrue();
    }

    // Creates a template from talk with no top bar and publishes it as version 1. Returns its tpl_ id.
    private static async Task<string> MakeTemplateAsync(McpClient agent, string name)
    {
        var created = await AgentSeed.CallAsync(agent, "create_template", new() { ["name"] = name, ["from"] = "talk" });
        var id = created.GetProperty("template").GetString()!;
        await SetTopBarAsync(agent, id, "none");
        var published = await AgentSeed.CallAsync(agent, "publish_template", new() { ["template"] = id });
        published.GetProperty("version").GetInt32().ShouldBe(1);
        return id;
    }

    private static async Task PublishVersion2Async(McpClient agent, string id)
    {
        await SetTopBarAsync(agent, id, "accent");
        var published = await AgentSeed.CallAsync(agent, "publish_template", new() { ["template"] = id });
        published.GetProperty("version").GetInt32().ShouldBe(2);
    }

    // update_template replaces top-level keys, so the whole chrome object goes back with its topBar changed.
    private static async Task SetTopBarAsync(McpClient agent, string id, string topBar)
    {
        var current = await AgentSeed.CallAsync(agent, "get_template", new() { ["template"] = id });
        var chrome = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(current.GetProperty("manifest").GetProperty("chrome").GetRawText())!;
        var patch = new Dictionary<string, object> { ["chrome"] = chrome.ToDictionary(p => p.Key, p => p.Key == "topBar" ? (object)topBar : p.Value) };
        await AgentSeed.CallAsync(agent, "update_template", new() { ["template"] = id, ["manifest"] = JsonSerializer.SerializeToElement(patch) });
        var after = await AgentSeed.CallAsync(agent, "get_template", new() { ["template"] = id });
        after.GetProperty("manifest").GetProperty("chrome").GetProperty("topBar").GetString().ShouldBe(topBar);
    }

    // A deck on the template's latest version with one slide, made as the agent's person would.
    private static async Task<string> MakeDeckAsync(McpClient agent, string template, string title)
    {
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = title, ["template"] = template });
        var id = created.GetProperty("deck").GetString()!;
        var slots = JsonSerializer.SerializeToElement(new { quote = "Small batches ship.", attribution = "Test" });
        await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = id, ["layout"] = "quote", ["slots"] = slots });
        var look = (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = id })).GetProperty("look");
        look.GetProperty("templateVersion").GetInt32().ShouldBe(1);
        return id;
    }
}
