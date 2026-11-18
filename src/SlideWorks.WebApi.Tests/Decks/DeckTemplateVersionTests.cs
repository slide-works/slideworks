using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// The pin (D7, D61): a new deck stores its template's ref and the latest published version (null on a built-in), and
// only a template the caller may pick is found. The app, and so the fake, is shared: each test adds a ref of its own.
[TestFixture]
public class DeckTemplateVersionTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    private FakeTemplates Templates => Factory.Services.GetRequiredService<FakeTemplates>();

    [Test]
    public async Task CreateDeck_WhenOnAnUploadedTemplate_ShouldPinItsLatestVersion()
    {
        Templates.Add(Uploaded("tpl_pinlatest01", version: 1, latest: 2));
        Templates.Add(Uploaded("tpl_pinlatest01", version: 2, latest: 2));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var created = await CreateOnTheWireAsync(agent, """{"title":"Pinned","template":"tpl_pinlatest01"}""");

        var look = created.GetProperty("look");
        look.GetProperty("template").GetString().ShouldBe("tpl_pinlatest01");
        look.GetProperty("templateVersion").GetInt32().ShouldBe(2);
        look.EnumerateObject().Select(p => p.Name).ShouldBe(["template", "palette", "templateVersion"], ignoreOrder: true);
    }

    [Test]
    public async Task CreateDeck_WhenPostedOnAnUploadedTemplate_ShouldCreateTheDeck()
    {
        Templates.Add(Uploaded("tpl_pinposted01", version: 3, latest: 3));

        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Posted", template = "tpl_pinposted01" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateDeck_WhenTheTemplateIsNotPickable_ShouldRefuseAsUnknown()
    {
        Templates.Add(Uploaded("tpl_notpickable1", version: 1, latest: 1), TestUser.Alex.Email);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = await DeckCountAsync(agent);

        var (isError, refused) = await agent.CallToolAsync("create_deck", new { title = "Not mine", template = "tpl_notpickable1" });
        var (_, unknown) = await agent.CallToolAsync("create_deck", new { title = "Not there", template = "tpl_nosuchthing1" });
        var posted = await Client.PostAsJsonAsync("/api/decks", new { title = "Not mine", template = "tpl_notpickable1" });

        isError.ShouldBeTrue();
        refused.GetString().ShouldBe("Unknown template 'tpl_notpickable1'");
        unknown.GetString().ShouldBe("Unknown template 'tpl_nosuchthing1'");
        posted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await posted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Unknown template 'tpl_notpickable1'");
        (await DeckCountAsync(agent)).ShouldBe(before);
    }

    [Test]
    public async Task CreateDeck_WhenBuiltIn_ShouldLeaveTheVersionNull()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var named = await CreateOnTheWireAsync(agent, """{"title":"Built in","template":"workshop"}""");
        var byDefault = await CreateOnTheWireAsync(agent, """{"title":"Default"}""");

        named.GetProperty("look").GetProperty("template").GetString().ShouldBe("workshop");
        named.GetProperty("look").GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        byDefault.GetProperty("look").GetProperty("template").GetString().ShouldBe(new TemplateCatalogue().Default.Name);
        byDefault.GetProperty("look").GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // create_deck on the raw JSON-RPC wire: the structured result as an agent's client reads it.
    private async Task<JsonElement> CreateOnTheWireAsync(McpTestClient agent, string argumentsJson)
        => (await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "create_deck", argumentsJson)).GetProperty("structuredContent");

    private static async Task<int> DeckCountAsync(McpTestClient agent)
        => (await agent.CallToolAsync("list_decks", new { })).Json.GetProperty("decks").GetArrayLength();

    // An uploaded template: the default built-in's structure under a tpl_ ref and a name of its own.
    private static ResolvedTemplate Uploaded(string templateRef, int version, int latest)
        => new(new TemplateCatalogue().Default with { Name = "northwind", Label = "Northwind", IsDefault = false }, templateRef, version, BuiltIn: false)
        {
            LatestVersion = latest,
        };
}
