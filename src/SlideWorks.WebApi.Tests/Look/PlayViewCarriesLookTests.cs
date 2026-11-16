using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L6, L7: the play view, the deck page and get_deck carry the deck's look, and each slide its deepDive flag.
[TestFixture]
public class PlayViewCarriesLookTests : ApiTestFixture
{
    [Test]
    public async Task Play_ShouldCarryTheResolvedLookAndDeepDive()
    {
        var id = await CreateWithSlideAsync(template: "workshop", palette: "midnight");

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{id}");

        JsonShape.AssertMatches(play, "deck-play.json", ScreenShapes.DeckPlay);
        var look = play.GetProperty("look");
        look.GetProperty("template").GetProperty("name").GetString().ShouldBe("workshop");
        look.GetProperty("template").GetProperty("layouts").EnumerateArray().Select(l => l.GetString())
            .ShouldBe(new TemplateCatalogue().Find("workshop")!.Layouts);
        look.GetProperty("palette").GetProperty("tokens").EnumerateObject().Select(t => t.Name)
            .ShouldBe(new PaletteCatalogue().Find("midnight")!.Tokens.Keys, ignoreOrder: true);
        look.GetProperty("draft").GetBoolean().ShouldBeTrue();
        look.GetProperty("footerDateLabel").GetString().ShouldBe(DisplayDate.Day(Clock.GetUtcNow()));
        play.GetProperty("slides")[0].GetProperty("deepDive").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task DeckPage_ShouldCarryRevisionLookOverridesAndContrast()
    {
        var id = await CreateWithSlideAsync(template: "update", palette: "plum");

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}");

        JsonShape.AssertMatches(page, "deck-page.json", ScreenShapes.DeckPage);
        page.GetProperty("revision").GetInt64().ShouldBeGreaterThan(0);
        var look = page.GetProperty("look");
        look.GetProperty("template").GetProperty("chrome").GetProperty("footer").GetString().ShouldBe("date");
        look.GetProperty("chromeOverrides").EnumerateObject().ShouldAllBe(o => o.Value.ValueKind == JsonValueKind.Null);
        look.GetProperty("contrast").GetProperty("pass").GetBoolean().ShouldBeTrue();
        page.GetProperty("slides")[0].GetProperty("deepDive").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task GetDeck_ShouldCarryTheLookAndEachSlidesDeepDive()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (_, created) = await agent.CallToolAsync("create_deck", new { title = "Offsite", template = "workshop", palette = "ember" });
        var id = created.GetProperty("deck").GetString()!;
        await DeckSeed.AddSlideAsync(agent, id, "bullets", """{ "headline": "One", "items": ["a"] }""", notes: "Say a.");

        var (isError, deck) = await agent.CallToolAsync("get_deck", new { deck = id });

        isError.ShouldBeFalse(deck.ToString());
        var look = deck.GetProperty("look");
        look.GetProperty("template").GetString().ShouldBe("workshop");
        look.GetProperty("palette").GetString().ShouldBe("ember");
        look.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("draft").GetBoolean().ShouldBeTrue();
        look.GetProperty("chrome").EnumerateObject().Select(c => c.Name).ShouldBe(["tracker", "footer", "progress"], ignoreOrder: true);
        var slide = deck.GetProperty("slides")[0];
        slide.GetProperty("deep_dive").GetBoolean().ShouldBeFalse();
        slide.GetProperty("notes").GetString().ShouldBe("Say a.");
    }

    // A logo is set by set_look (A1); the view built from a deck with one names it root-relative, never through the
    // public URL, so a headless browser loads it from the host it renders on (L6).
    [Test]
    public void LookView_WhenDeckHasALogo_ShouldLinkItRootRelative()
    {
        var deck = new Deck { Id = "7Kq2xYp9LmQa", Slug = "s", Title = "T", PartsJson = "[]", Template = "talk", Palette = "cobalt", LogoImageId = "img_logo0001" };

        var look = LookViews.From(deck, Factory.Services.GetRequiredService<ITemplateCatalogue>(), Factory.Services.GetRequiredService<IPaletteCatalogue>());

        look.Logo!.Url.ShouldBe("/api/images/img_logo0001");
    }

    private async Task<string> CreateWithSlideAsync(string template, string palette)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, created) = await agent.CallToolAsync("create_deck", new { title = "Look", template, palette });
        isError.ShouldBeFalse(created.ToString());
        var id = created.GetProperty("deck").GetString()!;
        await DeckSeed.AddSlideAsync(agent, id, "bullets", """{ "headline": "One", "items": ["a"] }""", part: null);
        return id;
    }
}
