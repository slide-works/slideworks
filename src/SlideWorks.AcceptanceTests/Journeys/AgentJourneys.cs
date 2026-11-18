using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Journeys where an agent connected over OAuth changes a deck and Sam sees it in the browser. The agent registers
// once for the class, as a real agent keeps its client id (registration is limited to 10 a minute per IP and the whole
// run shares one host), and each test sends Sam through consent for its own token.
public class AgentJourneys : AcceptanceTest
{
    private static readonly string[] TOOLS =
    [
        "add_poll_option", "add_slide", "close_poll", "create_deck", "create_template", "delete_slide", "get_deck", "get_deck_url",
        "get_poll_results", "get_template", "list_blocks", "list_decks", "list_layouts", "list_palettes", "list_templates", "move_slide",
        "open_poll", "preview_deck", "publish_template", "set_look", "set_notes", "set_sharing", "set_talk_length", "update_poll",
        "update_slide", "update_template", "upload_asset", "upload_image",
    ];

    private AgentOAuthClient _oauth = null!;

    [OneTimeSetUp]
    public async Task RegisterAgent() => _oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");

    [OneTimeTearDown]
    public void ForgetAgent() => _oauth.Dispose();

    [Test]
    public async Task McpListsAllTools_WhenAgentConnects_ShouldListK6AndLookTools()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);

        (await agent.ToolNamesAsync()).Order(StringComparer.Ordinal).ShouldBe(TOOLS);
    }

    [Test]
    public async Task DeckPageShowsAgentActivityLive_WhenAgentAddsSlideWhileOpen_ShouldShowItWithoutReload()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = await CreateDeckAsync(agent, "Live Activity");
        var deck = new DeckPage(Page);

        await deck.GotoAsync(deckId);
        await Expect(deck.SlideCount).ToHaveTextAsync("0 slides");
        await Expect(deck.ActivityRows.First).ToContainTextAsync("Claude created the deck");
        await AddQuoteAsync(agent, deckId, "Small batches make feedback cheap.");

        await Expect(deck.SlideCount).ToHaveTextAsync("1 slide");
        await Expect(deck.Thumbnails).ToHaveCountAsync(1);
        await Expect(deck.ActivityRows).ToHaveCountAsync(2);
        await Expect(deck.ActivityRows.First).ToContainTextAsync("Claude added slide 1 (quote)");
        await Expect(deck.ActivityRows.First).ToContainTextAsync("via MCP add_slide");
    }

    // A change that lands after the deck page's first read but before it has joined the deck on /hubs/deck sends no
    // event to the page. The page re-reads once it has joined, so the change still shows. The join is held by holding
    // the hub's negotiate request.
    [Test]
    public async Task DeckPageShowsChangeMadeBeforeItJoined_WhenAgentChangesDeckWhileJoinIsHeld_ShouldShowItOnceJoined()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = await CreateDeckAsync(agent, "Before The Join");
        var deck = new DeckPage(Page);
        var hub = await HeldNegotiate.RouteAsync(Page);

        await deck.GotoAsync(deckId);
        await Expect(deck.SlideCount).ToHaveTextAsync("0 slides");
        await hub.AskedAsync(1);
        await AddQuoteAsync(agent, deckId, "Joined late, still current.");
        hub.Release();

        await Expect(deck.SlideCount).ToHaveTextAsync("1 slide");
        await Expect(deck.Thumbnails).ToHaveCountAsync(1);
        await Expect(deck.ActivityRows.First).ToContainTextAsync("Claude added slide 1 (quote)");
    }

    [Test]
    public async Task DeletedSlideDisappearsFromDeckPageAndPlayer_WhenAgentDeletesIt_ShouldRenumberTheRest()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = await CreateDeckAsync(agent, "Deleted Slide");
        await AddQuoteAsync(agent, deckId, "First, keep this one.");
        await AddQuoteAsync(agent, deckId, "Second, this one goes.");
        await AddQuoteAsync(agent, deckId, "Third, keep this one too.");
        var deck = new DeckPage(Page);

        await deck.GotoAsync(deckId);
        await Expect(deck.Thumbnails).ToHaveCountAsync(3);
        var deleted = await agent.CallAsync("delete_slide", new { deck = deckId, slide = 2 });
        deleted.GetProperty("slides").GetInt32().ShouldBe(2);

        await Expect(deck.SlideCount).ToHaveTextAsync("2 slides");
        await Expect(deck.Thumbnails).ToHaveCountAsync(2);
        await Expect(deck.Thumbnail(2)).ToContainTextAsync("Third, keep this one too.");
        await Expect(deck.ActivityRows.First).ToContainTextAsync("Claude deleted slide 2 (quote)");

        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync("1 / 2");
        await Expect(player.Slide(1)).ToContainTextAsync("First, keep this one.");
        await Expect(player.Slide(2)).ToContainTextAsync("Third, keep this one too.");
        await Expect(player.Slide(3)).ToHaveCountAsync(0);
        await Expect(player.Player).Not.ToContainTextAsync("Second, this one goes.");
    }

    [Test]
    public async Task DeepLinkOpensSlideAndStep_WhenAgentLinksToAStep_ShouldOpenThereAndCarryOn()
    {
        await using var agent = await AgentSession.ConnectAsync(_oauth, Page, TestUser.Sam);
        var deckId = await CreateDeckAsync(agent, "Deep Link");
        await AddQuoteAsync(agent, deckId, "Start small.");
        await agent.CallAsync("add_slide", new
        {
            deck = deckId,
            layout = "bullets",
            slots = new { headline = "Three habits", items = new[] { "Merge every day", "Demo every Friday", "Split old stories" }, reveal = "one-by-one" },
        });
        await AddQuoteAsync(agent, deckId, "Then keep going.");
        var link = (await agent.CallAsync("get_deck_url", new { deck = deckId, slide = 2, step = 2 })).GetProperty("url").GetString().ShouldNotBeNull();
        var player = new PlayerPage(Page);

        await Page.GotoAsync(link);

        await Expect(player.Count).ToHaveTextAsync("2 / 3");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        await Expect(player.RevealedBullets(2)).ToHaveCountAsync(2);
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.RevealedBullets(2)).ToHaveCountAsync(3);
        await Expect(Page).ToHaveURLAsync(link[..link.LastIndexOf('#')] + "#2.3");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("3 / 3");
    }

    private static async Task<string> CreateDeckAsync(AgentSession agent, string title) =>
        (await agent.CallAsync("create_deck", new { title })).GetProperty("deck").GetString().ShouldNotBeNull();

    private static Task<JsonElement> AddQuoteAsync(AgentSession agent, string deckId, string quote) =>
        agent.CallAsync("add_slide", new { deck = deckId, layout = "quote", slots = new { quote, attribution = "Sam Rivera" } });
}
