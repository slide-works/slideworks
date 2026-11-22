using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The change log's first line for a deck names its template the way a person does: an uploaded template by its label,
// never by its tpl_ id, and a built-in by its name as before.
[TestFixture]
public class TemplateDeckSummaryTests : ApiTestFixture
{
    [Test]
    public async Task Create_WhenTheTemplateIsUploaded_ShouldNameItByItsLabelInTheActivity()
    {
        using var created = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Journey Studio" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var template = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{template}");
        var manifest = editor.GetProperty("manifest");
        var label = manifest.GetProperty("label").GetString()!;
        var published = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, manifest.GetRawText());

        using var deck = await Client.PostAsJsonAsync("/api/decks", new { title = "On mine", template = published });

        deck.StatusCode.ShouldBe(HttpStatusCode.Created, await deck.Content.ReadAsStringAsync());
        var id = (await deck.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var activity = await DeckSeed.ActivityAsync(Client, id);
        label.ShouldNotBeNullOrWhiteSpace();
        activity.Single().GetProperty("summary").GetString().ShouldBe($"created the deck from the {label} template with the Cobalt palette");
        activity.Single().GetProperty("summary").GetString()!.ShouldNotContain("tpl_");
    }

    [Test]
    public async Task Create_WhenTheTemplateIsBuiltIn_ShouldKeepItsNameInTheActivity()
    {
        using var deck = await Client.PostAsJsonAsync("/api/decks", new { title = "On talk", template = "talk" });

        var id = (await deck.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var activity = await DeckSeed.ActivityAsync(Client, id);
        activity.Single().GetProperty("summary").GetString().ShouldBe("created the deck from the talk template with the Cobalt palette");
    }
}
