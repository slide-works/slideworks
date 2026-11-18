using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L8 through PUT /api/decks/{id}/look: one call is one activity entry and one DeckChanged, and the revision goes up. The
// rules are LookRules' (L3, D9), the logo is a completed image of this deck (K11), and chrome overrides merge (D17). The
// catalogue holds the built-ins plus LockingTemplateCatalogue's locking template.
[TestFixture]
public class SetLookTests : ApiTestFixture
{
    private readonly LockingTemplateCatalogue _templates = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<ITemplateCatalogue>(_templates);

    [Test]
    public async Task Put_WhenPaletteOnly_ShouldChangeItAndRecordOneEntry()
    {
        var deck = await CreateDeckAsync();

        var result = await PutOkAsync(deck, new { palette = "ember" });

        result.GetProperty("look").GetProperty("palette").GetProperty("name").GetString().ShouldBe("ember");
        result.GetProperty("look").GetProperty("template").GetProperty("name").GetString().ShouldBe("talk");
        result.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        (await SummariesAsync(deck))[0].ShouldBe("changed the palette to Ember");
    }

    [Test]
    public async Task Put_WhenTemplateOnly_ShouldKeepThePalette()
    {
        var deck = await CreateDeckAsync(palette: "plum");

        var look = (await PutOkAsync(deck, new { template = "workshop" })).GetProperty("look");

        look.GetProperty("template").GetProperty("name").GetString().ShouldBe("workshop");
        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe("plum");
        (await SummariesAsync(deck))[0].ShouldBe("changed the template to workshop");
    }

    [Test]
    public async Task Put_WhenTemplateAndPalette_ShouldRecordOneEntryWithJoinedClauses()
    {
        var deck = await CreateDeckAsync();

        await PutOkAsync(deck, new { template = "workshop", palette = "ember" });

        var summaries = await SummariesAsync(deck);
        summaries.ShouldBe(["changed the template to workshop and the palette to Ember", "created the deck from the talk template with the Cobalt palette"]);
        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("via").GetString().ShouldBe("web");
        entry.GetProperty("tool").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Put_WhenDraft_ShouldSetAndClearIt()
    {
        var deck = await CreateDeckAsync();

        (await PutOkAsync(deck, new { draft = true })).GetProperty("look").GetProperty("draft").GetBoolean().ShouldBeTrue();
        (await PutOkAsync(deck, new { draft = false })).GetProperty("look").GetProperty("draft").GetBoolean().ShouldBeFalse();

        (await SummariesAsync(deck)).Take(2).ShouldBe(["changed the draft mark to off", "changed the draft mark to on"]);
    }

    [Test]
    public async Task Put_WhenChromeKeysChangeAndReset_ShouldMergeIntoTheOverrides()
    {
        var deck = await CreateDeckAsync(template: "workshop");
        var workshop = _templates.Find("workshop")!.Chrome;

        await PutOkAsync(deck, new { chrome = new { tracker = !workshop.Tracker, footer = "date" } });
        var look = (await PutOkAsync(deck, new { chrome = new { progress = !workshop.Progress } })).GetProperty("look");

        var overrides = look.GetProperty("chromeOverrides");
        overrides.GetProperty("tracker").GetBoolean().ShouldBe(!workshop.Tracker);
        overrides.GetProperty("footer").GetString().ShouldBe("date");
        overrides.GetProperty("progress").GetBoolean().ShouldBe(!workshop.Progress);
        look.GetProperty("template").GetProperty("chrome").GetProperty("tracker").GetBoolean().ShouldBe(!workshop.Tracker);

        using var reset = JsonDocument.Parse("""{ "chrome": { "tracker": null, "footer": null } }""");
        look = (await PutOkAsync(deck, reset.RootElement)).GetProperty("look");
        overrides = look.GetProperty("chromeOverrides");
        overrides.GetProperty("tracker").ValueKind.ShouldBe(JsonValueKind.Null);
        overrides.GetProperty("footer").ValueKind.ShouldBe(JsonValueKind.Null);
        overrides.GetProperty("progress").GetBoolean().ShouldBe(!workshop.Progress);
        look.GetProperty("template").GetProperty("chrome").GetProperty("tracker").GetBoolean().ShouldBe(workshop.Tracker);
        look.GetProperty("template").GetProperty("chrome").GetProperty("footer").GetString().ShouldBe(workshop.Footer);
        (await SummariesAsync(deck))[0].ShouldBe("changed the tracker to the template's default and the footer to the template's default");
    }

    [Test]
    public async Task Put_WhenLogoSetAndCleared_ShouldShowItThenNot()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));

        var look = (await PutOkAsync(deck, new { logo = image })).GetProperty("look");
        look.GetProperty("logo").GetProperty("url").GetString().ShouldBe($"/api/images/{image}");

        using var clear = JsonDocument.Parse("""{ "logo": null }""");
        look = (await PutOkAsync(deck, clear.RootElement)).GetProperty("look");
        look.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SummariesAsync(deck)).Take(2).ShouldBe(["removed the logo", "set the logo"]);
    }

    [Test]
    public async Task Put_WhenLogoIsAnotherDecksImage_ShouldRefuseIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var other = await DeckSeed.CreateDeckAsync(agent, title: "Other");
        var image = await ImageSeed.UploadAsync(agent, Factory, other, ImageSeed.Bytes("image/png"));

        (await PutErrorAsync(deck, new { logo = image })).ShouldBe($"logo.image: unknown image {image}");
    }

    [Test]
    public async Task Put_WhenLogoUploadNotCompleted_ShouldRefuseIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = (await ImageSeed.StartAsync(agent, deck)).GetProperty("image").GetString()!;

        (await PutErrorAsync(deck, new { logo = started })).ShouldBe($"logo.image: unknown image {started}");
    }

    [TestCase("""{ "template": "nope" }""", "Unknown template 'nope'")]
    [TestCase("""{ "palette": "nope" }""", "Unknown palette 'nope'")]
    [TestCase("""{ "template": "locked", "palette": "cobalt" }""", "Template 'locked' locks its palette")]
    [TestCase("""{}""", "Nothing to change")]
    [TestCase("""{ "chrome": {} }""", "Nothing to change")]
    [TestCase("""{ "chrome": { "footer": "left" } }""", "chrome.footer: must be 'author', 'date', 'off' or null")]
    [TestCase("""{ "chrome": { "shadow": true } }""", "chrome: unknown key 'shadow'; expected tracker, footer or progress")]
    [TestCase("""{ "logo": 7 }""", "logo: must be an image id, or null to remove the logo")]
    public async Task Put_WhenRefused_ShouldReturn400WithTheRuleTextAndChangeNothing(string body, string error)
    {
        var deck = await CreateDeckAsync();
        var before = await PageAsync(deck);

        using var json = JsonDocument.Parse(body);
        (await PutErrorAsync(deck, json.RootElement)).ShouldBe(error);

        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("activity").GetArrayLength().ShouldBe(1);
    }

    [Test]
    public async Task Put_WhenDeckIsOnALockingTemplate_ShouldRefuseAnotherPalette()
    {
        var deck = await CreateDeckAsync(template: LockingTemplateCatalogue.LOCKING);

        (await PutErrorAsync(deck, new { palette = "plum" })).ShouldBe("Template 'locked' locks its palette");
    }

    [Test]
    public async Task Put_WhenSwitchingToALockingTemplate_ShouldSwitchThePaletteToo()
    {
        var deck = await CreateDeckAsync(palette: "plum");

        var look = (await PutOkAsync(deck, new { template = LockingTemplateCatalogue.LOCKING })).GetProperty("look");

        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe(_templates.Palette);
        var label = new PaletteCatalogue().Find(_templates.Palette)!.Label;
        (await SummariesAsync(deck))[0].ShouldBe($"changed the template to locked and the palette to {label}");
    }

    [Test]
    public async Task Put_WhenTemplateLeavesSlidesOutside_ShouldWarnAboutEachOne()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Ship smaller");
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Why", "items": ["One", "Two"] }""");
        await DeckSeed.AddQuoteAsync(agent, deck, "Again");

        var result = await PutOkAsync(deck, new { template = "update" });

        result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldBe([
            "Slide 1: layout 'quote' is not in the 'update' template",
            "Slide 3: layout 'quote' is not in the 'update' template",
        ]);
        result.GetProperty("look").GetProperty("template").GetProperty("name").GetString().ShouldBe("update");
    }

    [Test]
    public async Task Put_WhenNotTheOwner_ShouldReturn404()
    {
        var deck = await CreateDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PutAsJsonAsync($"/api/decks/{deck}/look", new { palette = "ember" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Deck not found");
        (await PageAsync(deck)).GetProperty("look").GetProperty("palette").GetProperty("name").GetString().ShouldBe("cobalt");
    }

    [Test]
    public async Task Put_WhenChanged_ShouldPublishOneDeckChangedAndRaiseTheRevision()
    {
        var deck = await CreateDeckAsync();
        var before = (await PageAsync(deck)).GetProperty("revision").GetInt64();
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await PutOkAsync(deck, new { template = "workshop", palette = "ember", draft = false, chrome = new { tracker = false } });

        (await received.Reader.ReadAsync()).ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        received.Reader.TryRead(out _).ShouldBeFalse();
        (await PageAsync(deck)).GetProperty("revision").GetInt64().ShouldBe(before + 1);
        (await SummariesAsync(deck)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Put_ShouldMatchTheLookChangeContract()
    {
        var deck = await CreateDeckAsync();

        var result = await PutOkAsync(deck, new { template = "workshop", palette = "ember", chrome = new { tracker = false, footer = "date" } });

        using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "look-change.json")));
        var options = new JsonShapeOptions { NullablePaths = ["look.template.titleArt", "look.logo", "look.chromeOverrides.progress", .. ScreenShapes.LookNullablePaths] };
        Should.NotThrow(() => JsonShape.AssertMatches(result, contract.RootElement.GetProperty("response"), "look-change.json response", options));
    }

    private async Task<string> CreateDeckAsync(string? template = null, string? palette = null)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Look", template, palette });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> PutOkAsync<T>(string deck, T body)
    {
        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> PutErrorAsync<T>(string deck, T body)
    {
        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", body);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;
    }

    private Task<JsonElement> PageAsync(string deck) => Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

    private async Task<IReadOnlyList<string>> SummariesAsync(string deck)
        => (await DeckSeed.ActivityAsync(Client, deck)).Select(e => e.GetProperty("summary").GetString()!).ToList();
}
