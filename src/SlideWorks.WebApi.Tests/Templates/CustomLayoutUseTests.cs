using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Layouts;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Custom layouts in use (SW-477): a deck on a template with custom layouts validates slides against them, carries their
// steps and surface in every view, their tree and CSS in the look, lists them, and never uses one that is not approved,
// that belongs to another template or that the deck's pinned version does not have. The layouts come from the fake of
// ITemplateLayouts; the templates are seeded rows. Each test uses a template of its own.
[TestFixture]
[CancelAfter(60000)]
public class CustomLayoutUseTests : ApiTestFixture
{
    private const string DIAL = "dial-board";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplateLayouts.AddTo(services);
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());
    };

    private FakeTemplateLayouts Layouts => Factory.Services.GetRequiredService<FakeTemplateLayouts>();

    [Test]
    public async Task AddSlide_WhenTheLayoutIsCustom_ShouldValidateItsSlots()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;

        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "Dials", items = new[] { "a", "b" } } });
        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = new string('x', 21), items = new[] { "a" } } });

        error.ShouldContain($"{DIAL}.headline");
        (await SlideCalls.SlidesAsync(agent, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task UpdateSlide_WhenTheLayoutIsCustom_ShouldValidateItsSlots()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;
        var added = await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "Dials", items = new[] { "a" } } });
        var revision = added.GetProperty("revision").GetInt64();

        var error = await SlideCalls.ErrorAsync(agent, "update_slide", new { deck, slide = 1, revision, slots = new { headline = new string('x', 21), items = new[] { "a" } } });

        error.ShouldContain($"{DIAL}.headline");
        var ok = await SlideCalls.UpdateAsync(agent, deck, 1, revision, """{"headline":"Fine","items":["a","b"]}""");
        ok.GetProperty("warnings").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheLayoutIsCustom_ShouldNotWarnAboutTheTemplate()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;

        var added = await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "Dials", items = new[] { "a" } } });

        added.GetProperty("warnings").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheLayoutBelongsToAnotherTemplate_ShouldRefuseAsUnknown()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync();
        await using var _a = agent;
        var other = await SeedTemplateAsync();
        Layouts.Add(other, 1, Layout("elsewhere", LayoutSurface.Light));

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "elsewhere", slots = new { headline = "x", items = new[] { "a" } } });
        var unknown = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "never-heard-of", slots = new { headline = "x", items = new[] { "a" } } });

        error.ShouldBe(unknown.Replace("never-heard-of", "elsewhere", StringComparison.Ordinal));
        (await SlideCalls.SlidesAsync(agent, deck)).Count.ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheLayoutIsNotApproved_ShouldRefuseAsUnknown()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light, approved: false));
        await using var _a = agent;

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a" } } });
        var unknown = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "never-heard-of", slots = new { headline = "x", items = new[] { "a" } } });

        error.ShouldBe(unknown.Replace("never-heard-of", DIAL, StringComparison.Ordinal));
    }

    [Test]
    public async Task AddSlide_WhenTheDecksVersionDoesNotHaveTheLayout_ShouldRefuseAsUnknown()
    {
        var id = await SeedTemplateAsync();
        Layouts.Add(id, 2, Layout(DIAL, LayoutSurface.Light));
        await AddVersionAsync(id, 2);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOnAsync(agent, id, 1);

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a" } } });

        error.ShouldContain(DIAL);
        (await SlideCalls.SlidesAsync(agent, deck)).Count.ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheLayoutIsLightOnlyAndDarkIsAsked_ShouldRefuse()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = DIAL, surface = "dark", slots = new { headline = "x", items = new[] { "a" } } });

        error.ShouldContain($"{DIAL}.surface: this layout is light only");
    }

    [Test]
    public async Task AddSlide_WhenTheLayoutIsEitherAndDarkIsAsked_ShouldStoreDark()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Either));
        await using var _a = agent;

        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, surface = "dark", slots = new { headline = "x", items = new[] { "a" } } });

        (await StoredSurfaceAsync(deck)).ShouldBe("dark");
    }

    [Test]
    public async Task AddSlide_WhenTheCustomLayoutIsEitherAndNoSurfaceIsAsked_ShouldStoreNone()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Either));
        await using var _a = agent;

        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a" } } });

        (await StoredSurfaceAsync(deck)).ShouldBeNull();
    }

    [Test]
    public async Task Play_WhenTheSlideIsOnACustomLayout_ShouldCarryItsStepsAndSurface()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Dark));
        await using var _a = agent;
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a", "b", "c", "d" }, reveal = "one-by-one" } });

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        var slide = play.GetProperty("slides")[0];
        slide.GetProperty("steps").GetInt32().ShouldBe(4);
        slide.GetProperty("surface").GetString().ShouldBe("dark");
    }

    [Test]
    public async Task DeckPage_WhenTheSlideIsOnACustomLayout_ShouldCarryItsSteps()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a", "b", "c", "d" }, reveal = "one-by-one" } });

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        page.GetProperty("slides")[0].GetProperty("steps").GetInt32().ShouldBe(4);
    }

    [Test]
    public async Task Play_WhenTheDeckUsesACustomLayout_ShouldCarryTreeAndCss()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        var custom = play.GetProperty("look").GetProperty("customLayouts");
        custom.GetArrayLength().ShouldBe(1);
        var entry = custom[0];
        entry.GetProperty("name").GetString().ShouldBe(DIAL);
        entry.GetProperty("surface").GetString().ShouldBe("light");
        entry.GetProperty("css").GetString()!.ShouldContain("root");
        entry.GetProperty("slots").GetProperty("headline").GetProperty("type").GetString().ShouldBe("text");
        entry.GetProperty("tree").GetRawText().ShouldNotContain("<", Case.Sensitive);
        entry.GetProperty("tree").GetProperty("scope").GetString().ShouldBe("0a1b2c3d");
    }

    [Test]
    public async Task Play_WhenTheTemplateIsBuiltIn_ShouldSendNoCustomLayouts()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        var custom = play.GetProperty("look").GetProperty("customLayouts");
        custom.ValueKind.ShouldBe(JsonValueKind.Array);
        custom.GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Play_WhenTheDeckIsPinnedToVersionOne_ShouldCarryVersionOnesLayouts()
    {
        var id = await SeedTemplateAsync();
        Layouts.Add(id, 1, Layout("old-dial", LayoutSurface.Light));
        Layouts.Add(id, 2, Layout("new-dial", LayoutSurface.Light));
        await AddVersionAsync(id, 2);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOnAsync(agent, id, 1);

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        play.GetProperty("look").GetProperty("customLayouts").EnumerateArray().Select(l => l.GetProperty("name").GetString()).ShouldBe(["old-dial"]);
    }

    [Test]
    public async Task Play_WhenALayoutIsNotApproved_ShouldNotCarryIt()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout("approved-one", LayoutSurface.Light), Layout("draft-one", LayoutSurface.Light, approved: false));
        await using var _a = agent;

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        play.GetProperty("look").GetProperty("customLayouts").EnumerateArray().Select(l => l.GetProperty("name").GetString()).ShouldBe(["approved-one"]);
    }

    [Test]
    public async Task Viewer_WhenTheTemplateIsPrivate_ShouldStillGetTheLayoutInTheLook()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email);
        using var alex = ClientFor(TestUser.Alex);

        var play = await alex.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        play.GetProperty("look").GetProperty("customLayouts")[0].GetProperty("name").GetString().ShouldBe(DIAL);
    }

    [Test]
    public async Task Guest_WhenTheSlideIsOnADarkCustomLayout_ShouldCarryDark()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Dark));
        await using var _a = agent;
        await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a" } } });
        (await StoredSurfaceAsync(deck)).ShouldBe("dark");
        var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        live.EnsureSuccessStatusCode();
        var code = (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        Factory.Services.GetRequiredService<SettablePresentations>().Set(SettablePresentations.At(deck, slide: 1));
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        joined.GetProperty("current").GetProperty("surface").GetString().ShouldBe("dark");
    }

    [Test]
    public async Task ListLayouts_WhenTheDeckIsOnACustomTemplate_ShouldListItsLayoutsLast()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light), Layout("not-yet", LayoutSurface.Light, approved: false));
        await using var _a = agent;

        var result = await SlideCalls.OkAsync(agent, "list_layouts", new { deck });

        var rows = result.GetProperty("layouts").EnumerateArray().ToList();
        rows[^1].GetProperty("name").GetString().ShouldBe(DIAL);
        rows[^1].GetProperty("custom").GetBoolean().ShouldBeTrue();
        rows[^1].GetProperty("steps").GetString().ShouldNotBeNull();
        rows[^1].GetProperty("schema").GetProperty("properties").TryGetProperty("headline", out _).ShouldBeTrue();
        rows.Count(r => r.GetProperty("custom").GetBoolean()).ShouldBe(1);
        rows.Select(r => r.GetProperty("name").GetString()).ShouldNotContain("not-yet");
    }

    [Test]
    public async Task ListLayouts_WhenTheDeckIsOnABuiltIn_ShouldListNoCustomLayout()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await SlideCalls.OkAsync(agent, "list_layouts", new { deck });

        result.GetProperty("layouts").EnumerateArray().ShouldAllBe(r => !r.GetProperty("custom").GetBoolean());
    }

    [Test]
    public async Task ListLayouts_WhenTheDecksTemplateIsArchived_ShouldStillListItsLayouts()
    {
        var (agent, deck, id) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Templates.SingleAsync(t => t.Id == id)).ArchivedAt = Clock.GetUtcNow();
            await db.SaveChangesAsync();
        }

        var result = await SlideCalls.OkAsync(agent, "list_layouts", new { deck });

        result.GetProperty("layouts").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldContain(DIAL);
    }

    [Test]
    public async Task ListLayouts_WhenAskedByDeckTheCallerCannotEdit_ShouldSayNotFound()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;
        await using var mira = await McpTestClient.ConnectAsAsync(Factory, TestUser.Mira);

        var error = await SlideCalls.ErrorAsync(mira, "list_layouts", new { deck });
        var missing = await SlideCalls.ErrorAsync(mira, "list_layouts", new { deck = "nodeck0000000" });

        error.ShouldBe(missing);
    }

    [Test]
    public async Task ListLayouts_WhenTwoTemplatesShareALayoutName_ShouldMeasureEachOnItsOwn()
    {
        var narrow = Layout("same-name", LayoutSurface.Light, itemMax: 6);
        var wide = Layout("same-name", LayoutSurface.Light, itemMax: 2);
        var (agentA, deckA, _) = await DeckWithLayoutAsync(narrow);
        await using var _a = agentA;
        var (agentB, deckB, _) = await DeckWithLayoutAsync(wide);
        await using var _b = agentB;

        var first = await SlideCalls.OkAsync(agentA, "list_layouts", new { deck = deckA });
        var second = await SlideCalls.OkAsync(agentB, "list_layouts", new { deck = deckB });

        Fits(first).ShouldBe(6);
        Fits(second).ShouldBe(2);
    }

    [Test]
    public async Task UploadAsset_WhenKindIsLayoutOnTheWire_ShouldReturnAnUploadUrl()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var created = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "upload_asset", $$"""{"template":"{{id}}","kind":"layout","filename":"dial.json"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var content = result.GetProperty("structuredContent");
        content.GetProperty("kind").GetString().ShouldBe("layout");
        content.GetProperty("upload_url").GetString().ShouldNotBeNullOrEmpty();
    }

    private static int Fits(JsonElement listed)
        => listed.GetProperty("layouts").EnumerateArray().Single(r => r.GetProperty("custom").GetBoolean())
            .GetProperty("capacity").EnumerateArray().Single(c => c.GetProperty("slot").GetString() == "items").GetProperty("fits").GetInt32();

    // A template with a published version 1 holding these layouts, and a deck of Sam's on it.
    private async Task<(McpTestClient Agent, string Deck, string Template)> DeckWithLayoutAsync(params TemplateLayout[] layouts)
    {
        var id = await SeedTemplateAsync();
        foreach (var layout in layouts)
        {
            Layouts.Add(id, 1, layout);
        }
        var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return (agent, await DeckOnAsync(agent, id, 1), id);
    }

    private async Task<string> SeedTemplateAsync()
    {
        var created = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        var draft = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{draft}");
        return await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, editor.GetProperty("manifest").GetRawText());
    }

    private async Task AddVersionAsync(string id, int version)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await db.TemplateVersions.AsNoTracking().SingleAsync(v => v.TemplateId == id && v.Version == 1);
        db.TemplateVersions.Add(new TemplateVersion { TemplateId = id, Version = version, ManifestJson = first.ManifestJson, PublishedAt = Clock.GetUtcNow() });
        await db.SaveChangesAsync();
    }

    private async Task<string> DeckOnAsync(McpTestClient agent, string templateId, int version)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Decks.SingleAsync(d => d.Id == deck);
        row.Template = templateId;
        row.TemplateVersion = version;
        await db.SaveChangesAsync();
        return deck;
    }

    private async Task<string?> StoredSurfaceAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Slides.AsNoTracking().SingleAsync(s => s.DeckId == deck && s.DeletedAt == null)).Surface;
    }

    // A compiled layout built around a definition made from the public slot types: a headline, a list of items that reveal
    // one by one, and the reveal switch. Each call gets an asset id of its own, so its scope is its own.
    private static TemplateLayout Layout(string name, LayoutSurface surface, bool approved = true, int itemMax = 6)
    {
        var definition = new LayoutDefinition(
            name,
            "A headline over a list of items.",
            surface,
            [SlotKit.Req("headline", new TextSlot(20)), SlotKit.Req("items", new ListSlot(new TextSlot(40), 1, itemMax)), SlotKit.Opt("reveal", SlotKit.Reveal())],
            steps: StepPlan.List("items"));
        var asset = "0a1b2c3d" + Guid.NewGuid().ToString("N")[..24];
        var tree = new LayoutTree(LayoutTree.VERSION, "0a1b2c3d", [new TreeNode("div", [$"cl-0a1b2c3d-{name}__root"], null, null, null, null, [], [], [])]);
        var surfaceName = surface switch { LayoutSurface.Dark => "dark", LayoutSurface.Either => "either", _ => "light" };
        var slots = """{"name":"NAME","description":"A headline over a list of items.","surface":"SURFACE","steps":"items","slots":{"headline":{"type":"text","max":20},"items":{"type":"list","min":1,"max":MAX,"of":{"type":"text","max":40}}}}"""
            .Replace("NAME", name, StringComparison.Ordinal).Replace("SURFACE", surfaceName, StringComparison.Ordinal).Replace("MAX", itemMax.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var compiled = new CompiledLayout(CompiledLayout.COMPILER_VERSION, name, "A headline over a list of items.", surfaceName, new CustomSteps(CustomSteps.LIST, "items", 0), definition, tree, $".cl-0a1b2c3d-{name}__root {{ color: var(--sw-ink); }} /* root */", slots, "{}", "{}");
        return new TemplateLayout(compiled, asset, approved, [], []);
    }
}
