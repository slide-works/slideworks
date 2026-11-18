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
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// A person's template drafts over HTTP (T5), on the real service: started from a built-in, read as the editor view,
// updated and listed. Other tasks' services are the stubs, so a published template is a database row from TemplateRowSeed
// and the template seam is FakeTemplates.
[TestFixture]
public class TemplateDraftsTests : ApiTestFixture
{
    private const int FIFTY = 50;

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    [Test]
    public async Task Create_WhenStartedFromTalk_ShouldCopyItsFields()
    {
        var talk = new TemplateCatalogue().Find("talk").ShouldNotBeNull();

        var id = await CreateAsync("talk", "Quarterly review");
        var editor = await GetEditorAsync(id);

        editor.GetProperty("name").GetString().ShouldBe("Quarterly review");
        editor.GetProperty("revision").GetInt32().ShouldBe(1);
        editor.GetProperty("status").GetString().ShouldBe("draft");
        editor.GetProperty("version").GetInt32().ShouldBe(0);
        var manifest = editor.GetProperty("manifest");
        manifest.GetProperty("name").GetString().ShouldBe("quarterly-review");
        manifest.GetProperty("label").GetString().ShouldBe("Quarterly review");
        manifest.GetProperty("basedOn").GetString().ShouldBe("talk");
        manifest.GetProperty("purpose").GetString().ShouldBe(talk.Purpose);
        manifest.GetProperty("layouts").EnumerateArray().Select(l => l.GetString()).ShouldBe(talk.Layouts);
        manifest.GetProperty("outline").EnumerateArray().Select(l => l.GetString()).ShouldBe(talk.Outline);
        manifest.GetProperty("houseStyle").EnumerateArray().Select(l => l.GetString()).ShouldBe(talk.HouseStyle);
        manifest.GetProperty("type").GetProperty("head").GetString().ShouldBe(talk.Type.Head);
        manifest.GetProperty("type").GetProperty("headWeight").GetInt32().ShouldBe(talk.Type.HeadWeight);
        manifest.GetProperty("shape").GetProperty("radius").GetProperty("lg").GetInt32().ShouldBe(talk.Shape.Radius.Lg);
        manifest.GetProperty("chrome").GetProperty("tracker").GetBoolean().ShouldBe(talk.Chrome.Tracker);
        manifest.GetProperty("fonts").GetArrayLength().ShouldBe(0);
        manifest.GetProperty("customLayouts").GetArrayLength().ShouldBe(0);
        manifest.GetProperty("logos").TryGetProperty("light", out _).ShouldBeFalse();
        editor.GetProperty("assets").GetArrayLength().ShouldBe(0);
        (await RecordAsync(id)).Access.ShouldBe(Features.Decks.DeckSharing.Private);
    }

    [Test]
    public async Task Create_WhenTheBuiltInIsUnknown_ShouldChangeNothing()
    {
        var before = await CountAsync();

        var response = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "no-such-template", name = "Mine" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CountAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Create_WhenIHaveFiftyTemplates_ShouldChangeNothing()
    {
        for (var i = 0; i < FIFTY; i++)
        {
            await TemplateRowSeed.DraftAsync(Factory, TestUser.Ada, "{}");
        }
        var before = await CountAsync();
        using var ada = ClientFor(TestUser.Ada);

        var response = await ada.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "One too many" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("too_many_templates");
        (await CountAsync()).ShouldBe(before);
        (await OwnedByAsync(TestUser.Ada)).ShouldBe(FIFTY);
    }

    [Test]
    public async Task Update_WhenAFileIsNotAnAsset_ShouldChangeNothing()
    {
        var id = await CreateAsync("talk", "Mine");
        var before = await GetEditorAsync(id);
        var manifest = Manifest(before);
        manifest["logos"]!["light"] = "00000000000000000000000000000000";

        var response = await PutAsync(id, manifest, 1);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("asset_not_found");
        body.GetProperty("path").GetString().ShouldBe("logos.light");
        await ShouldBeUnchangedAsync(id, before);
    }

    [Test]
    public async Task Update_WhenAFileIsOfTheWrongKind_ShouldChangeNothing()
    {
        var id = await CreateAsync("talk", "Mine");
        var font = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "mark.woff2");
        var before = await GetEditorAsync(id);
        var manifest = Manifest(before);
        manifest["logos"]!["light"] = font;

        var response = await PutAsync(id, manifest, 1);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("path").GetString().ShouldBe("logos.light");
        await ShouldBeUnchangedAsync(id, before);
    }

    [Test]
    public async Task Update_WhenSaved_ShouldAddOneToTheRevision()
    {
        var id = await CreateAsync("talk", "Mine");
        var logo = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Logo, "mark.svg");
        var manifest = Manifest(await GetEditorAsync(id));
        manifest["label"] = "Renamed";
        manifest["logos"]!["light"] = logo;

        var response = await PutAsync(id, manifest, 1);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, saved.ToString());
        saved.GetProperty("revision").GetInt32().ShouldBe(2);
        saved.GetProperty("name").GetString().ShouldBe("Renamed");
        saved.GetProperty("manifest").GetProperty("logos").GetProperty("light").GetString().ShouldBe(logo);
        saved.GetProperty("assets").EnumerateArray().Select(a => a.GetProperty("assetId").GetString()).ShouldBe([logo]);
        var record = await RecordAsync(id);
        record.Revision.ShouldBe(2);
        record.Name.ShouldBe("Renamed");
    }

    [Test]
    public async Task Update_WhenTheManifestHasAnUnknownField_ShouldChangeNothing()
    {
        var id = await CreateAsync("talk", "Mine");
        var before = await GetEditorAsync(id);
        var manifest = Manifest(before);
        manifest["colour"] = "red";

        var response = await PutAsync(id, manifest, 1);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("manifest_invalid");
        body.GetProperty("path").GetString().ShouldBe("colour");
        await ShouldBeUnchangedAsync(id, before);
    }

    [Test]
    public async Task Update_WhenTheRevisionIsStale_ShouldBe409()
    {
        var id = await CreateAsync("talk", "Mine");
        var before = await GetEditorAsync(id);

        var response = await PutAsync(id, Manifest(before), 7);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("stale_revision");
        await ShouldBeUnchangedAsync(id, before);
    }

    [Test]
    public async Task Get_WhenNotTheOwner_ShouldBeNotFound()
    {
        var id = await CreateAsync("talk", "Mine");
        using var alex = ClientFor(TestUser.Alex);

        using var get = await alex.GetAsync($"/api/templates/{id}");
        using var put = await alex.PutAsJsonAsync($"/api/templates/{id}", new { manifest = Manifest(await GetEditorAsync(id)), revision = 1 });

        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RecordAsync(id)).Revision.ShouldBe(1);
    }

    [Test]
    public async Task List_WhenIOwnADraft_ShouldPutItUnderMine()
    {
        var id = await CreateAsync("workshop", "Quarterly review");

        var list = await Client.GetFromJsonAsync<JsonElement>("/api/templates");

        var row = list.GetProperty("mine").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        row.GetProperty("name").GetString().ShouldBe("Quarterly review");
        row.GetProperty("shortName").GetString().ShouldBe("quarterly-review");
        row.GetProperty("type").GetString().ShouldBe("Plus Jakarta Sans / Inter");
        row.GetProperty("palette").GetString().ShouldBe("any");
        row.GetProperty("paletteLocked").GetBoolean().ShouldBeFalse();
        row.GetProperty("version").GetInt32().ShouldBe(0);
        row.GetProperty("status").GetString().ShouldBe("draft");
        row.GetProperty("deckCount").GetInt32().ShouldBe(0);
        row.GetProperty("sharing").GetString().ShouldBe("private");
        row.GetProperty("owner").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("purpose").ValueKind.ShouldBe(JsonValueKind.String);
        list.GetProperty("shared").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldNotContain(id);
        list.GetProperty("builtIn").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldContain("workshop");
    }

    [Test]
    public async Task List_WhenAStoredManifestDoesNotParse_ShouldSkipItAndListTheRest()
    {
        var good = await CreateAsync("workshop", "Fine one");
        var broken = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, "{ not a manifest");

        var response = await Client.GetAsync("/api/templates");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ids = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mine").EnumerateArray()
            .Select(r => r.GetProperty("id").GetString()).ToList();
        ids.ShouldContain(good);
        ids.ShouldNotContain(broken);
    }

    [Test]
    public async Task List_WhenOneIsSharedWithMe_ShouldPutItUnderShared()
    {
        var mine = await CreateAsync("talk", "Mine");
        var shared = await TemplateRowSeed.PublishedAsync(
            Factory, TestUser.Mira, ManifestText(await GetEditorAsync(mine)), version: 3, access: Features.Decks.DeckSharing.Link);
        var mira = await OwnerIdAsync(shared);
        Factory.Services.GetRequiredService<FakeTemplates>().Add(
            new ResolvedTemplate(new TemplateCatalogue().Default with { Name = "harbour", Label = "Harbour talks", IsDefault = false }, shared, 3, false)
            {
                LatestVersion = 3,
                OwnerId = mira,
                OwnerName = TestUser.Mira.Name,
            });
        var ownPublished = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, ManifestText(await GetEditorAsync(mine)));
        Factory.Services.GetRequiredService<FakeTemplates>().Add(
            new ResolvedTemplate(new TemplateCatalogue().Default with { Name = "own", Label = "Own", IsDefault = false }, ownPublished, 1, false)
            {
                LatestVersion = 1,
                OwnerId = await OwnerIdAsync(mine),
                OwnerName = TestUser.Sam.Name,
            });

        var list = await Client.GetFromJsonAsync<JsonElement>("/api/templates");

        var row = list.GetProperty("shared").EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("id").GetString().ShouldBe(shared);
        row.GetProperty("version").GetInt32().ShouldBe(3);
        row.GetProperty("status").GetString().ShouldBe("published");
        row.GetProperty("sharing").GetString().ShouldBe("link");
        row.GetProperty("owner").GetString().ShouldBe(TestUser.Mira.Name);
        list.GetProperty("mine").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldContain(ownPublished);
    }

    private async Task<string> CreateAsync(string basedOn, string name)
    {
        var response = await Client.PostAsJsonAsync("/api/templates", new { basedOn, name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> GetEditorAsync(string id)
    {
        var response = await Client.GetAsync($"/api/templates/{id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> PutAsync(string id, JsonNode manifest, int revision)
        => Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision });

    private static JsonNode Manifest(JsonElement editor) => JsonNode.Parse(ManifestText(editor))!;

    private static string ManifestText(JsonElement editor) => editor.GetProperty("manifest").GetRawText();

    // A refused change leaves the record, its revision and its draft row as they were read.
    private async Task ShouldBeUnchangedAsync(string id, JsonElement before)
    {
        var record = await RecordAsync(id);
        record.Revision.ShouldBe(before.GetProperty("revision").GetInt32());
        record.Name.ShouldBe(before.GetProperty("name").GetString());
        var after = await GetEditorAsync(id);
        JsonNode.DeepEquals(JsonNode.Parse(after.GetRawText()), JsonNode.Parse(before.GetRawText())).ShouldBeTrue();
    }

    private async Task<TemplateRecord> RecordAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Templates.AsNoTracking().SingleAsync(t => t.Id == id);
    }

    private async Task<Guid> OwnerIdAsync(string id) => (await RecordAsync(id)).OwnerId;

    private async Task<int> OwnedByAsync(TestUser person)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Templates.CountAsync(t => db.Users.Any(u => u.Id == t.OwnerId && u.Email == person.Email));
    }

    private async Task<int> CountAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Templates.CountAsync();
    }
}
