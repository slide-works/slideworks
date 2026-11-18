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

    // D81: archiving hides a template from every picker, its owner included.
    [Test]
    public async Task CreateDeck_WhenTheTemplateIsArchived_ShouldRefuseAsUnknown()
    {
        Templates.Add(Uploaded("tpl_archived0001", version: 1, latest: 1) with { Archived = true });
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = await DeckCountAsync(agent);

        var (isError, refused) = await agent.CallToolAsync("create_deck", new { title = "Archived", template = "tpl_archived0001" });
        var posted = await Client.PostAsJsonAsync("/api/decks", new { title = "Archived", template = "tpl_archived0001" });

        isError.ShouldBeTrue();
        refused.GetString().ShouldBe("Unknown template 'tpl_archived0001'");
        posted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await posted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Unknown template 'tpl_archived0001'");
        (await DeckCountAsync(agent)).ShouldBe(before);
    }

    // list_layouts follows create_deck's may-pick rule: a template the caller may not pick, or an archived one, answers
    // exactly as one that does not exist, apart from the name the caller sent.
    [Test]
    public async Task ListLayouts_WhenTheTemplateIsNotPickable_ShouldAnswerAsForAnUnknownTemplate()
    {
        Templates.Add(Uploaded("tpl_layoutsnot01", version: 1, latest: 1), TestUser.Alex.Email);
        Templates.Add(Uploaded("tpl_layoutsarc01", version: 1, latest: 1) with { Archived = true });
        Templates.Add(Uploaded("tpl_layoutsmine1", version: 1, latest: 1), TestUser.Sam.Email);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var notMine = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "list_layouts", """{"template":"tpl_layoutsnot01"}""");
        var archived = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "list_layouts", """{"template":"tpl_layoutsarc01"}""");
        var unknown = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "list_layouts", """{"template":"tpl_layoutsnone1"}""");
        var (mineIsError, mine) = await agent.CallToolAsync("list_layouts", new { template = "tpl_layoutsmine1" });

        unknown.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(unknown).ShouldContain("Unknown template 'tpl_layoutsnone1'");
        notMine.GetRawText().Replace("tpl_layoutsnot01", "tpl_layoutsnone1", StringComparison.Ordinal).ShouldBe(unknown.GetRawText());
        archived.GetRawText().Replace("tpl_layoutsarc01", "tpl_layoutsnone1", StringComparison.Ordinal).ShouldBe(unknown.GetRawText());
        mineIsError.ShouldBeFalse(mine.ToString());
        mine.GetProperty("layouts").GetArrayLength().ShouldBeGreaterThan(0);
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

    // The real service answers a pinned version with the template's latest published version; the fake answers what was
    // added last, so version 1 is added again once version 2 is out.
    [Test]
    public async Task GetDeck_WhenTheTemplateHasANewerPublishedVersion_ShouldSayAnUpdateIsAvailable()
    {
        Templates.Add(Uploaded("tpl_getbehind001", version: 1, latest: 1));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = (await CreateOnTheWireAsync(agent, """{"title":"Behind","template":"tpl_getbehind001"}""")).GetProperty("deck").GetString()!;
        var before = await GetLookOnTheWireAsync(agent, deck);
        Templates.Add(Uploaded("tpl_getbehind001", version: 2, latest: 2));
        Templates.Add(Uploaded("tpl_getbehind001", version: 1, latest: 2));

        var after = await GetLookOnTheWireAsync(agent, deck);

        before.GetProperty("templateVersion").GetInt32().ShouldBe(1);
        before.GetProperty("updateAvailable").GetBoolean().ShouldBeFalse();
        after.GetProperty("template").GetString().ShouldBe("tpl_getbehind001");
        after.GetProperty("templateVersion").GetInt32().ShouldBe(1);
        after.GetProperty("updateAvailable").GetBoolean().ShouldBeTrue();
        after.EnumerateObject().Select(p => p.Name).ShouldBe(["template", "palette", "logo", "draft", "chrome", "templateVersion", "updateAvailable"], ignoreOrder: true);
    }

    [Test]
    public async Task GetDeck_WhenBuiltIn_ShouldSayNoUpdateAndNoVersion()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = (await CreateOnTheWireAsync(agent, """{"title":"Built in","template":"workshop"}""")).GetProperty("deck").GetString()!;

        var look = await GetLookOnTheWireAsync(agent, deck);

        look.GetProperty("templateVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        look.GetProperty("updateAvailable").GetBoolean().ShouldBeFalse();
    }

    // set_look answers in get_deck's terms: a move pins the latest version, so nothing newer is waiting.
    [Test]
    public async Task SetLook_WhenMovedToAnUploadedTemplate_ShouldAnswerItsVersionAndNoUpdate()
    {
        Templates.Add(Uploaded("tpl_setanswer001", version: 1, latest: 2));
        Templates.Add(Uploaded("tpl_setanswer001", version: 2, latest: 2));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = (await CreateOnTheWireAsync(agent, """{"title":"Moved"}""")).GetProperty("deck").GetString()!;

        var set = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_look", $$"""{"deck":"{{deck}}","template":"tpl_setanswer001"}""");

        var look = set.GetProperty("structuredContent").GetProperty("look");
        look.GetProperty("templateVersion").GetInt32().ShouldBe(2);
        look.GetProperty("updateAvailable").GetBoolean().ShouldBeFalse();
        (await GetLookOnTheWireAsync(agent, deck)).GetRawText().ShouldBe(look.GetRawText());
    }

    // get_deck's look on the raw JSON-RPC wire.
    private async Task<JsonElement> GetLookOnTheWireAsync(McpTestClient agent, string deck)
        => (await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_deck", $$"""{"deck":"{{deck}}"}""")).GetProperty("structuredContent").GetProperty("look");

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
