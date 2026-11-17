using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// The privacy guard for GET /api/join/{code} (J3): an allow-list of keys, which is guest.json's, and a value test that
// seeds what must never reach a phone and looks for it in the raw body.
[TestFixture]
[CancelAfter(30000)]
public class GuestPrivacyTests : JoinFixture
{
    private const string NOTES = "Say the secret rollout date out loud";
    private const string DEEP_DIVE = "Hidden appendix about pricing";
    private const string VIEWER = "viewer.person@guests.example";

    [Test]
    public async Task GuestView_WhenDeckHasNotesAndViewers_ShouldContainNoneOfThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Why", "items": ["One", "Two"] }""", notes: NOTES);
        var (isError, added) = await agent.CallToolAsync("add_slide", new { deck, layout = "quote", slots = Slots($$"""{ "quote": "{{DEEP_DIVE}}", "attribution": "Sam" }"""), deep_dive = true });
        isError.ShouldBeFalse(added.ToString());
        await SharingApi.PutOkAsync(Client, deck, "named", VIEWER);
        Open(CODE, deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var raw = await anonymous.GetStringAsync($"/api/join/{CODE}");

        var body = JsonDocument.Parse(raw).RootElement;
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "guest.json")));
        JsonShape.AssertMatches(body, sample.RootElement.GetProperty("live"), "guest.json", ScreenShapes.Guest);
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["deckTitle", "presenterName", "look", "deckUrl", "state"], ignoreOrder: true);
        body.GetProperty("state").EnumerateObject().Select(p => p.Name).ShouldBe(["type", "seq", "phase", "current", "revision", "activity"], ignoreOrder: true);
        raw.ShouldNotContain(NOTES);
        raw.ShouldNotContain(DEEP_DIVE);
        raw.ShouldNotContain(VIEWER);
        raw.ShouldNotContain(TestUser.Sam.Email);
        raw.ShouldNotContain("\"notes\"");
        raw.ShouldNotContain("\"viewers\"");
    }
}
