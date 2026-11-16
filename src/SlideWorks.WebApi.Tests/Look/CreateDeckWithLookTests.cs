using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L3, L6, L7, L8: POST /api/decks and create_deck take a template and palette through LookRules, and the deck page shows
// the look the deck got. The catalogue holds the built-ins plus LockingTemplateCatalogue's locking template.
[TestFixture]
public class CreateDeckWithLookTests : ApiTestFixture
{
    private readonly LockingTemplateCatalogue _templates = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<ITemplateCatalogue>(_templates);

    [Test]
    public async Task Post_WhenNoLookNamed_ShouldUseTheDefaultTemplateAndPalette()
    {
        var look = await LookOfAsync(await PostDeckAsync(new { title = "Defaults" }));

        look.GetProperty("template").GetProperty("name").GetString().ShouldBe("talk");
        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe("cobalt");
        look.GetProperty("draft").GetBoolean().ShouldBeFalse();
        look.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Post_WhenTemplateAndPaletteNamed_ShouldUseThemAndRecordThem()
    {
        var id = await PostDeckAsync(new { title = "Named", template = "update", palette = "ember" });

        var look = await LookOfAsync(id);
        look.GetProperty("template").GetProperty("name").GetString().ShouldBe("update");
        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe("ember");
        look.GetProperty("palette").GetProperty("tokens").GetProperty("accent").GetString()
            .ShouldBe(new PaletteCatalogue().Find("ember")!.Tokens["accent"]);
        var activity = await DeckSeed.ActivityAsync(Client, id);
        activity.Single().GetProperty("summary").GetString().ShouldBe("Created from the update template with the Ember palette");
    }

    [Test]
    public async Task Post_WhenWorkshop_ShouldStartAsADraft()
    {
        var look = await LookOfAsync(await PostDeckAsync(new { title = "Workshop", template = "workshop" }));

        look.GetProperty("draft").GetBoolean().ShouldBeTrue();
        look.GetProperty("template").GetProperty("chrome").GetProperty("deepDivesOnDivider").GetBoolean().ShouldBeTrue();
    }

    [TestCase("nope", null, "Unknown template 'nope'")]
    [TestCase(null, "nope", "Unknown palette 'nope'")]
    [TestCase(LockingTemplateCatalogue.LOCKING, "cobalt", "Template 'locked' locks its palette")]
    public async Task Post_WhenLookRefused_ShouldReturn400WithTheRuleText(string? template, string? palette, string error)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Refused", template, palette });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(error);
    }

    [Test]
    public async Task Post_WhenTemplateLocksItsPalette_ShouldForceIt()
    {
        var look = await LookOfAsync(await PostDeckAsync(new { title = "Locked", template = LockingTemplateCatalogue.LOCKING }));

        look.GetProperty("template").GetProperty("name").GetString().ShouldBe(LockingTemplateCatalogue.LOCKING);
        look.GetProperty("palette").GetProperty("name").GetString().ShouldBe(_templates.Palette);
    }

    [Test]
    public async Task CreateDeck_WhenTemplateNamed_ShouldReturnItsLookLayoutsOutlineAndHouseStyle()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var workshop = _templates.Find("workshop")!;

        var (isError, json) = await agent.CallToolAsync("create_deck", new { title = "Offsite", template = "workshop", palette = "plum" });

        isError.ShouldBeFalse(json.ToString());
        json.GetProperty("look").GetProperty("template").GetString().ShouldBe("workshop");
        json.GetProperty("look").GetProperty("palette").GetString().ShouldBe("plum");
        json.GetProperty("layouts").EnumerateArray().Select(l => l.GetString()).ShouldBe(workshop.Layouts);
        json.GetProperty("outline").EnumerateArray().Select(l => l.GetString()).ShouldBe(workshop.Outline);
        json.GetProperty("house_style").EnumerateArray().Select(l => l.GetString()).ShouldBe(workshop.HouseStyle);
        json.GetProperty("sharing").GetString().ShouldBe("private");
        var look = await LookOfAsync(json.GetProperty("deck").GetString()!);
        look.GetProperty("draft").GetBoolean().ShouldBeTrue();
    }

    [TestCase("nope", null, "Unknown template 'nope'")]
    [TestCase(null, "nope", "Unknown palette 'nope'")]
    [TestCase(LockingTemplateCatalogue.LOCKING, "cobalt", "Template 'locked' locks its palette")]
    public async Task CreateDeck_WhenLookRefused_ShouldReturnAToolErrorWithTheRuleText(string? template, string? palette, string error)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await agent.CallToolAsync("create_deck", new { title = "Refused", template, palette });

        isError.ShouldBeTrue(json.ToString());
        json.GetString().ShouldBe(error);
    }

    private async Task<string> PostDeckAsync(object body)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> LookOfAsync(string id)
        => (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}")).GetProperty("look");
}
