using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L7, L8 through set_look: the same path as PUT /api/decks/{id}/look, so the same rules and the same error texts as tool
// errors. The look comes back in get_deck's terms, and the activity row names the tool.
[TestFixture]
public class SetLookToolTests : ApiTestFixture
{
    private static readonly JsonElement NULL = JsonDocument.Parse("null").RootElement;

    private readonly LockingTemplateCatalogue _templates = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<ITemplateCatalogue>(_templates);

    [Test]
    public async Task SetLook_WhenTemplateAndPalette_ShouldReturnTheLookInGetDecksTermsAndRecordTheTool()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await SetLookAsync(agent, new { deck, template = "workshop", palette = "ember", draft = false, chrome = new { tracker = false } });

        var look = result.GetProperty("look");
        look.GetProperty("template").GetString().ShouldBe("workshop");
        look.GetProperty("palette").GetString().ShouldBe("ember");
        look.GetProperty("draft").GetBoolean().ShouldBeFalse();
        look.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("chrome").GetProperty("tracker").GetBoolean().ShouldBeFalse();
        look.GetProperty("chrome").GetProperty("footer").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("updateAvailable").GetBoolean().ShouldBeFalse();
        result.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var (_, got) = await agent.CallToolAsync("get_deck", new { deck });
        got.GetProperty("look").ToString().ShouldBe(look.ToString());

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("changed the template to workshop, the palette to Ember, the draft mark to off and the tracker to off");
        entry.GetProperty("tool").GetString().ShouldBe("set_look");
        entry.GetProperty("via").GetString().ShouldBe("mcp");
        entry.GetProperty("actorKind").GetString().ShouldBe("agent");
    }

    [Test]
    public async Task SetLook_WhenChromeKeyIsNull_ShouldResetItAndKeepTheOthers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await SetLookAsync(agent, new { deck, chrome = new { tracker = false, footer = "date" } });

        var look = (await SetLookAsync(agent, new { deck, chrome = new { tracker = NULL } })).GetProperty("look");

        look.GetProperty("chrome").GetProperty("tracker").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("chrome").GetProperty("footer").GetString().ShouldBe("date");
    }

    [Test]
    public async Task SetLook_WhenLogoSetThenNull_ShouldSetAndRemoveIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));

        (await SetLookAsync(agent, new { deck, logo = image })).GetProperty("look").GetProperty("logo").GetString().ShouldBe(image);
        (await SetLookAsync(agent, new { deck, logo = NULL })).GetProperty("look").GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task SetLook_WhenLogoIsAnotherDecksOrIncomplete_ShouldGiveTheApisText()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var other = await DeckSeed.CreateDeckAsync(agent, title: "Other");
        var foreign = await ImageSeed.UploadAsync(agent, Factory, other, ImageSeed.Bytes("image/png"));
        var started = (await ImageSeed.StartAsync(agent, deck)).GetProperty("image").GetString()!;

        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck, logo = foreign })).ShouldBe($"logo.image: unknown image {foreign}");
        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck, logo = started })).ShouldBe($"logo.image: unknown image {started}");
    }

    [Test]
    public async Task SetLook_WhenRefused_ShouldGiveTheRuleText()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck, template = "nope" })).ShouldBe("Unknown template 'nope'");
        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck, palette = "nope" })).ShouldBe("Unknown palette 'nope'");
        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck, template = LockingTemplateCatalogue.LOCKING, palette = "cobalt" }))
            .ShouldBe("Template 'locked' locks its palette");
        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck })).ShouldBe("Nothing to change");
        (await SlideCalls.ErrorAsync(agent, "set_look", new { deck, chrome = new { footer = "left" } }))
            .ShouldBe("chrome.footer: must be 'author', 'date', 'off' or null");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task SetLook_WhenSwitchingToALockingTemplate_ShouldSwitchThePalette()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var look = (await SetLookAsync(agent, new { deck, template = LockingTemplateCatalogue.LOCKING })).GetProperty("look");

        look.GetProperty("palette").GetString().ShouldBe(_templates.Palette);
    }

    [Test]
    public async Task SetLook_WhenTemplateLeavesSlidesOutside_ShouldWarn()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Ship smaller");

        var result = await SetLookAsync(agent, new { deck, template = "update" });

        result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldBe(["Slide 1: layout 'quote' is not in the 'update' template"]);
    }

    [Test]
    public async Task SetLook_WhenNotTheOwner_ShouldSayDeckNotFound()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);

        (await SlideCalls.ErrorAsync(alex, "set_look", new { deck, palette = "ember" })).ShouldBe("Deck not found");
    }

    [Test]
    public async Task SetLook_ShouldPublishOneDeckChanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await SetLookAsync(agent, new { deck, palette = "plum", draft = true });

        (await received.Reader.ReadAsync()).ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        received.Reader.TryRead(out _).ShouldBeFalse();
    }

    private static async Task<JsonElement> SetLookAsync(McpTestClient agent, object args)
    {
        var (isError, json) = await agent.CallToolAsync("set_look", args);
        isError.ShouldBeFalse(json.ToString());
        return json;
    }
}
