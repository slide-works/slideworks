using System.Text.Json;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// The deck page and the player re-read the deck when their hub connection comes back. An agent edits a slide's text over
// MCP while the page's hub socket is down and its reconnect is held at negotiate (DroppableHub), so no DeckChanged event
// can reach the page; only the re-read on the rejoin can show the new text. The page is not reloaded. Neither page draws a
// live or offline indicator, so the socket really being down is shown by DropAsync closing one open socket and by the
// held reconnect leaving the old text on screen after the edit has been saved and read back.
public class ReconnectRereadJourneyTests : AcceptanceTest
{
    private const string BEFORE = "Before the drop.";
    private const string AFTER = "Edited while offline.";
    private const string OPEN_HUB_SOCKET = "() => window.__hubSockets.some((s) => s.readyState === WebSocket.OPEN)";

    // A mark set on the window before the drop; a reload would clear it, so finding it after the new text shows proves the
    // page re-read the deck in place.
    private const string MARK_DOCUMENT = "() => { window.__sw813SameDocument = true; }";
    private const string SAME_DOCUMENT = "() => window.__sw813SameDocument === true";

    [Test]
    public async Task DeckPage_WhenItsHubDropsAndTheSlideIsEditedWhileDown_ShouldShowTheNewTextAfterTheReconnectWithoutReloading()
    {
        var hub = await DroppableHub.RouteAsync(Page);
        var id = await CreateDeckAsync();
        await SignIn.AsAsync(Page, TestUser.Sam);
        var deck = new DeckPage(Page);
        await deck.GotoAsync(id);
        await Expect(deck.Thumbnails).ToHaveCountAsync(1);
        await Expect(deck.Thumbnail(1)).ToBeVisibleAsync();
        await Expect(deck.Thumbnail(1)).ToContainTextAsync(BEFORE, new() { UseInnerText = true });
        await Page.WaitForFunctionAsync(OPEN_HUB_SOCKET);

        await Page.EvaluateAsync(MARK_DOCUMENT);
        (await hub.DropAsync()).ShouldBe(1, "the deck page should have had one hub socket to drop");
        await EditWhileDownAsync(id);

        await Expect(deck.Thumbnail(1)).ToBeVisibleAsync();
        await Expect(deck.Thumbnail(1)).ToContainTextAsync(BEFORE, new() { UseInnerText = true });
        await Expect(deck.Thumbnail(1)).Not.ToContainTextAsync(AFTER, new() { UseInnerText = true });

        hub.Release();

        await Expect(deck.Thumbnail(1)).ToContainTextAsync(AFTER, new() { UseInnerText = true });
        await Expect(deck.Thumbnail(1)).Not.ToContainTextAsync(BEFORE, new() { UseInnerText = true });
        await Expect(deck.Thumbnail(1)).ToBeVisibleAsync();
        await Expect(deck.Error).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<bool>(SAME_DOCUMENT)).ShouldBeTrue("the page should not have reloaded");
    }

    [Test]
    public async Task Player_WhenItsHubDropsAndTheSlideIsEditedWhileDown_ShouldShowTheNewTextAfterTheReconnectWithoutReloading()
    {
        var hub = await DroppableHub.RouteAsync(Page);
        var id = await CreateDeckAsync();
        var player = new PlayerPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await player.GotoAsync(id);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Expect(player.Slide(1)).ToContainTextAsync(BEFORE, new() { UseInnerText = true });
        await Page.WaitForFunctionAsync(OPEN_HUB_SOCKET);

        await Page.EvaluateAsync(MARK_DOCUMENT);
        (await hub.DropAsync()).ShouldBe(1, "the player should have had one hub socket to drop");
        await EditWhileDownAsync(id);

        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Expect(player.Slide(1)).ToContainTextAsync(BEFORE, new() { UseInnerText = true });
        await Expect(player.Slide(1)).Not.ToContainTextAsync(AFTER, new() { UseInnerText = true });

        hub.Release();

        await Expect(player.Slide(1)).ToContainTextAsync(AFTER, new() { UseInnerText = true });
        await Expect(player.Slide(1)).Not.ToContainTextAsync(BEFORE, new() { UseInnerText = true });
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Expect(player.Error).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<bool>(SAME_DOCUMENT)).ShouldBeTrue("the page should not have reloaded");
    }

    // Sam's deck with one quote slide saying BEFORE, made the way an agent makes it.
    private static async Task<string> CreateDeckAsync()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Reread {Guid.NewGuid():N}" });
        var id = created.GetProperty("deck").GetString()!;
        await AgentSeed.AddQuotesAsync(TestUser.Sam, id, BEFORE);
        return id;
    }

    // Changes slide 1's quote to AFTER over MCP, then reads the deck back to prove the write landed.
    private static async Task EditWhileDownAsync(string id)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = id });
        var slide = deck.GetProperty("slides").EnumerateArray().Single();
        var revision = slide.GetProperty("revision").GetInt64();
        var slots = JsonSerializer.SerializeToElement(new { quote = AFTER, attribution = TestUser.Sam.Name });
        await AgentSeed.CallAsync(agent, "update_slide", new() { ["deck"] = id, ["slide"] = 1, ["revision"] = revision, ["slots"] = slots });

        var after = await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = id });
        after.GetProperty("slides").EnumerateArray().Single().GetRawText().ShouldContain(AFTER);
    }
}
