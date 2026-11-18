using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// What the real ITemplates does with uploaded templates (T4): rows from TemplateRowSeed, answered through the host's own
// service, with the people of the tests as the callers. The routes of publishing are used where a deck or a publish is
// the point. Sam owns every template; Alex is the person it is shared with or not.
[TestFixture]
public class TemplatesDatabaseTests : ApiTestFixture
{
    private readonly List<AsyncServiceScope> _scopes = [];

    [OneTimeTearDown]
    public async Task DisposeScopes()
    {
        foreach (var scope in _scopes)
        {
            await scope.DisposeAsync();
        }
    }

    [Test]
    public async Task Find_WhenTheViewerHasNoAccess_ShouldStillResolveForTheDeck()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Private one"));
        var alex = await ActorAsync(TestUser.Alex);

        var found = await TemplatesFor().FindAsync(id, null, CancellationToken.None);
        var forDeck = await TemplatesFor().ForDeckAsync(DeckOn(id, 1), CancellationToken.None);
        var listed = await TemplatesFor().ListForAsync(alex, CancellationToken.None);

        found.ShouldNotBeNull();
        found.Ref.ShouldBe(id);
        forDeck.Ref.ShouldBe(id);
        forDeck.BuiltIn.ShouldBeFalse();
        listed.Select(t => t.Ref).ShouldNotContain(id);
    }

    [Test]
    public async Task ListFor_WhenSharedWithMe_ShouldIncludeIt()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Named one"), access: DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);

        var listed = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Alex), CancellationToken.None);
        var mira = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Mira), CancellationToken.None);

        var uploaded = listed.Single(t => t.Ref == id);
        uploaded.OwnerName.ShouldBe(TestUser.Sam.Name);
        uploaded.LatestVersion.ShouldBe(1);
        uploaded.BuiltIn.ShouldBeFalse();
        mira.Select(t => t.Ref).ShouldNotContain(id);
    }

    [Test]
    public async Task ListFor_WhenSetBackToPrivate_ShouldLeaveItOut()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Back to private"), access: DeckSharing.Private);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);

        var forAlex = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Alex), CancellationToken.None);
        var forSam = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Sam), CancellationToken.None);

        forAlex.Select(t => t.Ref).ShouldNotContain(id);
        forSam.Select(t => t.Ref).ShouldContain(id);
    }

    [Test]
    public async Task ListFor_WhenLinkAndNoViewerRow_ShouldLeaveItOut()
    {
        var withoutRow = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Link, no row"), access: DeckSharing.Link);
        var withRow = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Link, used"), access: DeckSharing.Link);
        await TemplateRowSeed.ViewerAsync(Factory, withRow, TestUser.Alex.Email);

        var listed = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Alex), CancellationToken.None);

        listed.Select(t => t.Ref).ShouldNotContain(withoutRow);
        listed.Select(t => t.Ref).ShouldContain(withRow);
    }

    [Test]
    public async Task ListFor_WhenArchived_ShouldLeaveItOut()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("To archive"));
        var before = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Sam), CancellationToken.None);

        (await Client.PostAsync($"/api/templates/{id}/archive", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = await TemplatesFor().ListForAsync(await ActorAsync(TestUser.Sam), CancellationToken.None);

        before.Select(t => t.Ref).ShouldContain(id);
        after.Select(t => t.Ref).ShouldNotContain(id);
    }

    [Test]
    public async Task ForDeck_WhenTheTemplateIsArchived_ShouldStillResolveItsVersion()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Archived but used"));
        await Client.PostAsync($"/api/templates/{id}/archive", null);

        var forDeck = await TemplatesFor().ForDeckAsync(DeckOn(id, 1), CancellationToken.None);

        forDeck.Ref.ShouldBe(id);
        forDeck.Version.ShouldBe(1);
        forDeck.Archived.ShouldBeTrue();
    }

    [Test]
    public async Task ForDeck_WhenPinnedToVersion1_ShouldIgnoreVersion2()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("Version one"));
        await AddVersionAsync(id, 2, await ManifestAsync("Version two"), null);

        var pinned = await TemplatesFor().ForDeckAsync(DeckOn(id, 1), CancellationToken.None);
        var unpinned = await TemplatesFor().ForDeckAsync(DeckOn(id, null), CancellationToken.None);

        pinned.Version.ShouldBe(1);
        pinned.Template.Label.ShouldBe("Version one");
        unpinned.Version.ShouldBe(2);
    }

    [Test]
    public async Task ForDeck_WhenTheRefIsGone_ShouldFallBackToTheDefault()
    {
        var found = await TemplatesFor().ForDeckAsync(DeckOn("tpl_gonegonegone", 3), CancellationToken.None);

        found.BuiltIn.ShouldBeTrue();
        found.Template.IsDefault.ShouldBeTrue();
    }

    [Test]
    public async Task Find_WhenOnlyADraftExists_ShouldBeNull()
    {
        var id = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, await ManifestAsync("Draft only"));

        var latest = await TemplatesFor().FindAsync(id, null, CancellationToken.None);
        var draft = await TemplatesFor().FindAsync(id, 0, CancellationToken.None);
        var forDeck = await TemplatesFor().ForDeckAsync(DeckOn(id, null), CancellationToken.None);

        latest.ShouldBeNull();
        draft.ShouldBeNull();
        forDeck.BuiltIn.ShouldBeTrue();
    }

    [Test]
    public async Task Find_WhenTheManifestHasAPalette_ShouldCarryItAsTemplate()
    {
        var manifest = JsonNode.Parse(await ManifestAsync("With palette"))!;
        manifest["palette"] = SamplePalette();
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, manifest.ToJsonString());

        var found = await TemplatesFor().FindAsync(id, null, CancellationToken.None);

        found.ShouldNotBeNull();
        found.Template.Palette.ShouldBe("template");
        found.OwnPalette.ShouldNotBeNull();
        found.OwnPalette.Name.ShouldBe("template");
        found.OwnPalette.Tokens["accent"].ShouldBe(SamplePalette()["accent"]!.GetValue<string>());
        foreach (var extra in SamplePalette()["extras"]?.AsArray() ?? [])
        {
            var name = extra!["name"]!.GetValue<string>();
            found.OwnPalette.Tokens[name].ShouldBe(extra["colour"]!.GetValue<string>());
            found.OwnPalette.Tokens[$"{name}-ink"].ShouldBe(extra["ink"]!.GetValue<string>());
        }
    }

    [Test]
    public async Task Find_WhenTheManifestNamesFonts_ShouldCarryTheirAssetIds()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("With fonts"));
        var head = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "head-700.woff2", """{"avgCharEm": 0.62}""");
        var body = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "body-400.woff2", """{"avgCharEm": 0.58}""");
        var manifest = JsonNode.Parse(await ManifestAsync("With fonts"))!;
        manifest["fonts"] = new JsonArray(Font("head", head, 700), Font("body", body, 400));
        await SetVersionAsync(id, 1, manifest.ToJsonString(), """{"body":0.58,"head":0.62,"cell":0.58}""");

        var found = await TemplatesFor().FindAsync(id, null, CancellationToken.None);

        found.ShouldNotBeNull();
        found.Look.Fonts.Select(f => (f.Role, f.Asset, f.Weight)).ShouldBe([("head", head, 700), ("body", body, 400)]);
        found.Look.Fonts[0].Family.ShouldBe("Northwind Sans");
        found.FitWidths.Head.ShouldBe(0.62);
        found.FitWidths.Body.ShouldBe(0.58);
        found.FitWidths.Cell.ShouldBe(0.58);
    }

    [Test]
    public async Task Find_WhenVersion1IsAsked_ShouldCarryLatestVersion2()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync("One"));
        await AddVersionAsync(id, 2, await ManifestAsync("Two"), null);

        var first = await TemplatesFor().FindAsync(id, 1, CancellationToken.None);
        var latest = await TemplatesFor().FindAsync(id, null, CancellationToken.None);
        var missing = await TemplatesFor().FindAsync(id, 3, CancellationToken.None);

        first.ShouldNotBeNull();
        first.Version.ShouldBe(1);
        first.LatestVersion.ShouldBe(2);
        latest.ShouldNotBeNull();
        latest.Version.ShouldBe(2);
        latest.FitWidths.ShouldBe(SlideWorks.Core.Slides.FitWidths.Default);
        missing.ShouldBeNull();
    }

    [Test]
    public async Task FindLayout_WhenTheTemplateDoesNotAllowIt_ShouldAnswerNull()
    {
        var manifest = JsonNode.Parse(await ManifestAsync("Few layouts"))!;
        manifest["layouts"] = new JsonArray("title", "bullets");
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, manifest.ToJsonString());

        var found = await TemplatesFor().FindAsync(id, null, CancellationToken.None);

        found.ShouldNotBeNull();
        found.FindLayout("title").ShouldNotBeNull();
        found.FindLayout("poll").ShouldNotBeNull();
        found.FindLayout("quote").ShouldBeNull();
    }

    // SW-407 amendment: the real seam, where the SW-407 test used a fake.
    [Test]
    public async Task Archive_WhenDecksUseIt_ShouldKeepThemRenderingOnTheRealSeam()
    {
        var id = await CreateAsync();
        (await PublishAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutDeckOnTemplateAsync(deck, id, 1);

        var archive = await Client.PostAsync($"/api/templates/{id}/archive", null);
        var play = await Client.GetAsync($"/api/play/{deck}");

        archive.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        play.StatusCode.ShouldBe(HttpStatusCode.OK);
        var look = (await play.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("look");
        look.GetProperty("templateRef").GetString().ShouldBe(id);
        look.GetProperty("templateMissing").GetBoolean().ShouldBeFalse();
    }

    // SW-407 amendment: pinning tests for publishing.
    [Test]
    public async Task Publish_WhenTheStoredDraftNoLongerParses_ShouldRefuseAsManifestInvalid()
    {
        var id = await CreateAsync();
        await SetVersionAsync(id, 0, "{ not json", null);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("manifest_invalid");
        (await VersionsAsync(id)).Select(v => v.Version).ShouldBe([0]);
    }

    [TestCase("font")]
    [TestCase("logo")]
    public async Task Publish_WhenTheAssetBelongsToAnotherTemplate_ShouldRefuse(string kind)
    {
        var id = await CreateAsync();
        var other = await CreateAsync();
        var manifest = await DraftAsync(id);
        string path;
        if (kind == "font")
        {
            var foreign = await TemplateRowSeed.AssetAsync(Factory, other, TemplateAssetKind.Font, "foreign.woff2", """{"avgCharEm": 0.58}""", rightsConfirmed: true);
            manifest["fonts"] = new JsonArray(Font("head", foreign, 700));
            path = "fonts[0].file";
        }
        else
        {
            var foreign = await TemplateRowSeed.AssetAsync(Factory, other, TemplateAssetKind.Logo, "foreign.svg");
            manifest["logos"]!["light"] = foreign;
            path = "logos.light";
        }
        await SetVersionAsync(id, 0, manifest.ToJsonString(), null);

        var response = await PublishAsync(id);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe("asset_not_found");
        body.GetProperty("path").GetString().ShouldBe(path);
        (await VersionsAsync(id)).Select(v => v.Version).ShouldBe([0]);
    }

    [Test]
    public async Task Publish_WhenAnotherPersonHasDecksOnTheTemplate_ShouldCountOnlyMine()
    {
        var id = await CreateAsync();
        (await PublishAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var mine = await SharingApi.CreateDeckAsync(Client);
        using var alexClient = ClientFor(TestUser.Alex);
        var theirs = await SharingApi.CreateDeckAsync(alexClient);
        await PutDeckOnTemplateAsync(mine, id, 1);
        await PutDeckOnTemplateAsync(theirs, id, 1);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetRawText().ShouldBe("""{"version":2,"decksBehind":1}""");
    }

    private ITemplates TemplatesFor()
    {
        var scope = Factory.Services.CreateAsyncScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<ITemplates>();
    }

    private async Task<ICurrentActor> ActorAsync(TestUser person)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        return new Actor(user.Id, user.Email);
    }

    private static Deck DeckOn(string template, int? version)
        => new()
        {
            Id = "deckdeckdeck",
            Slug = "a-deck",
            Title = "A deck",
            PartsJson = "[]",
            Template = template,
            TemplateVersion = version,
            Palette = "cobalt",
        };

    private Task<HttpResponseMessage> PublishAsync(string id) => Client.PostAsync($"/api/templates/{id}/publish", null);

    private async Task<string> CreateAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    // A valid manifest the editor leaves for a new template, under a label of the test's own.
    private async Task<string> ManifestAsync(string label)
    {
        var manifest = await DraftAsync(await CreateAsync());
        manifest["label"] = label;
        return manifest.ToJsonString();
    }

    private async Task<JsonNode> DraftAsync(string id)
    {
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}");
        return JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!;
    }

    private static JsonObject Font(string role, string file, int weight)
        => new() { ["role"] = role, ["family"] = "Northwind Sans", ["file"] = file, ["weight"] = weight, ["style"] = "normal" };

    private static JsonObject SamplePalette()
    {
        var contracts = Path.GetDirectoryName(JsonShape.ScreensDirectory())!;
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(contracts, "template-manifest.sample.json")))!;
        return JsonNode.Parse(sample["palette"]!.ToJsonString())!.AsObject();
    }

    private async Task AddVersionAsync(string id, int version, string manifestJson, string? fitWidthsJson)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId = id,
            Version = version,
            ManifestJson = manifestJson,
            FitWidthsJson = fitWidthsJson,
            PublishedAt = Clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    private async Task SetVersionAsync(string id, int version, string manifestJson, string? fitWidthsJson)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TemplateVersions.SingleAsync(v => v.TemplateId == id && v.Version == version);
        row.ManifestJson = manifestJson;
        row.FitWidthsJson = fitWidthsJson;
        await db.SaveChangesAsync();
    }

    // The one place a test points a deck at a template: the public routes only make decks on built-ins.
    private async Task PutDeckOnTemplateAsync(string deckId, string templateId, int version)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deck = await db.Decks.SingleAsync(d => d.Id == deckId);
        deck.Template = templateId;
        deck.TemplateVersion = version;
        await db.SaveChangesAsync();
    }

    private async Task<List<TemplateVersion>> VersionsAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == id).OrderBy(v => v.Version).ToListAsync();
    }

    private sealed record Actor(Guid? PresenterId, string? Email) : ICurrentActor
    {
        public bool IsSignedIn => PresenterId != null;

        public string? Name => null;

        public ActorKind Kind => ActorKind.Person;

        public ActorVia Via => ActorVia.Web;

        public UserAccess? Access => UserAccess.Approved;

        public bool IsAdmin => false;
    }
}
