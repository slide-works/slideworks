using System.Net;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// SW-568: an agent copies a look, uploads images, and finishes a poll in the calls that create them.
[TestFixture]
[CancelAfter(30000)]
public class AgentRoundTripTests : ApiTestFixture
{
    [Test]
    public async Task CreateDeck_WhenLikeNamesADeck_ShouldCopyItsLookAndLogo()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var source = await DeckSeed.CreateDeckAsync(agent, "The reference");
        var bytes = ImageSeed.SamplePng();
        var logo = await ImageSeed.UploadAsync(agent, Factory, source, bytes, name: "logo.png");
        await SlideCalls.OkAsync(agent, "set_look", new { deck = source, palette = "ember", logo, draft = true });

        var created = await SlideCalls.OkAsync(agent, "create_deck", new { title = "The copy", like = source });

        var deck = created.GetProperty("deck").GetString()!;
        var look = (await SlideCalls.OkAsync(agent, "get_deck", new { deck })).GetProperty("look");
        look.GetProperty("template").GetString().ShouldBe("talk");
        look.GetProperty("palette").GetString().ShouldBe("ember");
        look.GetProperty("draft").GetBoolean().ShouldBeTrue();
        var copied = look.GetProperty("logo").GetString()!;
        copied.ShouldNotBe(logo);
        using var served = await Client.GetAsync($"/api/images/{copied}");
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString()!.ShouldContain("like \"The reference\"");
    }

    [Test]
    public async Task CreateDeck_WhenLikeAlsoNamesATemplate_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var source = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "create_deck", new { title = "Both", like = source, template = "talk" });

        error.ShouldBe("like: leave template, palette and colors out; they come from that deck");
    }

    [Test]
    public async Task UploadImages_WhenSeveralFiles_ShouldReturnEveryUploadUrl()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await SlideCalls.OkAsync(agent, "upload_images", new
        {
            deck,
            files = new[]
            {
                new { name = "one.png", contentType = "image/png" },
                new { name = "two.png", contentType = "image/png" },
            },
        });

        var images = result.GetProperty("images").EnumerateArray().ToList();
        images.Count.ShouldBe(2);
        images.Select(image => image.GetProperty("name").GetString()).ShouldBe(["one.png", "two.png"]);
        foreach (var image in images)
        {
            using var put = await ImageSeed.PutAsync(Factory, image.GetProperty("uploadUrl").GetString()!, ImageSeed.Bytes("image/png"));
            put.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await DeckSeed.ActivityAsync(Client, deck)).ShouldContain(entry => entry.GetProperty("summary").GetString() == "started uploading 2 images");
    }

    [Test]
    public async Task AddSlide_WhenPollSettingsAreGiven_ShouldStoreThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var slots = JsonDocument.Parse("""{ "question": "Which part should we cut?", "options": ["The intro", "The demo", "The recap", "The close"] }""");

        await SlideCalls.OkAsync(agent, "add_slide", new
        {
            deck,
            layout = "poll",
            slots = slots.RootElement,
            poll = new { pick = "upTo", max = 2, correct = 1, results = "afterClose", opens = "manual" },
        });

        using var response = await Client.GetAsync($"/api/decks/{deck}/polls/1");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var settings = body.RootElement.GetProperty("settings");
        settings.GetProperty("pick").GetString().ShouldBe("upTo");
        settings.GetProperty("max").GetInt32().ShouldBe(2);
        settings.GetProperty("correct").GetInt32().ShouldBe(1);
        settings.GetProperty("results").GetString().ShouldBe("afterClose");
        settings.GetProperty("opens").GetString().ShouldBe("manual");
    }

    [Test]
    public async Task AddSlide_WhenAnswersAreCloud_ShouldStoreAnOpenQuestion()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var slots = JsonDocument.Parse("""{ "question": "One word for the quarter", "options": [] }""");

        await SlideCalls.OkAsync(agent, "add_slide", new
        {
            deck,
            layout = "poll",
            slots = slots.RootElement,
            poll = new { answers = "cloud" },
        });

        using var response = await Client.GetAsync($"/api/decks/{deck}/polls/1");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("settings").GetProperty("answers").GetString().ShouldBe("cloud");
    }

    [Test]
    public async Task AddSlide_WhenAChoicePollHasNoOptions_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var slots = JsonDocument.Parse("""{ "question": "Which part?", "options": [] }""");

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "poll", slots = slots.RootElement });

        error.ShouldBe("options: a choice poll needs 2 to 8");
    }

    [Test]
    public async Task AddSlide_WhenStyleIsNumbered_ShouldStoreIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var slots = JsonDocument.Parse("""{ "headline": "The order", "items": ["First", "Second"], "style": "numbered" }""");

        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = "bullets", slots = slots.RootElement });

        var stored = (await SlideCalls.SlidesAsync(agent, deck))[0].GetProperty("slots");
        stored.GetProperty("style").GetString().ShouldBe("numbered");
    }

    [Test]
    public async Task ListLayouts_WhenCompact_ShouldOmitTheSchema()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var compact = await SlideCalls.OkAsync(agent, "list_layouts", new { detail = "compact" });
        var full = await SlideCalls.OkAsync(agent, "list_layouts", new { });

        compact.TryGetProperty("$defs", out _).ShouldBeFalse();
        compact.GetProperty("layouts")[0].TryGetProperty("schema", out _).ShouldBeFalse();
        compact.GetProperty("layouts")[0].GetProperty("slots").GetArrayLength().ShouldBeGreaterThan(0);
        var iconEnum = full.GetProperty("$defs").GetProperty("icon").GetProperty("enum").GetRawText();
        iconEnum.Length.ShouldBeGreaterThan(20);
        // The icon catalogue is written once, in $defs. A new layout must not copy it back into every schema.
        full.GetRawText().Split(iconEnum).Length.ShouldBe(2);
    }

    [Test]
    public async Task GetDeck_WhenSummary_ShouldReturnHeadlinesOnly()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var slots = JsonDocument.Parse("""{ "headline": "Small batches", "items": ["They ship."] }""");
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = "bullets", slots = slots.RootElement });

        var summary = await SlideCalls.OkAsync(agent, "get_deck", new { deck, detail = "summary" });

        var slide = summary.GetProperty("slides")[0];
        slide.GetProperty("number").GetInt32().ShouldBe(1);
        slide.GetProperty("layout").GetString().ShouldBe("bullets");
        slide.GetProperty("headline").GetString().ShouldBe("Small batches");
        slide.TryGetProperty("slots", out _).ShouldBeFalse();
    }
}
