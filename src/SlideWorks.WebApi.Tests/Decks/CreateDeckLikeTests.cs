using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// create_deck with like copies that deck's look onto a new empty deck. Template, palette, chrome, draft, logo bytes,
// talk length and colours come across; slides, title, subtitle, author and parts do not. like with a look argument, or a
// deck the caller does not own (even one shared by link), stores nothing.
[TestFixture]
public class CreateDeckLikeTests : ApiTestFixture
{
    private const string ACCENT = "#2f5beb";

    [Test]
    public async Task CreateDeck_WhenLikeNamesADeck_ShouldCopyTemplatePaletteLogoChromeDraftTalkLengthAndColours()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var source = (await SlideCalls.OkAsync(agent, "create_deck", new { title = "Source look", subtitle = "For the offsite", author = "Sam Rivera", parts = new[] { "Open", "Close" } })).GetProperty("deck").GetString().ShouldNotBeNull();
        await DeckSeed.AddQuoteAsync(agent, source, "Ship smaller");
        await SlideCalls.OkAsync(agent, "set_look", new { deck = source, colors = new { accent = ACCENT } });
        var logo = ImageSeed.Bytes("image/png");
        var sourceLogo = await ImageSeed.UploadAsync(agent, Factory, source, logo);
        await SlideCalls.OkAsync(agent, "set_look", new
        {
            deck = source,
            template = "workshop",
            palette = "ember",
            logo = sourceLogo,
            draft = false,
            chrome = new { tracker = false, footer = "date", progress = false },
        });
        await SlideCalls.OkAsync(agent, "set_talk_length", new { deck = source, minutes = 45 });
        var sourceDeck = await SlideCalls.OkAsync(agent, "get_deck", new { deck = source });
        var sourceActivity = (await DeckSeed.ActivityAsync(Client, source)).Count;

        var created = await SlideCalls.OkAsync(agent, "create_deck", new { title = "Copied look", like = source });

        created.EnumerateObject().Select(property => property.Name).ShouldBe(["deck", "url", "sharing", "look", "layouts", "outline", "house_style"], ignoreOrder: true);
        created.GetProperty("sharing").GetString().ShouldBe("private");
        created.GetProperty("look").GetProperty("template").GetString().ShouldBe("workshop");
        created.GetProperty("look").GetProperty("palette").GetString().ShouldBe("ember");
        created.GetProperty("look").GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        created.GetProperty("layouts").EnumerateArray().Select(layout => layout.GetString()).ShouldBe(new TemplateCatalogue().Find("workshop").ShouldNotBeNull().Layouts);
        var copy = created.GetProperty("deck").GetString().ShouldNotBeNull();
        copy.ShouldNotBe(source);

        var got = await SlideCalls.OkAsync(agent, "get_deck", new { deck = copy });
        got.GetProperty("title").GetString().ShouldBe("Copied look");
        got.GetProperty("subtitle").ValueKind.ShouldBe(JsonValueKind.Null);
        got.GetProperty("author").ValueKind.ShouldBe(JsonValueKind.Null);
        got.GetProperty("parts").GetArrayLength().ShouldBe(0);
        got.GetProperty("slides").GetArrayLength().ShouldBe(0);
        got.GetProperty("talk_minutes").GetInt32().ShouldBe(45);
        var look = got.GetProperty("look");
        look.GetProperty("template").GetString().ShouldBe("workshop");
        look.GetProperty("palette").GetString().ShouldBe("ember");
        look.GetProperty("draft").GetBoolean().ShouldBeFalse();
        look.GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("chrome").GetProperty("tracker").GetBoolean().ShouldBeFalse();
        look.GetProperty("chrome").GetProperty("footer").GetString().ShouldBe("date");
        look.GetProperty("chrome").GetProperty("progress").GetBoolean().ShouldBeFalse();
        var copyLogo = look.GetProperty("logo").GetString().ShouldNotBeNull();
        copyLogo.ShouldNotBe(sourceLogo);
        sourceDeck.GetProperty("look").GetProperty("logo").GetString().ShouldBe(sourceLogo);
        (await Client.GetByteArrayAsync($"/api/images/{copyLogo}")).ShouldBe(logo);
        (await Client.GetByteArrayAsync($"/api/images/{sourceLogo}")).ShouldBe(logo);
        (await SlideCalls.SlidesAsync(agent, source)).Count.ShouldBe(1);
        (await DeckSeed.ActivityAsync(Client, source)).Count.ShouldBe(sourceActivity);
        (await DeckSeed.ActivityAsync(Client, copy)).ShouldHaveSingleItem().GetProperty("summary").GetString()
            .ShouldBe("created the deck like \"Source look\" from the workshop template with the Ember palette");

        var sourceTokens = (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{source}")).GetProperty("look").GetProperty("palette").GetProperty("tokens");
        var copyTokens = (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{copy}")).GetProperty("look").GetProperty("palette").GetProperty("tokens");
        copyTokens.GetProperty("accent").GetString().ShouldBe(ACCENT);
        copyTokens.GetRawText().ShouldBe(sourceTokens.GetRawText());
    }

    [Test]
    public async Task CreateDeck_WhenLikeIsCombinedWithATemplate_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var source = await DeckSeed.CreateDeckAsync(agent, "Source look");
        var before = (await SlideCalls.OkAsync(agent, "get_deck", new { deck = source })).GetRawText();
        var decks = await DeckCountAsync(agent);

        (await SlideCalls.ErrorAsync(agent, "create_deck", new { title = "Copied", like = source, template = "workshop" }))
            .ShouldBe("like: leave template, palette and colors out; they come from that deck");

        (await SlideCalls.OkAsync(agent, "get_deck", new { deck = source })).GetRawText().ShouldBe(before);
        (await DeckCountAsync(agent)).ShouldBe(decks);
    }

    [Test]
    public async Task CreateDeck_WhenLikeIsCombinedWithAPalette_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var source = await DeckSeed.CreateDeckAsync(agent, "Source look");
        var before = (await SlideCalls.OkAsync(agent, "get_deck", new { deck = source })).GetRawText();
        var decks = await DeckCountAsync(agent);

        (await SlideCalls.ErrorAsync(agent, "create_deck", new { title = "Copied", like = source, palette = "ember" }))
            .ShouldBe("like: leave template, palette and colors out; they come from that deck");

        (await SlideCalls.OkAsync(agent, "get_deck", new { deck = source })).GetRawText().ShouldBe(before);
        (await DeckCountAsync(agent)).ShouldBe(decks);
    }

    [Test]
    public async Task CreateDeck_WhenLikeIsCombinedWithColors_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var source = await DeckSeed.CreateDeckAsync(agent, "Source look");
        var before = (await SlideCalls.OkAsync(agent, "get_deck", new { deck = source })).GetRawText();
        var decks = await DeckCountAsync(agent);

        (await SlideCalls.ErrorAsync(agent, "create_deck", new { title = "Copied", like = source, colors = new { accent = ACCENT } }))
            .ShouldBe("like: leave template, palette and colors out; they come from that deck");

        (await SlideCalls.OkAsync(agent, "get_deck", new { deck = source })).GetRawText().ShouldBe(before);
        (await DeckCountAsync(agent)).ShouldBe(decks);
    }

    [Test]
    public async Task CreateDeck_WhenLikeNamesADeckTheCallerDoesNotOwn_ShouldCreateNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var source = await DeckSeed.CreateDeckAsync(sam, "Sam's deck");
        var before = (await SlideCalls.OkAsync(sam, "get_deck", new { deck = source })).GetRawText();
        var samDecks = await DeckCountAsync(sam);
        var alexDecks = await DeckCountAsync(alex);

        (await SlideCalls.ErrorAsync(alex, "create_deck", new { title = "Stolen look", like = source })).ShouldBe("Deck not found");

        (await SlideCalls.OkAsync(sam, "get_deck", new { deck = source })).GetRawText().ShouldBe(before);
        (await DeckCountAsync(sam)).ShouldBe(samDecks);
        (await DeckCountAsync(alex)).ShouldBe(alexDecks);
    }

    [Test]
    public async Task CreateDeck_WhenLikeNamesALinkSharedDeckTheCallerDoesNotOwn_ShouldCreateNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var source = await DeckSeed.CreateDeckAsync(sam, "Sam's shared deck");
        await SlideCalls.OkAsync(sam, "set_sharing", new { deck = source, level = "link" });
        var alexDecks = await DeckCountAsync(alex);

        (await SlideCalls.ErrorAsync(alex, "create_deck", new { title = "Borrowed look", like = source })).ShouldBe("Deck not found");

        (await DeckCountAsync(alex)).ShouldBe(alexDecks);
    }

    private static async Task<int> DeckCountAsync(McpTestClient agent)
        => (await SlideCalls.OkAsync(agent, "list_decks", new { })).GetProperty("decks").GetArrayLength();
}
