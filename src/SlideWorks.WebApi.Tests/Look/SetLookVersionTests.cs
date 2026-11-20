using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// D7: a deck stays on the version it was made with and moves with set_look, forwards or back. The app, and so the fake, is
// shared: each test adds a ref of its own. A deck is made on its template through the route, which pins the latest version
// there is at that moment, and the later versions are added after.
[TestFixture]
public class SetLookVersionTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    private FakeTemplates Templates => Factory.Services.GetRequiredService<FakeTemplates>();

    [Test]
    public async Task SetLook_WhenTheVersionIsNewer_ShouldMoveTheDeckAndLogIt()
    {
        var deck = await DeckOnVersionOneAsync("tpl_vernewer0001");
        var before = await PageAsync(deck);

        var result = await PutOkAsync(deck, new { templateVersion = 3 });

        result.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(3);
        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64() + 1);
        var summaries = await SummariesAsync(deck);
        summaries[0].ShouldBe("Look: Northwind version 1 to 3");
        summaries.Count.ShouldBe(2);
    }

    [Test]
    public async Task SetLook_WhenTheVersionIsLatest_ShouldPinTheHighest()
    {
        var deck = await DeckOnVersionOneAsync("tpl_verlatest001");

        var result = await PutOkAsync(deck, new { templateVersion = "latest" });

        result.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(3);
        (await SummariesAsync(deck))[0].ShouldBe("Look: Northwind version 1 to 3");
    }

    // The client's version offer sends exactly this body and nothing else.
    [Test]
    public async Task SetLook_WhenTheBodyIsOnlyLatest_ShouldMoveTheDeck()
    {
        var deck = await DeckOnVersionOneAsync("tpl_verexactbody1");
        using var body = new StringContent("""{"templateVersion":"latest"}""", System.Text.Encoding.UTF8, "application/json");

        using var response = await Client.PutAsync($"/api/decks/{deck}/look", body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await RowAsync(deck)).TemplateVersion.ShouldBe(3);
        (await SummariesAsync(deck)).Count.ShouldBe(2);
    }

    [Test]
    public async Task SetLook_WhenTheVersionIsOlder_ShouldMoveBack()
    {
        var deck = await DeckOnVersionOneAsync("tpl_verolder0001");
        await PutOkAsync(deck, new { templateVersion = 3 });

        var result = await PutOkAsync(deck, new { templateVersion = 2 });

        result.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(2);
        (await SummariesAsync(deck))[0].ShouldBe("Look: Northwind version 3 to 2");
    }

    [TestCase("""{"templateVersion":9}""", "templateVersion: Northwind has no version 9")]
    [TestCase("""{"templateVersion":0}""", "templateVersion: use a version number or 'latest'")]
    [TestCase("""{"templateVersion":1.5}""", "templateVersion: use a version number or 'latest'")]
    [TestCase("""{"templateVersion":"newest"}""", "templateVersion: use a version number or 'latest'")]
    [TestCase("""{"templateVersion":null}""", "templateVersion: use a version number or 'latest'")]
    [TestCase("""{"templateVersion":true}""", "templateVersion: use a version number or 'latest'")]
    public async Task SetLook_WhenTheVersionDoesNotExist_ShouldChangeNothing(string body, string error)
    {
        var deck = await DeckOnVersionOneAsync($"tpl_ver{Guid.NewGuid():N}"[..16]);
        var before = await PageAsync(deck);

        using var json = JsonDocument.Parse(body);
        (await PutErrorAsync(deck, json.RootElement)).ShouldBe(error);

        await ShouldBeUnchangedAsync(deck, before);
    }

    [Test]
    public async Task SetLook_WhenTheTemplateIsNotSharedWithMe_ShouldChangeNothing()
    {
        // Moving to a template of someone else's, and to a newer version of one the caller no longer may pick, are both
        // refused as an unknown template, before anything is written.
        Templates.Add(Uploaded("tpl_vernotmine001", 1), TestUser.Alex.Email);
        var deck = await DeckOnVersionOneAsync("tpl_vernotshared1", pickableBy: TestUser.Sam.Email);
        Templates.Add(Uploaded("tpl_vernotshared1", 2), TestUser.Alex.Email);
        var before = await PageAsync(deck);

        (await PutErrorAsync(deck, new { template = "tpl_vernotmine001" })).ShouldBe("Unknown template 'tpl_vernotmine001'");
        (await PutErrorAsync(deck, new { template = "tpl_vernotmine001", templateVersion = 1 })).ShouldBe("Unknown template 'tpl_vernotmine001'");
        (await PutErrorAsync(deck, new { templateVersion = 2 })).ShouldBe("Unknown template 'tpl_vernotshared1'");
        (await PutErrorAsync(deck, new { templateVersion = "latest" })).ShouldBe("Unknown template 'tpl_vernotshared1'");

        await ShouldBeUnchangedAsync(deck, before);
    }

    [Test]
    public async Task SetLook_WhenTheTemplateIsBuiltIn_ShouldRefuseAVersion()
    {
        var deck = await CreateDeckAsync();
        Templates.Add(Uploaded("tpl_verbuiltin001", 1));
        var before = await PageAsync(deck);

        (await PutErrorAsync(deck, new { templateVersion = 1 })).ShouldBe("templateVersion: built-in templates have no versions");
        (await PutErrorAsync(deck, new { templateVersion = "latest" })).ShouldBe("templateVersion: built-in templates have no versions");
        (await PutErrorAsync(deck, new { template = "workshop", templateVersion = 1 })).ShouldBe("templateVersion: built-in templates have no versions");

        await ShouldBeUnchangedAsync(deck, before);
    }

    [Test]
    public async Task SetLook_WhenAlreadyOnThatVersion_ShouldLogNothing()
    {
        var deck = await DeckOnVersionOneAsync("tpl_veralready001");
        var before = await PageAsync(deck);

        var result = await PutOkAsync(deck, new { templateVersion = 1 });

        result.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(1);
        await ShouldBeUnchangedAsync(deck, before);
    }

    [Test]
    public async Task SetLook_WhenCalledOnTheWire_ShouldReturnAnObject()
    {
        var deck = await DeckOnVersionOneAsync("tpl_verwire000001");
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var set = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_look", $$"""{"deck":"{{deck}}","template_version":"latest"}""");

        (set.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(set.GetRawText());
        var content = set.GetProperty("structuredContent");
        content.ValueKind.ShouldBe(JsonValueKind.Object);
        content.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(3);
        content.GetProperty("look").GetProperty("updateAvailable").GetBoolean().ShouldBeFalse();
        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("Look: Northwind version 1 to 3");
        entry.GetProperty("tool").GetString().ShouldBe("set_look");
    }

    [Test]
    public async Task SetLook_WhenSwitchedToAnUploadedTemplate_ShouldPinItsLatest()
    {
        Templates.Add(Uploaded("tpl_verswitch0001", 1));
        Templates.Add(Uploaded("tpl_verswitch0001", 2));
        var deck = await CreateDeckAsync();

        var result = await PutOkAsync(deck, new { template = "tpl_verswitch0001" });

        result.GetProperty("look").GetProperty("template").GetProperty("name").GetString().ShouldBe("tpl_verswitch0001");
        (await RowAsync(deck)).TemplateVersion.ShouldBe(2);
    }

    [Test]
    public async Task SetLook_WhenSwitchedToAnUploadedTemplateAtAVersion_ShouldPinThatOne()
    {
        Templates.Add(Uploaded("tpl_veratversion1", 1));
        Templates.Add(Uploaded("tpl_veratversion1", 2));
        var deck = await CreateDeckAsync();

        await PutOkAsync(deck, new { template = "tpl_veratversion1", templateVersion = 1 });

        (await RowAsync(deck)).TemplateVersion.ShouldBe(1);
    }

    [Test]
    public async Task SetLook_WhenSwitchedToABuiltIn_ShouldClearTheVersion()
    {
        var deck = await DeckOnVersionOneAsync("tpl_verclear00001");

        await PutOkAsync(deck, new { template = "workshop" });

        var row = await RowAsync(deck);
        row.Template.ShouldBe("workshop");
        row.TemplateVersion.ShouldBeNull();
    }

    // Everything the refusal must leave alone: the revision, the activity and the version.
    private async Task ShouldBeUnchangedAsync(string deck, JsonElement before)
    {
        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("activity").GetArrayLength().ShouldBe(before.GetProperty("activity").GetArrayLength());
        after.GetProperty("look").GetProperty("templateVersion").GetRawText().ShouldBe(before.GetProperty("look").GetProperty("templateVersion").GetRawText());
    }

    // The deck is made on version 1, which is the latest at that moment; versions 2 and 3 are added after.
    private async Task<string> DeckOnVersionOneAsync(string templateRef, string? pickableBy = null)
    {
        string[] pickers = pickableBy is null ? [] : [pickableBy];
        Templates.Add(Uploaded(templateRef, 1), pickers);
        var deck = await CreateDeckAsync(templateRef);
        (await RowAsync(deck)).TemplateVersion.ShouldBe(1);
        if (pickableBy is null)
        {
            Templates.Add(Uploaded(templateRef, 2));
            Templates.Add(Uploaded(templateRef, 3));
        }
        return deck;
    }

    private async Task<string> CreateDeckAsync(string? template = null)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Versions", template });
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

    private async Task<Deck> RowAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.AsNoTracking().SingleAsync(d => d.Id == deck);
    }

    // An uploaded template: the default built-in's structure under a tpl_ ref, labelled Northwind.
    private static ResolvedTemplate Uploaded(string templateRef, int version)
        => new(new TemplateCatalogue().Default with { Name = templateRef, Label = "Northwind", IsDefault = false }, templateRef, version, BuiltIn: false)
        {
            LatestVersion = version,
        };
}

// The same move through the real database seam: a template published with two versions, seeded as rows.
[TestFixture]
public class SetLookVersionDatabaseTests : ApiTestFixture
{
    [Test]
    public async Task SetLook_WhenTheVersionIsLatestOnTheDatabase_ShouldMoveTheDeck()
    {
        var manifest = await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "..", "template-manifest.sample.json"));
        var template = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, manifest);
        var created = await Client.PostAsJsonAsync("/api/decks", new { title = "Seeded", template });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var deck = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TemplateVersions.Add(new Features.Templates.Data.TemplateVersion
            {
                TemplateId = template,
                Version = 2,
                ManifestJson = manifest,
                PublishedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { templateVersion = "latest" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(2);
        var activity = await DeckSeed.ActivityAsync(Client, deck);
        activity[0].GetProperty("summary").GetString().ShouldBe("Look: Northwind kickoff version 1 to 2");
        activity.Count.ShouldBe(2);
    }
}

// An archived template leaves the pick list, but its owner's decks keep moving between its versions (D7).
[TestFixture]
public class SetLookArchivedTemplateTests : ApiTestFixture
{
    private async Task<string> CreateDeckAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Versions" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<string> PutErrorAsync<T>(string deck, T body)
        => await ErrorOfAsync(await Client.PutAsJsonAsync($"/api/decks/{deck}/look", body));

    private async Task<Deck> RowAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.AsNoTracking().SingleAsync(d => d.Id == deck);
    }

    private async Task<(string Template, string Manifest)> ArchivedWithThreeVersionsAsync(DeckSharing access = DeckSharing.Private)
    {
        var manifest = await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "..", "template-manifest.sample.json"));
        var template = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, manifest, access: access);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        foreach (var version in new[] { 2, 3 })
        {
            db.TemplateVersions.Add(new Features.Templates.Data.TemplateVersion { TemplateId = template, Version = version, ManifestJson = manifest, PublishedAt = now });
        }
        await db.SaveChangesAsync();
        await db.Templates.Where(t => t.Id == template).ExecuteUpdateAsync(s => s.SetProperty(t => t.ArchivedAt, now));
        return (template, manifest);
    }

    private async Task<string> DeckOnAsync(HttpClient owner, string template, int? version)
    {
        var created = await owner.PostAsJsonAsync("/api/decks", new { title = "Archived" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var deck = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Decks.Where(d => d.Id == deck).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Template, template).SetProperty(d => d.TemplateVersion, version))).ShouldBe(1);
        return deck;
    }

    private static async Task<string> ErrorOfAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;
    }

    // The template's owner keeps moving a deck between versions after archiving (D7): the update offer and the undo.
    [Test]
    public async Task SetLook_WhenTheOwnerOfAnArchivedTemplateNamesLatest_ShouldMoveTheDeckAndLogIt()
    {
        var (template, _) = await ArchivedWithThreeVersionsAsync();
        var deck = await DeckOnAsync(Client, template, 1);
        using var body = new StringContent("""{"templateVersion":"latest"}""", System.Text.Encoding.UTF8, "application/json");

        using var response = await Client.PutAsync($"/api/decks/{deck}/look", body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await RowAsync(deck)).TemplateVersion.ShouldBe(3);
        var summaries = (await DeckSeed.ActivityAsync(Client, deck)).Select(e => e.GetProperty("summary").GetString()!).ToList();
        summaries[0].ShouldStartWith("Look: Northwind kickoff version 1 to 3");
        summaries.Count.ShouldBe(2);

        // The undo.
        var undo = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { templateVersion = 2 });
        undo.StatusCode.ShouldBe(HttpStatusCode.OK, await undo.Content.ReadAsStringAsync());
        (await RowAsync(deck)).TemplateVersion.ShouldBe(2);
    }

    [Test]
    public async Task SetLook_WhenTheTemplateIsArchivedAndSharedWithMe_ShouldRefuseAsUnknown()
    {
        var (template, _) = await ArchivedWithThreeVersionsAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, template, TestUser.Alex.Email);
        using var alex = ClientFor(TestUser.Alex);
        var deck = await DeckOnAsync(alex, template, 1);
        var before = await alex.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        var error = await ErrorOfAsync(await alex.PutAsync($"/api/decks/{deck}/look",
            new StringContent("""{"templateVersion":"latest"}""", System.Text.Encoding.UTF8, "application/json")));

        error.ShouldBe($"Unknown template '{template}'");
        var after = await alex.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("activity").GetArrayLength().ShouldBe(before.GetProperty("activity").GetArrayLength());
        (await RowAsync(deck)).TemplateVersion.ShouldBe(1);
    }

    [Test]
    public async Task SetLook_WhenTheOwnerSwitchesADeckToTheirArchivedTemplate_ShouldRefuseAsUnknown()
    {
        var (template, _) = await ArchivedWithThreeVersionsAsync();
        var deck = await CreateDeckAsync();

        (await PutErrorAsync(deck, new { template })).ShouldBe($"Unknown template '{template}'");
        (await PutErrorAsync(deck, new { template, templateVersion = 2 })).ShouldBe($"Unknown template '{template}'");

        var row = await RowAsync(deck);
        row.Template.ShouldNotBe(template);
        row.TemplateVersion.ShouldBeNull();
    }
}
