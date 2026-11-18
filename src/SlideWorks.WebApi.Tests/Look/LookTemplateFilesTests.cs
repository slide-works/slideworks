using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// T4, T7: the look comes through the template seam, so an uploaded template's files, top bar, versions and own palette
// reach the play view, and a look change moves a deck only to a template its owner may pick. The app, and so the fake,
// is shared: each test adds a ref of its own. A deck is made on a built-in and moved with the look-change route.
[TestFixture]
public class LookTemplateFilesTests : ApiTestFixture
{
    private readonly RecordedWarnings _warnings = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplates.Register(services);
        services.AddSingleton<ILogger<DeckLooks>>(_warnings);
    };

    private FakeTemplates Templates => Factory.Services.GetRequiredService<FakeTemplates>();

    [Test]
    public async Task Looks_WhenAskedByAPerson_ShouldListWhatTheyMayPick()
    {
        Templates.Add(Uploaded("tpl_looksmine001", 3) with { OwnerName = "Mira Okafor", OwnerId = Guid.NewGuid() }, TestUser.Sam.Email);
        Templates.Add(Uploaded("tpl_looksalex001", 1), TestUser.Alex.Email);

        var looks = await Client.GetFromJsonAsync<JsonElement>("/api/looks");

        JsonShape.AssertMatches(looks, "looks.json", ScreenShapes.Looks);
        var rows = looks.GetProperty("templates").EnumerateArray().ToDictionary(t => t.GetProperty("ref").GetString()!);
        rows.Keys.ShouldContain("tpl_looksmine001");
        rows.Keys.ShouldNotContain("tpl_looksalex001");
        rows["tpl_looksmine001"].GetProperty("version").GetInt32().ShouldBe(3);
        rows["tpl_looksmine001"].GetProperty("builtIn").GetBoolean().ShouldBeFalse();
        rows["tpl_looksmine001"].GetProperty("owner").GetString().ShouldBe("Mira Okafor");
        foreach (var builtIn in new TemplateCatalogue().All)
        {
            rows[builtIn.Name].GetProperty("builtIn").GetBoolean().ShouldBeTrue();
            rows[builtIn.Name].GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);
            rows[builtIn.Name].GetProperty("owner").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Test]
    public async Task Play_WhenTheTemplateIsBuiltIn_ShouldSendEmptyFontsAndLayouts()
    {
        var deck = await CreateDeckAsync();

        var look = await PlayLookAsync(deck);

        look.GetProperty("fonts").GetArrayLength().ShouldBe(0);
        look.GetProperty("customLayouts").GetArrayLength().ShouldBe(0);
        look.GetProperty("extras").GetArrayLength().ShouldBe(0);
        look.GetProperty("ornament").GetProperty("colours").GetArrayLength().ShouldBe(0);
        look.GetProperty("logos").GetRawText().ShouldBe("""{"light":null,"dark":null,"showOnDark":false}""");
        look.GetProperty("art").GetRawText().ShouldBe("""{"kind":"none","generator":null,"url":null}""");
        look.GetProperty("topBar").GetString().ShouldBe("none");
        look.GetProperty("templateRef").GetString().ShouldBe("talk");
        look.GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("latestVersion").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Play_WhenTheTemplateIsBuiltIn_ShouldSayTheTemplateIsNotMissing()
    {
        var deck = await CreateDeckAsync();

        var look = await PlayLookAsync(deck);

        look.GetProperty("templateMissing").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Play_WhenTheTemplateHasFonts_ShouldSendTheirUrls()
    {
        var files = TemplateLook.None with
        {
            Fonts = [new TemplateFont("Northwind Sans", "head", "a1", 800, "normal"), new TemplateFont("Northwind Text", "body", "a2", 400, "italic")],
            LightLogo = "l1",
            DarkLogo = "l2",
            ShowLogoOnDark = true,
            ArtKind = TemplateLook.IMAGE_ART,
            ArtAsset = "art1",
            OrnamentColours = ["accent", "accent-2"],
            Extras = [new ExtraColour("extra-1", "#101820", "#ffffff")],
        };
        Templates.Add(Uploaded("tpl_fontsurls001", 1) with { Look = files });
        var deck = await CreateDeckAsync();
        await MoveAsync(deck, "tpl_fontsurls001");

        var look = await PlayLookAsync(deck);

        JsonShape.AssertMatches(look, Sample("template-public.json").GetProperty("look"), "template-public.json look", LookAlone);
        look.GetProperty("fonts").GetRawText().ShouldBe(
            """[{"family":"Northwind Sans","role":"head","url":"/api/templates/tpl_fontsurls001/assets/a1","weight":800,"style":"normal"},{"family":"Northwind Text","role":"body","url":"/api/templates/tpl_fontsurls001/assets/a2","weight":400,"style":"italic"}]""");
        look.GetProperty("logos").GetRawText().ShouldBe(
            """{"light":"/api/templates/tpl_fontsurls001/assets/l1","dark":"/api/templates/tpl_fontsurls001/assets/l2","showOnDark":true}""");
        look.GetProperty("art").GetRawText().ShouldBe("""{"kind":"image","generator":null,"url":"/api/templates/tpl_fontsurls001/assets/art1"}""");
        look.GetProperty("ornament").GetRawText().ShouldBe("""{"colours":["accent","accent-2"]}""");
        look.GetProperty("extras").GetRawText().ShouldBe("""[{"name":"extra-1","colour":"#101820","ink":"#ffffff"}]""");
        look.GetProperty("templateRef").GetString().ShouldBe("tpl_fontsurls001");
    }

    [Test]
    public async Task Play_WhenTheTemplateSetsTheTopBar_ShouldSayAccent()
    {
        Templates.Add(Uploaded("tpl_topbaracc001", 1) with { Look = TemplateLook.None with { TopBar = TemplateLook.ACCENT_TOP_BAR } });
        var deck = await CreateDeckAsync();
        await MoveAsync(deck, "tpl_topbaracc001");

        var look = await PlayLookAsync(deck);

        look.GetProperty("topBar").GetString().ShouldBe("accent");
    }

    // No route sets chrome.topBar yet, so the override is written on the deck's row.
    [Test]
    public async Task Play_WhenTheDeckOverridesTheTopBar_ShouldSayNone()
    {
        Templates.Add(Uploaded("tpl_topbarnone01", 1) with { Look = TemplateLook.None with { TopBar = TemplateLook.ACCENT_TOP_BAR } });
        var deck = await CreateDeckAsync();
        await MoveAsync(deck, "tpl_topbarnone01");
        await WriteDeckRowAsync(deck, d => d.ChromeJson = """{"tracker":null,"footer":null,"progress":null,"joinBar":false,"topBar":"none"}""");

        var look = await PlayLookAsync(deck);
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        look.GetProperty("topBar").GetString().ShouldBe("none");
        page.GetProperty("look").GetProperty("chromeOverrides").GetProperty("topBar").GetString().ShouldBe("none");
    }

    // The real service answers version 1 with the template's latest version; the fake answers what was added last.
    [Test]
    public async Task Play_WhenTheDeckIsOnVersion1Of2_ShouldCarryBothVersions()
    {
        Templates.Add(Uploaded("tpl_oneoftwo0001", 1));
        var deck = await CreateDeckAsync();
        await MoveAsync(deck, "tpl_oneoftwo0001");
        Templates.Add(Uploaded("tpl_oneoftwo0001", 2));
        Templates.Add(Uploaded("tpl_oneoftwo0001", 1) with { LatestVersion = 2 });

        var look = await PlayLookAsync(deck);

        look.GetProperty("templateVersion").GetInt32().ShouldBe(1);
        look.GetProperty("latestVersion").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task ChangeLook_WhenMovedToAnUploadedTemplate_ShouldPinItsVersion()
    {
        Templates.Add(Uploaded("tpl_movepins0001", 1) with { LatestVersion = 2 });
        Templates.Add(Uploaded("tpl_movepins0001", 2));
        var deck = await CreateDeckAsync();

        var changed = await MoveAsync(deck, "tpl_movepins0001");
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
        var back = await MoveAsync(deck, "workshop");

        changed.GetProperty("look").GetProperty("templateRef").GetString().ShouldBe("tpl_movepins0001");
        changed.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(2);
        page.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(2);
        back.GetProperty("look").GetProperty("templateRef").GetString().ShouldBe("workshop");
        back.GetProperty("look").GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        (await ReadDeckRowAsync(deck, d => d.TemplateVersion)).ShouldBeNull();
    }

    // Naming the template the deck is already on moves nothing: the pin stays where it is.
    [Test]
    public async Task ChangeLook_WhenTheDecksOwnTemplateIsNamedAgain_ShouldKeepItsPin()
    {
        Templates.Add(Uploaded("tpl_keepspin0001", 1));
        var deck = await CreateDeckAsync();
        await MoveAsync(deck, "tpl_keepspin0001");
        Templates.Add(Uploaded("tpl_keepspin0001", 2));

        var again = await MoveAsync(deck, "tpl_keepspin0001");

        again.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task ChangeLook_WhenTheTemplateIsNotPickable_ShouldRefuseAsUnknownAndChangeNothing()
    {
        Templates.Add(Uploaded("tpl_notmine00001", 1), TestUser.Alex.Email);
        Templates.Add(Uploaded("tpl_archived0002", 1) with { Archived = true });
        var deck = await CreateDeckAsync();
        var before = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        var notMine = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { template = "tpl_notmine00001", draft = false });
        var archived = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { template = "tpl_archived0002" });
        var unknown = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { template = "tpl_nosuchthing2" });

        notMine.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await notMine.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Unknown template 'tpl_notmine00001'");
        (await archived.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Unknown template 'tpl_archived0002'");
        (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Unknown template 'tpl_nosuchthing2'");
        var after = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("look").GetRawText().ShouldBe(before.GetProperty("look").GetRawText());
        after.GetProperty("activity").GetArrayLength().ShouldBe(before.GetProperty("activity").GetArrayLength());
    }

    // set_look on the raw JSON-RPC wire: the same move and the same refusal as the route.
    [Test]
    public async Task SetLook_WhenMovedOnTheWire_ShouldMoveOnlyToATemplateTheCallerMayPick()
    {
        Templates.Add(Uploaded("tpl_wiremove0001", 4));
        Templates.Add(Uploaded("tpl_wirenot00001", 1), TestUser.Alex.Email);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var moved = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_look", $$"""{"deck":"{{deck}}","template":"tpl_wiremove0001"}""");
        var refused = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_look", $$"""{"deck":"{{deck}}","template":"tpl_wirenot00001"}""");

        moved.GetProperty("structuredContent").GetProperty("look").GetProperty("template").GetString().ShouldBe("tpl_wiremove0001");
        refused.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(refused).ShouldContain("Unknown template 'tpl_wirenot00001'");
        (await ReadDeckRowAsync(deck, d => d.Template)).ShouldBe("tpl_wiremove0001");
        (await ReadDeckRowAsync(deck, d => d.TemplateVersion)).ShouldBe(4);
    }

    [Test]
    public async Task Play_WhenThePaletteIsTheTemplates_ShouldSendItsTokens()
    {
        var own = new PaletteCatalogue().Find("ember")! with { Name = "northwind-own", Label = "Northwind" };
        var template = Uploaded("tpl_ownpalette01", 1);
        Templates.Add(template with { Template = template.Template with { Palette = ResolvedCatalogues.OWN_PALETTE, LockPalette = true }, OwnPalette = own });
        var deck = await CreateDeckAsync();
        await MoveAsync(deck, "tpl_ownpalette01");

        var look = await PlayLookAsync(deck);
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
        var back = await MoveAsync(deck, "talk");

        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe("template");
        look.GetProperty("palette").GetProperty("label").GetString().ShouldBe("Northwind");
        look.GetProperty("palette").GetProperty("tokens").GetProperty("accent").GetString().ShouldBe(own.Tokens["accent"]);
        page.GetProperty("look").GetProperty("contrast").GetProperty("lowest").GetProperty("pair").GetString().ShouldBe("on-accent");
        // A deck that leaves the template cannot keep its palette: it takes the default one.
        back.GetProperty("look").GetProperty("palette").GetProperty("name").GetString().ShouldBe(new PaletteCatalogue().Default.Name);
    }

    // D3: a deck always renders. No route leaves a deck on a ref that does not resolve, so the row is written.
    [Test]
    public async Task Play_WhenTheDecksTemplateCannotBeResolved_ShouldRenderTheDefaultAndSaySo()
    {
        var deck = await CreateDeckAsync();
        await WriteDeckRowAsync(deck, d =>
        {
            d.Template = "tpl_gonemissing1";
            d.TemplateVersion = 3;
            d.Palette = ResolvedCatalogues.OWN_PALETTE;
        });

        var look = await PlayLookAsync(deck);
        var page = await Client.GetAsync($"/api/decks/{deck}");
        var draft = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { draft = true });

        look.GetProperty("templateMissing").GetBoolean().ShouldBeTrue();
        look.GetProperty("templateRef").GetString().ShouldBe("tpl_gonemissing1");
        look.GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("template").GetProperty("name").GetString().ShouldBe(new TemplateCatalogue().Default.Name);
        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe(new PaletteCatalogue().Default.Name);
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        _warnings.Messages.ShouldContain(m => m.Contains(deck, StringComparison.Ordinal) && m.Contains("tpl_gonemissing1", StringComparison.Ordinal));
        // A change that does not move the deck leaves the stored ref and palette alone.
        draft.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadDeckRowAsync(deck, d => d.Template)).ShouldBe("tpl_gonemissing1");
        (await ReadDeckRowAsync(deck, d => d.Palette)).ShouldBe(ResolvedCatalogues.OWN_PALETTE);
    }

    private async Task<string> CreateDeckAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Look files", template = "talk" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> MoveAsync(string deck, string template)
    {
        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { template });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> PlayLookAsync(string deck)
        => (await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}")).GetProperty("look");

    private async Task WriteDeckRowAsync(string deck, Action<Features.Decks.Deck> write)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        write(await db.Decks.SingleAsync(d => d.Id == deck));
        await db.SaveChangesAsync();
    }

    private async Task<T> ReadDeckRowAsync<T>(string deck, Func<Features.Decks.Deck, T> read)
    {
        using var scope = Factory.Services.CreateScope();
        return read(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.AsNoTracking().SingleAsync(d => d.Id == deck));
    }

    private static JsonElement Sample(string file)
        => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

    // A look on its own, outside a screen: the screens' nullable paths without the "look." prefix.
    private static JsonShapeOptions LookAlone => new()
    {
        NullablePaths = ["template.titleArt", "logo", .. ScreenShapes.LookNullablePaths.Select(path => path["look.".Length..])],
    };

    // An uploaded template: the default built-in's structure under a tpl_ ref, which is also its name (T4).
    private static ResolvedTemplate Uploaded(string templateRef, int version)
        => new(new TemplateCatalogue().Default with { Name = templateRef, Label = "Northwind", IsDefault = false }, templateRef, version, BuiltIn: false)
        {
            LatestVersion = version,
        };

    // The warnings DeckLooks logged, for the test of a missing template.
    private sealed class RecordedWarnings : ILogger<DeckLooks>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                _messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
