using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-886 K8: GET /api/play/{id}?language= returns each slide's slots merged in that language through TranslationMerge.For. A
// stale line shows the primary text; a language the deck doesn't offer, or translations off, reads as the primary, with no error.
[TestFixture]
[CancelAfter(60000)]
public class PlayViewLanguageTests : ApiTestFixture
{
    private const string PLAN = """{ "headline": "Plan", "items": ["A", "B"] }""";

    [Test]
    public async Task Get_WhenLanguageIsOffered_ShouldReturnTheTranslatedSlots()
    {
        var deck = await SeedAsync(enabled: true, ("/headline", "แผน", "Plan"), ("/items/0", "เอ", "A"));

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}?language=th");

        var slots = play.GetProperty("slides")[0].GetProperty("slots");
        slots.GetProperty("headline").GetString().ShouldBe("แผน");
        slots.GetProperty("items").EnumerateArray().Select(item => item.GetString()).ShouldBe(["เอ", "B"]);
    }

    [Test]
    public async Task Get_WhenALineIsStale_ShouldShowThePrimaryText()
    {
        var deck = await SeedAsync(enabled: true, ("/headline", "แผนเก่า", "Old plan"), ("/items/0", "เอ", "A"));

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}?language=th");

        var slots = play.GetProperty("slides")[0].GetProperty("slots");
        slots.GetProperty("headline").GetString().ShouldBe("Plan");
        slots.GetProperty("items")[0].GetString().ShouldBe("เอ");
    }

    [Test]
    public async Task Get_WhenLanguageIsNotOffered_ShouldReturnThePrimary()
    {
        var deck = await SeedAsync(enabled: true, ("/headline", "แผน", "Plan"));

        var response = await Client.GetAsync($"/api/play/{deck}?language=ja");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var play = await response.Content.ReadFromJsonAsync<JsonElement>();
        play.GetProperty("slides")[0].GetProperty("slots").GetProperty("headline").GetString().ShouldBe("Plan");
    }

    [Test]
    public async Task Get_WhenTranslationsAreOff_ShouldReturnThePrimary()
    {
        var deck = await SeedAsync(enabled: false, ("/headline", "แผน", "Plan"));

        var response = await Client.GetAsync($"/api/play/{deck}?language=th");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var play = await response.Content.ReadFromJsonAsync<JsonElement>();
        play.GetProperty("slides")[0].GetProperty("slots").GetProperty("headline").GetString().ShouldBe("Plan");
    }

    [Test]
    public async Task Get_WhenLanguageIsGiven_ShouldKeepThePlayShape()
    {
        var deck = await SeedAsync(enabled: true, ("/headline", "แผน", "Plan"));

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}?language=th");

        JsonShape.AssertMatches(play, "deck-play.json", ScreenShapes.DeckPlay);
        play.GetProperty("id").GetString().ShouldBe(deck);
        play.GetProperty("isOwner").GetBoolean().ShouldBeTrue();
        play.GetProperty("slides")[0].GetProperty("number").GetInt32().ShouldBe(1);
    }

    // A deck the test client owns, one bullets slide, translations in Thai (on or off), and these Thai lines on slide 1.
    private async Task<string> SeedAsync(bool enabled, params (string Path, string Text, string From)[] thai)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", PLAN);
        var rows = new TranslationRowSeed(Factory.Services);
        await rows.SettingsAsync(deck, enabled, "en", "th");
        await rows.LinesAsync(deck, 1, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = thai.ToDictionary(line => line.Path, line => (line.Text, line.From)),
        });
        return deck;
    }
}
