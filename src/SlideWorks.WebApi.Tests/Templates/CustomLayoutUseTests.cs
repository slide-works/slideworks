using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
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
// that belongs to another template or that the deck's pinned version does not have. Every template, layout, version and
// deck is made through the public routes (draft, layout upload, manifest PUT, approve, publish, create_deck, archive); the
// compiler is FakeCustomLayoutCompiler, because Core's sanitisers are not built yet. A state no route can create (a published
// layout whose approval is withdrawn) is written to the database, and says so where it is. Each test uses templates of its
// own. The compiler is shared by the fixture, so the tests do not run in parallel.
[TestFixture]
[NonParallelizable]
[CancelAfter(60000)]
public class CustomLayoutUseTests : ApiTestFixture
{
    private const string DIAL = "dial-board";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeCustomLayoutCompiler.AddTo(services);
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());
    };

    private FakeCustomLayoutCompiler Compiler => Factory.Services.GetRequiredService<FakeCustomLayoutCompiler>();

    // What a test asks of a layout: the compiled one is built from it for each upload.
    private sealed record LayoutSpec(string Name, LayoutSurface Surface, bool Approved, int ItemMax, bool AlsoListed = false);

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
        await PublishNewTemplateAsync(Layout("elsewhere", LayoutSurface.Light));

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "elsewhere", slots = new { headline = "x", items = new[] { "a" } } });
        var unknown = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "never-heard-of", slots = new { headline = "x", items = new[] { "a" } } });

        error.ShouldBe(unknown.Replace("never-heard-of", "elsewhere", StringComparison.Ordinal));
        (await SlideCalls.SlidesAsync(agent, deck)).Count.ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheLayoutIsUnknown_ShouldListTheApprovedCustomLayoutsOnly()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light), Layout("unapproved-one", LayoutSurface.Light, approved: false));
        await using var _a = agent;
        var slots = new { headline = "x", items = new[] { "a" } };

        var added = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "never-heard-of", slots });

        added.ShouldStartWith("layout: unknown layout 'never-heard-of' (known: ");
        added.ShouldEndWith($", {DIAL})");
        added.ShouldNotContain("unapproved-one");
    }

    // An uploaded template's refusal keeps the UTF-16 check ahead of the layout lookup.
    [Test]
    public async Task AddSlide_WhenTheLayoutIsUnknownAndSlotsHaveALoneSurrogate_ShouldRefuseTheText()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light));
        await using var _a = agent;

        var message = await McpWire.SendToolCallAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$$"""{"deck":"{{{deck}}}","layout":"never-heard-of","slots":{"headline":"Ship it \ud83d"}}""");

        McpWire.TextOf(message.GetProperty("result")).ShouldBe("slots.headline: text is not valid UTF-16");
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
        var id = await PublishNewTemplateAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOnAsync(agent, id);
        await PublishVersionAsync(id, Layout(DIAL, LayoutSurface.Light));

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a" } } });

        error.ShouldContain(DIAL);
        (await SlideCalls.SlidesAsync(agent, deck)).Count.ShouldBe(0);
    }

    [Test]
    public async Task AddSlide_WhenTheCustomNameIsAlsoInTheManifestsLayoutsList_ShouldUseTheCustomLayout()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Light, alsoListed: true));
        await using var _a = agent;

        var added = await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "Dials", items = new[] { "a", "b" } } });
        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = new string('x', 21), items = new[] { "a" } } });

        added.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        error.ShouldContain($"{DIAL}.headline");
        var listed = await SlideCalls.OkAsync(agent, "list_layouts", new { deck });
        var rows = listed.GetProperty("layouts").EnumerateArray().Where(r => r.GetProperty("name").GetString() == DIAL).ToList();
        rows.ShouldHaveSingleItem().GetProperty("custom").GetBoolean().ShouldBeTrue();
        listed.GetRawText().Split($"\"name\":\"{DIAL}\"").Length.ShouldBe(2, "the name appears once in the whole result, compact or not");
    }

    [Test]
    public async Task UpdateSlide_WhenMovingFromADarkCustomLayoutToALightOnlyOneWithNoSurface_ShouldStoreTheNewLayoutsSurface()
    {
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout(DIAL, LayoutSurface.Dark));
        await using var _a = agent;
        var added = await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout = DIAL, slots = new { headline = "x", items = new[] { "a" } } });
        (await StoredSurfaceAsync(deck)).ShouldBe("dark");

        await SlideCalls.UpdateAsync(agent, deck, 1, added.GetProperty("revision").GetInt64(), """{ "headline": "Ship smaller", "columns": ["Was", "Now"], "rows": [["Big", "Small"]] }""", layout: "table");

        (await StoredSurfaceAsync(deck)).ShouldBeNull();
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
        var scope = entry.GetProperty("tree").GetProperty("scope").GetString()!;
        scope.ShouldMatch("^[0-9a-f]{8}$");
        entry.GetProperty("css").GetString()!.ShouldContain($"cl-{scope}-{DIAL}__root");
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
        var id = await PublishNewTemplateAsync(Layout("old-dial", LayoutSurface.Light));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckOnAsync(agent, id);
        await PublishVersionAsync(id, Layout("new-dial", LayoutSurface.Light));

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
    public async Task Guest_WhenTheDeckUsesCustomLayouts_ShouldCarryTheApprovedOnesOfItsPinnedVersionOnly()
    {
        await PublishNewTemplateAsync(Layout("elsewhere", LayoutSurface.Light));
        var (agent, deck, _) = await DeckWithLayoutAsync(Layout("approved-one", LayoutSurface.Dark), Layout("draft-one", LayoutSurface.Light, approved: false));
        await using var _a = agent;
        var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        live.EnsureSuccessStatusCode();
        var code = (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        Factory.Services.GetRequiredService<SettablePresentations>().Set(SettablePresentations.At(deck, slide: 1));
        using var anonymous = ClientFor(TestUser.Anonymous);

        var guest = await anonymous.GetFromJsonAsync<JsonElement>($"/api/join/{code}");

        var custom = guest.GetProperty("look").GetProperty("customLayouts").EnumerateArray().ToList();
        custom.Select(l => l.GetProperty("name").GetString()).ShouldBe(["approved-one"]);
        custom[0].GetProperty("surface").GetString().ShouldBe("dark");
        custom[0].GetProperty("tree").GetProperty("scope").GetString().ShouldNotBeNullOrEmpty();
        custom[0].GetProperty("css").GetString().ShouldNotBeNullOrEmpty();
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
        (await Client.PostAsync($"/api/templates/{id}/archive", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

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
    private async Task<(McpTestClient Agent, string Deck, string Template)> DeckWithLayoutAsync(params LayoutSpec[] layouts)
    {
        var id = await PublishNewTemplateAsync(layouts);
        var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return (agent, await DeckOnAsync(agent, id), id);
    }

    // A new template of Sam's (a draft through POST /api/templates), published as version 1 with these layouts.
    private async Task<string> PublishNewTemplateAsync(params LayoutSpec[] layouts)
    {
        var created = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        await PublishVersionAsync(id, layouts);
        return id;
    }

    // Makes the draft's customLayouts exactly these (each uploaded through the upload routes, listed through the draft
    // PUT, approved through the approve route) and publishes it as the next version. A layout asked for unapproved is
    // approved to get past the publish gate, then has its approval withdrawn in the database: no route does that.
    private async Task PublishVersionAsync(string id, params LayoutSpec[] layouts)
    {
        var entries = new JsonArray();
        var assets = new List<(LayoutSpec Spec, string Asset)>();
        foreach (var spec in layouts)
        {
            var asset = await UploadAsync(id, spec);
            assets.Add((spec, asset));
            entries.Add(new JsonObject { ["name"] = spec.Name, ["file"] = asset });
        }
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}");
        var manifest = JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!;
        manifest["customLayouts"] = entries;
        // A custom name that the manifest's built-in layouts list also names.
        foreach (var listed in layouts.Where(l => l.AlsoListed))
        {
            manifest["layouts"]!.AsArray().Add(listed.Name);
        }
        using var put = await Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision = editor.GetProperty("revision").GetInt32() });
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        foreach (var (spec, asset) in assets)
        {
            using var approve = await Client.PostAsJsonAsync($"/api/templates/{id}/layouts/{spec.Name}/approve", new { asset, overflow = Array.Empty<string>(), contrast = Array.Empty<string>() });
            approve.StatusCode.ShouldBe(HttpStatusCode.NoContent, await approve.Content.ReadAsStringAsync());
        }
        using var publish = await Client.PostAsync($"/api/templates/{id}/publish", null);
        publish.StatusCode.ShouldBe(HttpStatusCode.OK, await publish.Content.ReadAsStringAsync());
        foreach (var (_, asset) in assets.Where(a => !a.Spec.Approved))
        {
            await WithdrawApprovalAsync(id, asset);
        }
    }

    // One layout document through the upload routes as Sam, the compiler set to return the layout the spec describes,
    // compiled for the scope of the bytes sent (the server's scope is the start of their hash). Returns the asset id.
    private async Task<string> UploadAsync(string template, LayoutSpec spec)
    {
        var body = Encoding.UTF8.GetBytes($$$"""{"markup":"<div>{{{spec.Name}}}</div>","css":".a{}","slots":{"name":"{{{spec.Name}}}","nonce":"{{{Guid.NewGuid():N}}}"}}""");
        Compiler.Accept(Compiled(spec, Convert.ToHexStringLower(SHA256.HashData(body))[..8]));
        Compiler.DefinitionFrom(DefinitionOf);
        var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "layout", fileName = "layout.json", rightsConfirmed = false });
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(body);
        using var put = await http.PutAsync(url, content);
        var stored = await put.Content.ReadFromJsonAsync<JsonElement>();
        put.StatusCode.ShouldBe(HttpStatusCode.OK, stored.ToString());
        return stored.GetProperty("assetId").GetString()!;
    }

    // Database write: a published version's layout whose approval is withdrawn. The routes only ever approve.
    private async Task WithdrawApprovalAsync(string template, string asset)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TemplateAssets.SingleAsync(a => a.TemplateId == template && a.Id == asset);
        var meta = JsonNode.Parse(row.MetaJson!)!;
        meta["review"]!["approved"] = false;
        row.MetaJson = meta.ToJsonString();
        await db.SaveChangesAsync();
    }

    // A deck of Sam's on the template, made through create_deck, which pins the latest published version there is now.
    private async Task<string> DeckOnAsync(McpTestClient agent, string templateId)
    {
        var (isError, json) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", template = templateId });
        isError.ShouldBeFalse(json.ToString());
        return json.GetProperty("deck").GetString()!;
    }

    private async Task<string?> StoredSurfaceAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Slides.AsNoTracking().SingleAsync(s => s.DeckId == deck && s.DeletedAt == null)).Surface;
    }

    private static LayoutSpec Layout(string name, LayoutSurface surface, bool approved = true, int itemMax = 6, bool alsoListed = false) => new(name, surface, approved, itemMax, alsoListed);

    // The definition the stand-in compiler gives a stored layout, built from the schema as stored: a headline, a list of
    // items that reveal one by one, and the reveal switch.
    private static LayoutDefinition DefinitionOf(CustomLayoutSchema schema)
        => new(
            schema.Name,
            schema.Description,
            schema.Surface switch { "dark" => LayoutSurface.Dark, "either" => LayoutSurface.Either, _ => LayoutSurface.Light },
            [
                SlotKit.Req("headline", new TextSlot((int)schema.Slots.Single(s => s.Name == "headline").Max!.Value)),
                SlotKit.Req("items", new ListSlot(new TextSlot(40), 1, (int)schema.Slots.Single(s => s.Name == "items").Max!.Value)),
                SlotKit.Opt("reveal", SlotKit.Reveal()),
            ],
            steps: StepPlan.List("items"));

    // A compiled layout for the spec, its classes scoped as the server scopes them.
    private static CompiledLayout Compiled(LayoutSpec spec, string scope)
    {
        var name = spec.Name;
        var definition = DefinitionOf(new CustomLayoutSchema(name, "A headline over a list of items.", SurfaceName(spec.Surface), "items", [new CustomSlot("headline", "text", false, null, 20, [], [], null), new CustomSlot("items", "list", false, 1, spec.ItemMax, [], [], null)]));
        var tree = new LayoutTree(LayoutTree.VERSION, scope, [new TreeNode("div", [$"cl-{scope}-{name}__root"], null, null, null, null, [], [], [])]);
        var surfaceName = SurfaceName(spec.Surface);
        var slots = """{"name":"NAME","description":"A headline over a list of items.","surface":"SURFACE","steps":"items","slots":{"headline":{"type":"text","max":20},"items":{"type":"list","min":1,"max":MAX,"of":{"type":"text","max":40}}}}"""
            .Replace("NAME", name, StringComparison.Ordinal).Replace("SURFACE", surfaceName, StringComparison.Ordinal).Replace("MAX", spec.ItemMax.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return new CompiledLayout(CompiledLayout.COMPILER_VERSION, name, "A headline over a list of items.", surfaceName, new CustomSteps(CustomSteps.LIST, "items", 0), definition, tree, $".cl-{scope}-{name}__root {{ color: var(--sw-ink); }} /* root */", slots, "{}", "{}");
    }

    private static string SurfaceName(LayoutSurface surface) => surface switch { LayoutSurface.Dark => "dark", LayoutSurface.Either => "either", _ => "light" };
}
