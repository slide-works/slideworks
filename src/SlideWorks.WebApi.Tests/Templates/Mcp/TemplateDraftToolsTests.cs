using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates.Mcp;

// create_template, update_template and get_template on the fake ITemplateDrafts: what each tool asks of the service and how
// it words the answer, over raw JSON-RPC. The same tools on the real service are in TemplateDraftToolsRealTests. A fake's
// refusal is for every call, so these tests do not run in parallel and each accepts calls again when it is done.
[TestFixture]
[NonParallelizable]
public class TemplateDraftToolsTests : ApiTestFixture
{
    private const string TEMPLATE = "tpl_Nw4kT8pQz2Lm";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplateDrafts.Register(services);
        FakeTemplatePublishing.Register(services);
        FakeTemplateSharing.Register(services);
    };

    private FakeTemplatePublishing Publishing => Factory.Services.GetRequiredService<FakeTemplatePublishing>();

    private FakeTemplateDrafts Drafts => Factory.Services.GetRequiredService<FakeTemplateDrafts>();

    // The calls the fake has answered since a test began: it is shared by the fixture, so earlier tests left theirs.
    private List<string> CallsSince(int before) => [.. Drafts.Calls().Skip(before)];

    [TearDown]
    public void AcceptCallsAgain()
    {
        Drafts.Refuse(null);
        Drafts.RefuseUpdate(null);
    }

    [Test]
    public async Task CreateTemplate_WhenCalledOnTheWire_ShouldReturnAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "create_template", """{"name":"Kickoff","from":"workshop"}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.ValueKind.ShouldBe(JsonValueKind.Object);
        JsonDocument.Parse(McpWire.TextOf(result)).RootElement.ToString().ShouldBe(content.ToString());
        content.GetProperty("template").GetString().ShouldBe(TEMPLATE);
        content.GetProperty("status").GetString().ShouldBe("draft");
        content.GetProperty("revision").GetInt32().ShouldBe(7);
        content.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/templates/{TEMPLATE}/edit");
        var checks = content.GetProperty("checks").EnumerateArray().ToList();
        checks.Count.ShouldBe(Drafts.Editor.Checklist.Count);
        checks.Select(c => c.EnumerateObject().Select(p => p.Name).ToList()).ShouldAllBe(names => names.SequenceEqual(new[] { "check", "ok", "detail" }));
        checks.Select(c => c.GetProperty("ok").GetBoolean()).ShouldBe(Drafts.Editor.Checklist.Select(row => row.State == Features.Templates.Views.TemplateCheckState.Pass));
        CallsSince(before).ShouldBe(["Create workshop Kickoff", $"Get {TEMPLATE}"]);
    }

    [Test]
    public async Task CreateTemplate_WhenGivenAManifest_ShouldMergeItOverTheCopy()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;

        var (isError, _) = await agent.CallToolAsync("create_template", new { name = "Kickoff", from = "workshop", manifest = new { purpose = "Only mine." } });

        isError.ShouldBeFalse();
        CallsSince(before).ShouldBe(["Create workshop Kickoff", $"Get {TEMPLATE}", $"Update {TEMPLATE} 7"]);
        var sent = JsonDocument.Parse(Drafts.LastManifestJson!).RootElement;
        sent.GetProperty("purpose").GetString().ShouldBe("Only mine.");
        sent.GetProperty("label").GetString().ShouldBe(Drafts.Editor.Manifest.GetProperty("label").GetString());
        JsonNode.DeepEquals(JsonNode.Parse(sent.GetProperty("layouts").GetRawText()), JsonNode.Parse(Drafts.Editor.Manifest.GetProperty("layouts").GetRawText())).ShouldBeTrue();
    }

    [Test]
    public async Task CreateTemplate_WhenTheManifestIsRefused_ShouldArchiveTheDraftItMade()
    {
        Drafts.RefuseUpdate(TemplateRefusal.Of(TemplateErrorCodes.TOO_MANY_EXTRAS, "palette.extras"));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;
        var archivedBefore = Publishing.Calls().Count;

        var (isError, message) = await agent.CallToolAsync("create_template", new { name = "Kickoff", from = "workshop", manifest = new { palette = new { } } });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"palette.extras: {TemplateRefusal.Of(TemplateErrorCodes.TOO_MANY_EXTRAS).Message}");
        CallsSince(before).ShouldBe(["Create workshop Kickoff", $"Get {TEMPLATE}", $"Update {TEMPLATE} 7"]);
        Publishing.Calls().Skip(archivedBefore).ToList().ShouldBe([$"Archive {TEMPLATE}"]);
    }

    [Test]
    public async Task UpdateTemplate_WhenCalledOnTheWire_ShouldReturnTheChecks()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "update_template", $$$"""{"template":"{{{TEMPLATE}}}","manifest":{"purpose":"New."},"revision":7}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.GetProperty("template").GetString().ShouldBe(TEMPLATE);
        content.GetProperty("revision").GetInt32().ShouldBe(7);
        content.GetProperty("checks").GetArrayLength().ShouldBe(Drafts.Editor.Checklist.Count);
        CallsSince(before).ShouldBe([$"Get {TEMPLATE}", $"Update {TEMPLATE} 7"]);
        JsonDocument.Parse(Drafts.LastManifestJson!).RootElement.GetProperty("purpose").GetString().ShouldBe("New.");
    }

    [Test]
    public async Task UpdateTemplate_WhenRevisionIsLeftOut_ShouldUseTheCurrentOne()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;

        var (isError, _) = await agent.CallToolAsync("update_template", new { template = TEMPLATE, manifest = new { purpose = "New." } });

        isError.ShouldBeFalse();
        CallsSince(before).ShouldContain($"Update {TEMPLATE} {Drafts.Editor.Revision}");
    }

    [Test]
    public async Task UpdateTemplate_WhenTheManifestIsRefused_ShouldNameTheField()
    {
        Drafts.Refuse(TemplateRefusal.Of(TemplateErrorCodes.TOO_MANY_EXTRAS, "palette.extras"));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "update_template", $$$$"""{"template":"{{{{TEMPLATE}}}}","manifest":{"palette":{}}}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(result).ShouldBe($"palette.extras: {TemplateRefusal.Of(TemplateErrorCodes.TOO_MANY_EXTRAS).Message}");
    }

    [Test]
    public async Task UpdateTemplate_WhenTheRevisionIsStale_ShouldChangeNothing()
    {
        Drafts.Refuse(TemplateRefusal.Of(TemplateErrorCodes.STALE_REVISION));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;
        var lastManifest = Drafts.LastManifestJson;

        var (isError, message) = await agent.CallToolAsync("update_template", new { template = TEMPLATE, manifest = new { purpose = "New." }, revision = 3 });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe(TemplateRefusal.Of(TemplateErrorCodes.STALE_REVISION).Message);
        CallsSince(before).ShouldBeEmpty();
        Drafts.LastManifestJson.ShouldBe(lastManifest);
    }

    [Test]
    public async Task UpdateTemplate_WhenAKeyIsNull_ShouldRefuseItBeforeReadingOrWriting()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Drafts.Calls().Count;

        var (isError, message) = await agent.CallToolAsync("update_template", new { template = TEMPLATE, manifest = new { purpose = (string?)null } });

        isError.ShouldBeTrue();
        message.GetString().ShouldStartWith("purpose: The template manifest is not valid");
        CallsSince(before).ShouldBeEmpty();
    }

    [Test]
    public async Task GetTemplate_WhenCalledOnTheWire_ShouldReturnAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_template", $$$"""{"template":"{{{TEMPLATE}}}"}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.GetProperty("template").GetString().ShouldBe(TEMPLATE);
        content.GetProperty("name").GetString().ShouldBe(Drafts.Editor.Name);
        content.GetProperty("status").GetString().ShouldBe("published");
        content.GetProperty("revision").GetInt32().ShouldBe(7);
        content.GetProperty("version").GetInt32().ShouldBe(2);
        content.GetProperty("manifest").ValueKind.ShouldBe(JsonValueKind.Object);
        content.GetProperty("files").GetArrayLength().ShouldBe(Drafts.Editor.Assets.Count);
        var file = content.GetProperty("files")[0];
        file.GetProperty("asset").GetString().ShouldBe(Drafts.Editor.Assets[0].AssetId);
        file.GetProperty("kind").GetString().ShouldBe("font");
        file.EnumerateObject().Select(p => p.Name).ShouldBe(["asset", "kind", "fileName", "bytes", "rightsConfirmed"]);
        content.GetProperty("checks").GetArrayLength().ShouldBe(Drafts.Editor.Checklist.Count);
        var row = Drafts.Templates.Mine.Single(r => r.Id == TEMPLATE);
        content.GetProperty("decks").GetInt32().ShouldBe(row.DeckCount);
        content.GetProperty("sharing").GetString().ShouldBe(row.Sharing.ToString().ToLowerInvariant());
        content.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/templates/{TEMPLATE}/edit");
    }

    [Test]
    public async Task GetTemplate_WhenNotTheOwner_ShouldSayNotFound()
    {
        Drafts.Refuse(new NotFoundException($"No template '{TEMPLATE}'."));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, message) = await agent.CallToolAsync("get_template", new { template = TEMPLATE });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"No template '{TEMPLATE}'.");
    }
}
