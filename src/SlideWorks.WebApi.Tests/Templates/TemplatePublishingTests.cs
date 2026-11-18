using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Publishing, the checklist and archiving over HTTP (T5), on the real service: a template is made through the public
// route and its draft is then set straight in the database, so a check can be shown a manifest the editor would refuse.
// Files are rows from TemplateRowSeed. The template seam is FakeTemplates, which a deck's play view resolves through.
[TestFixture]
public class TemplatePublishingTests : ApiTestFixture
{
    private const string AVG_WIDE = """{"avgCharEm": 0.58}""";

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    [Test]
    public async Task Publish_WhenChecksPass_ShouldAddTheNextVersion()
    {
        var id = await DraftWithFontsAsync(rights: true);

        var first = await PublishAsync(id);
        var second = await PublishAsync(id);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<JsonElement>()).GetRawText().ShouldBe("""{"version":1,"decksBehind":0}""");
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32().ShouldBe(2);
        var versions = await VersionsAsync(id);
        versions.Select(v => v.Version).ShouldBe([0, 1, 2]);
        versions.Single(v => v.Version == 1).PublishedAt.ShouldBe(Clock.GetUtcNow());
        versions.Single(v => v.Version == 0).PublishedAt.ShouldBeNull();
    }

    [Test]
    public async Task Publish_WhenFontRightsAreNotConfirmed_ShouldChangeNothing()
    {
        var id = await DraftWithFontsAsync(rights: false);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "rights_not_confirmed", id);
    }

    [Test]
    public async Task Publish_WhenThePaletteFails_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var manifest = await DraftAsync(id);
        var palette = SamplePalette();
        palette["accent-2"] = "#a5f3fc";
        manifest["palette"] = palette;
        await SetDraftAsync(id, manifest);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "palette_contrast", id);
    }

    [Test]
    public async Task Publish_WhenAnExtraHasNoReadableInk_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var manifest = await DraftAsync(id);
        var palette = SamplePalette();
        palette["extras"] = new JsonArray(new JsonObject { ["name"] = "extra-1", ["colour"] = "#ffffff", ["ink"] = "#fefefe" });
        manifest["palette"] = palette;
        await SetDraftAsync(id, manifest);

        var response = await PublishAsync(id);

        var body = await ShouldBeRefusedAsync(response, "palette_contrast", id);
        body.GetProperty("path").GetString().ShouldBe("palette.extras[0].ink");
    }

    [Test]
    public async Task Publish_WhenNotTheOwner_ShouldBeNotFound()
    {
        var id = await CreateAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsync($"/api/templates/{id}/publish", null);
        var checklist = await alex.GetAsync($"/api/templates/{id}/checklist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        checklist.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await VersionsAsync(id)).Select(v => v.Version).ShouldBe([0]);
    }

    [Test]
    public async Task Edit_WhenPublished_ShouldLeaveTheVersionUnchanged()
    {
        var id = await CreateAsync();
        await PublishAsync(id);
        var published = (await VersionsAsync(id)).Single(v => v.Version == 1).ManifestJson;
        var manifest = await DraftAsync(id);
        manifest["label"] = "Renamed after publishing";

        var put = await Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision = 1 });

        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        var versions = await VersionsAsync(id);
        versions.Single(v => v.Version == 1).ManifestJson.ShouldBe(published);
        versions.Single(v => v.Version == 0).ManifestJson.ShouldContain("Renamed after publishing");
    }

    [Test]
    public async Task Archive_WhenDecksUseIt_ShouldKeepThemRendering()
    {
        var id = await CreateAsync();
        await PublishAsync(id);
        Factory.Services.GetRequiredService<FakeTemplates>().Add(Uploaded(id, version: 1));
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutDeckOnTemplateAsync(deck, id, 1);

        var archive = await Client.PostAsync($"/api/templates/{id}/archive", null);
        var play = await Client.GetAsync($"/api/play/{deck}");

        archive.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RecordAsync(id)).ArchivedAt.ShouldBe(Clock.GetUtcNow());
        play.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await play.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("look").GetProperty("templateRef").GetString().ShouldBe(id);
        (await VersionsAsync(id)).Select(v => v.Version).ShouldBe([0, 1]);
    }

    [Test]
    public async Task Archive_WhenNotTheOwner_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsync($"/api/templates/{id}/archive", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RecordAsync(id)).ArchivedAt.ShouldBeNull();
    }

    [Test]
    public async Task Publish_WhenTheTemplateIsArchived_ShouldStillAddAVersion()
    {
        var id = await CreateAsync();
        await Client.PostAsync($"/api/templates/{id}/archive", null);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await VersionsAsync(id)).Select(v => v.Version).ShouldBe([0, 1]);
        (await RecordAsync(id)).ArchivedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task Update_WhenTheTemplateIsArchived_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        await Client.PostAsync($"/api/templates/{id}/archive", null);
        var manifest = await DraftAsync(id);
        manifest["label"] = "Edited while archived";

        var put = await Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision = 1 });
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("template_archived");
        (await RecordAsync(id)).Revision.ShouldBe(1);
        (await VersionsAsync(id)).Single().ManifestJson.ShouldNotContain("Edited while archived");
    }

    [Test]
    public async Task Update_WhenTheAssetBelongsToAnotherTemplate_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var other = await CreateAsync();
        var foreign = await TemplateRowSeed.AssetAsync(Factory, other, TemplateAssetKind.Logo, "other-mark.svg");
        var manifest = await DraftAsync(id);
        manifest["logos"]!["light"] = foreign;

        var put = await Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision = 1 });
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("asset_not_found");
        body.GetProperty("path").GetString().ShouldBe("logos.light");
        (await RecordAsync(id)).Revision.ShouldBe(1);
        (await VersionsAsync(id)).Single().ManifestJson.ShouldNotContain(foreign);
    }

    [Test]
    public async Task Update_WhenOnlyAPublishedVersionExists_ShouldCreateTheDraftRow()
    {
        var baseline = await BaselineManifestAsync();
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, baseline.ToJsonString());
        var manifest = JsonNode.Parse(baseline.ToJsonString())!;
        manifest["label"] = "Second thoughts";

        var put = await Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision = 1 });

        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        var versions = await VersionsAsync(id);
        versions.Select(v => v.Version).ShouldBe([0, 1]);
        versions.Single(v => v.Version == 0).ManifestJson.ShouldContain("Second thoughts");
        versions.Single(v => v.Version == 1).ManifestJson.ShouldBe(baseline.ToJsonString());
    }

    [Test]
    public async Task Publish_WhenAFontIsNotAnAsset_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var manifest = await DraftAsync(id);
        manifest["fonts"] = new JsonArray(Font("head", "11111111111111111111111111111111", 700));
        await SetDraftAsync(id, manifest);

        var response = await PublishAsync(id);

        var body = await ShouldBeRefusedAsync(response, "asset_not_found", id);
        body.GetProperty("path").GetString().ShouldBe("fonts[0].file");
    }

    [Test]
    public async Task Publish_WhenOverTheFontLimit_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var fonts = new JsonArray();
        for (var i = 0; i < 9; i++)
        {
            var asset = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, $"face-{i}.woff2", rightsConfirmed: true);
            fonts.Add(Font("body", asset, 100 * (i + 1)));
        }
        var manifest = await DraftAsync(id);
        manifest["fonts"] = fonts;
        await SetDraftAsync(id, manifest);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "too_many_fonts", id);
    }

    [Test]
    public async Task Checklist_WhenAFontWeightIsMissing_ShouldNotBlock()
    {
        var id = await CreateAsync();
        var regular = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "head-400.woff2", rightsConfirmed: true);
        var manifest = await DraftAsync(id);
        manifest["fonts"] = new JsonArray(Font("head", regular, 400));
        await SetDraftAsync(id, manifest);

        var checklist = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}/checklist");

        checklist.GetProperty("checklist").EnumerateArray().Select(r => r.GetProperty("key").GetString())
            .ShouldBe(["palette", "fonts", "fontRights", "logoArt", "limits"]);
        checklist.GetProperty("checklist")[1].GetProperty("state").GetString().ShouldBe("todo");
        checklist.GetProperty("checklist")[1].GetProperty("tab").GetString().ShouldBe("fonts");
        checklist.GetProperty("canPublish").GetBoolean().ShouldBeTrue();
        (await PublishAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Publish_WhenAFontIsWide_ShouldStoreItsWidths()
    {
        var id = await CreateAsync();
        var head = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "wide-head-700.woff2", AVG_WIDE, rightsConfirmed: true);
        var manifest = await DraftAsync(id);
        manifest["fonts"] = new JsonArray(Font("head", head, 700));
        await SetDraftAsync(id, manifest);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await VersionsAsync(id)).Single(v => v.Version == 1).FitWidthsJson.ShouldBe("""{"body":0.56,"head":0.58,"cell":0.56}""");
    }

    [Test]
    public async Task Publish_WhenTwoOfMyDecksAreOnVersion1_ShouldSayTwoBehind()
    {
        var id = await CreateAsync();
        await PublishAsync(id);
        var templates = Factory.Services.GetRequiredService<FakeTemplates>();
        templates.Add(Uploaded(id, version: 1));
        var behind = new[] { await SharingApi.CreateDeckAsync(Client), await SharingApi.CreateDeckAsync(Client) };
        var current = await SharingApi.CreateDeckAsync(Client);
        foreach (var deck in behind)
        {
            await PutDeckOnTemplateAsync(deck, id, 1);
        }
        await PutDeckOnTemplateAsync(current, id, 2);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetRawText().ShouldBe("""{"version":2,"decksBehind":2}""");
    }

    private Task<HttpResponseMessage> PublishAsync(string id) => Client.PostAsync($"/api/templates/{id}/publish", null);

    private async Task<string> CreateAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    // A template with a head font in two weights, the way the editor leaves one that is ready to publish.
    private async Task<string> DraftWithFontsAsync(bool rights)
    {
        var id = await CreateAsync();
        var regular = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "head-400.woff2", rightsConfirmed: rights);
        var bold = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "head-700.woff2", rightsConfirmed: rights);
        var manifest = await DraftAsync(id);
        manifest["fonts"] = new JsonArray(Font("head", regular, 400), Font("head", bold, 700));
        await SetDraftAsync(id, manifest);
        return id;
    }

    private static JsonObject Font(string role, string file, int weight)
        => new() { ["role"] = role, ["family"] = "Northwind Sans", ["file"] = file, ["weight"] = weight, ["style"] = "normal" };

    private async Task<JsonNode> BaselineManifestAsync() => await DraftAsync(await CreateAsync());

    private async Task<JsonNode> DraftAsync(string id)
    {
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}");
        return JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!;
    }

    // The palette of the contract sample, which passes every pair, as the flat object a manifest holds.
    private static JsonObject SamplePalette()
    {
        var contracts = Path.GetDirectoryName(JsonShape.ScreensDirectory())!;
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(contracts, "template-manifest.sample.json")))!;
        return JsonNode.Parse(sample["palette"]!.ToJsonString())!.AsObject();
    }

    private static ResolvedTemplate Uploaded(string templateRef, int version)
        => new(new TemplateCatalogue().Default with { Name = "northwind", Label = "Northwind", IsDefault = false }, templateRef, version, BuiltIn: false)
        {
            LatestVersion = version,
        };

    private async Task SetDraftAsync(string id, JsonNode manifest)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var draft = await db.TemplateVersions.SingleAsync(v => v.TemplateId == id && v.Version == TemplateVersion.DRAFT);
        draft.ManifestJson = manifest.ToJsonString();
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

    // A refused publish answers 400 with the code, and leaves the draft as the only version.
    private async Task<JsonElement> ShouldBeRefusedAsync(HttpResponseMessage response, string code, string id)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe(code);
        (await VersionsAsync(id)).Select(v => v.Version).ShouldBe([0]);
        return body;
    }

    private async Task<List<TemplateVersion>> VersionsAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == id).OrderBy(v => v.Version).ToListAsync();
    }

    private async Task<TemplateRecord> RecordAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Templates.AsNoTracking().SingleAsync(t => t.Id == id);
    }
}
