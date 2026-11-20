using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// K6 read side: the play view, the deck page, get_deck and the presenter view each carry every slide's resolved surface,
// "light" or "dark": the one the slide stores, else its layout's own as the deck's template finds the layout. A layout
// that takes either surface is light until the slide stores one.
[TestFixture]
public class SlideSurfaceReadTests : ApiTestFixture
{
    private const string TITLE = """{ "title": "Ship smaller" }""";
    private const string BULLETS = """{ "headline": "Why", "items": ["a", "b"] }""";
    private const string STATEMENT = """{ "statement": "Small batches win." }""";

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    [Test]
    public async Task Play_WhenTheSlideHasNoSurface_ShouldCarryTheLayoutsOwn()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        play.GetProperty("slides")[0].GetProperty("surface").GetString().ShouldBe("light");
    }

    [Test]
    public async Task Play_WhenTheSlideIsATitle_ShouldSayDark()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "title", TITLE);

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        play.GetProperty("slides")[0].GetProperty("surface").GetString().ShouldBe("dark");
    }

    [Test]
    public async Task Reads_WhenTheSlidesStoreNoSurface_ShouldCarryEachLayoutsOwnOnEveryRead()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "title", TITLE);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        await DeckSeed.AddSlideAsync(agent, deck, "statement", STATEMENT);

        var surfaces = await SurfacesAsync(agent, deck);

        surfaces.Keys.ShouldBe(["play", "page", "get_deck", "presenter"]);
        surfaces["play"].ShouldBe(["dark", "light", "light"]);
        surfaces["page"].ShouldBe(["dark", "light", "light"]);
        surfaces["get_deck"].ShouldBe(["dark", "light", "light"]);
        surfaces["presenter"].ShouldBe(["dark", "light", "light"]);
    }

    [Test]
    public async Task Reads_WhenTheSlideStoresDark_ShouldSayDarkOnEveryRead()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "statement", STATEMENT);
        await DeckSeed.AddSlideAsync(agent, deck, "statement", STATEMENT);
        await StoreSurfaceAsync(deck, position: 1, "dark");

        var surfaces = await SurfacesAsync(agent, deck);

        surfaces["play"].ShouldBe(["light", "dark"]);
        surfaces["page"].ShouldBe(["light", "dark"]);
        surfaces["get_deck"].ShouldBe(["light", "dark"]);
        surfaces["presenter"].ShouldBe(["light", "dark"]);
    }

    // The layout is found through the deck's template, never the catalogue: here the update template answers a dark
    // bullets layout of its own, and every read follows it. The fake is shared by the fixture, so only this test uses
    // the update template.
    [Test]
    public async Task Reads_WhenTheTemplateFindsItsOwnLayout_ShouldCarryThatLayoutsSurface()
    {
        var darkBullets = new LayoutDefinition("bullets", "Bullets on a dark slide.", LayoutSurface.Dark, BulletsLayout.Definition.Slots.Fields);
        Factory.Services.GetRequiredService<FakeTemplates>().Add(BuiltInTemplates.Resolve(new TemplateCatalogue().Find("update")!) with
        {
            Layouts = name => string.Equals(name, "bullets", StringComparison.Ordinal) ? darkBullets : LayoutCatalogue.Find(name),
        });
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, created) = await agent.CallToolAsync("create_deck", new { title = "Own layout", template = "update" });
        var deck = created.GetProperty("deck").GetString()!;
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        await DeckSeed.AddSlideAsync(agent, deck, "quote", """{ "quote": "Ship it.", "attribution": "Sam Rivera" }""");

        var surfaces = await SurfacesAsync(agent, deck);

        isError.ShouldBeFalse(created.ToString());
        surfaces["play"].ShouldBe(["dark", "light"]);
        surfaces["page"].ShouldBe(["dark", "light"]);
        surfaces["get_deck"].ShouldBe(["dark", "light"]);
        surfaces["presenter"].ShouldBe(["dark", "light"]);
    }

    // The raw JSON-RPC result, as an agent's client reads it: surface is a plain lower-case string on each slide.
    [Test]
    public async Task GetDeck_WhenCalledOnTheWire_ShouldCarryEachSlidesSurface()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "title", TITLE);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_deck", $$"""{"deck":"{{deck}}"}""");

        var slides = result.GetProperty("structuredContent").GetProperty("slides");
        slides[0].GetProperty("surface").ValueKind.ShouldBe(JsonValueKind.String);
        slides[0].GetProperty("surface").GetString().ShouldBe("dark");
        slides[1].GetProperty("surface").GetString().ShouldBe("light");
    }

    // Each slide's surface, in slide order, on each of the four reads.
    private async Task<Dictionary<string, List<string?>>> SurfacesAsync(McpTestClient agent, string deck)
    {
        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
        var (_, forAgent) = await agent.CallToolAsync("get_deck", new { deck });
        var presenter = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/presenter");
        return new Dictionary<string, List<string?>>
        {
            ["play"] = SurfacesOf(play),
            ["page"] = SurfacesOf(page),
            ["get_deck"] = SurfacesOf(forAgent),
            ["presenter"] = SurfacesOf(presenter),
        };
    }

    private static List<string?> SurfacesOf(JsonElement view)
        => view.GetProperty("slides").EnumerateArray().Select(slide => slide.GetProperty("surface").GetString()).ToList();

    // TODO: add_slide's surface argument replaces this direct row update once slide writes take a surface.
    private async Task StoreSurfaceAsync(string deck, int position, string surface)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Slides.Where(s => s.DeckId == deck && s.Position == position).ExecuteUpdateAsync(s => s.SetProperty(slide => slide.Surface, surface));
    }
}
