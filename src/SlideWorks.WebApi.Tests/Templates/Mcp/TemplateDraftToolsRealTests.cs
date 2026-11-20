using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates.Mcp;

// create_template, update_template and get_template on the app's real ITemplateDrafts: the tools are proven against
// production code, not only the fake. Sam owns the templates; Alex is a person who may not see them, and what he is told
// is exactly what an unknown id gets.
[TestFixture]
public class TemplateDraftToolsRealTests : ApiTestFixture
{
    [Test]
    public async Task CreateTemplate_WhenCalledOnTheRealService_ShouldMakeADraftGetTemplateReadsBack()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var created = await CreateAsync(agent, "Kickoff");
        var (isError, read) = await agent.CallToolAsync("get_template", new { template = created.GetProperty("template").GetString() });

        isError.ShouldBeFalse(read.ToString());
        created.GetProperty("template").GetString()!.ShouldStartWith("tpl_");
        created.GetProperty("status").GetString().ShouldBe("draft");
        created.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/templates/{created.GetProperty("template").GetString()}/edit");
        read.GetProperty("name").GetString().ShouldBe("Kickoff");
        read.GetProperty("status").GetString().ShouldBe("draft");
        read.GetProperty("revision").GetInt32().ShouldBe(created.GetProperty("revision").GetInt32());
        read.GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("sharing").GetString().ShouldBe("private");
        read.GetProperty("decks").GetInt32().ShouldBe(0);
        read.GetProperty("files").GetArrayLength().ShouldBe(0);
        read.GetProperty("checks").ToString().ShouldBe(created.GetProperty("checks").ToString());
        read.GetProperty("manifest").GetProperty("basedOn").GetString().ShouldBe("workshop");
    }

    [Test]
    public async Task CreateTemplate_WhenFromIsUnknown_ShouldSayWhichBuiltInsExist()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, message) = await agent.CallToolAsync("create_template", new { name = "Kickoff", from = "nonesuch" });

        isError.ShouldBeTrue();
        message.GetString().ShouldStartWith("No built-in template named 'nonesuch'. The built-in templates are: ");
        message.GetString()!.ShouldContain("workshop");
    }

    [Test]
    public async Task CreateTemplate_WhenGivenAManifest_ShouldMergeItOverTheCopy()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var plain = await CreateAsync(agent, "Plain");

        var created = await CreateAsync(agent, "Merged", """{"purpose":"Only for kickoffs."}""");
        var (_, read) = await agent.CallToolAsync("get_template", new { template = created.GetProperty("template").GetString() });
        var (_, copy) = await agent.CallToolAsync("get_template", new { template = plain.GetProperty("template").GetString() });

        created.GetProperty("revision").GetInt32().ShouldBe(plain.GetProperty("revision").GetInt32() + 1);
        read.GetProperty("manifest").GetProperty("purpose").GetString().ShouldBe("Only for kickoffs.");
        read.GetProperty("manifest").GetProperty("layouts").ToString().ShouldBe(copy.GetProperty("manifest").GetProperty("layouts").ToString());
        read.GetProperty("manifest").GetProperty("label").GetString().ShouldBe("Merged");
    }

    [Test]
    public async Task UpdateTemplate_WhenCalledOnTheWire_ShouldChangeTheDraftAndReturnTheChecks()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = (await CreateAsync(agent, "Kickoff")).GetProperty("template").GetString()!;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "update_template", $$$"""{"template":"{{{id}}}","manifest":{"purpose":"Changed."}}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.GetProperty("template").GetString().ShouldBe(id);
        content.GetProperty("revision").GetInt32().ShouldBeGreaterThan(1);
        content.GetProperty("checks").EnumerateArray().First().GetProperty("check").GetString().ShouldBe("palette");
        var (_, read) = await agent.CallToolAsync("get_template", new { template = id });
        read.GetProperty("manifest").GetProperty("purpose").GetString().ShouldBe("Changed.");
        read.GetProperty("revision").GetInt32().ShouldBe(content.GetProperty("revision").GetInt32());
    }

    [Test]
    public async Task UpdateTemplate_WhenTheRevisionIsStale_ShouldChangeNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = (await CreateAsync(agent, "Kickoff")).GetProperty("template").GetString()!;
        var (_, before) = await agent.CallToolAsync("get_template", new { template = id });

        var (isError, message) = await agent.CallToolAsync("update_template", new { template = id, manifest = new { purpose = "Lost." }, revision = 99 });
        var (_, after) = await agent.CallToolAsync("get_template", new { template = id });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe(TemplateRefusal.Of(TemplateErrorCodes.STALE_REVISION).Message);
        after.ToString().ShouldBe(before.ToString());
    }

    [Test]
    public async Task UpdateTemplate_WhenTheManifestIsRefused_ShouldNameTheFieldAndChangeNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = (await CreateAsync(agent, "Kickoff")).GetProperty("template").GetString()!;
        var (_, before) = await agent.CallToolAsync("get_template", new { template = id });

        var (isError, message) = await agent.CallToolAsync("update_template", new { template = id, manifest = new { nonesuch = 1 } });
        var (_, after) = await agent.CallToolAsync("get_template", new { template = id });

        isError.ShouldBeTrue();
        message.GetString().ShouldStartWith("nonesuch: The template manifest is not valid: ");
        after.ToString().ShouldBe(before.ToString());
    }

    [Test]
    public async Task UpdateTemplate_WhenAFileFieldNamesNoAsset_ShouldRefuseWithTheAssetNotFoundText()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = (await CreateAsync(agent, "Kickoff")).GetProperty("template").GetString()!;

        var (isError, message) = await agent.CallToolAsync("update_template", new { template = id, manifest = new { logos = new { light = "0123456789abcdef0123456789abcdef", showOnDark = false } } });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"logos.light: {TemplateRefusal.Of(TemplateErrorCodes.ASSET_NOT_FOUND, "logos.light").Message}");
    }

    [Test]
    public async Task GetTemplate_WhenCalledOnTheWire_ShouldReturnAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = (await CreateAsync(agent, "Kickoff")).GetProperty("template").GetString()!;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_template", $$"""{"template":"{{id}}"}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.ValueKind.ShouldBe(JsonValueKind.Object);
        content.EnumerateObject().Select(p => p.Name).ShouldBe(["template", "name", "status", "revision", "version", "manifest", "files", "checks", "sharing", "decks", "url"]);
    }

    [Test]
    public async Task GetTemplate_WhenNotTheOwner_ShouldSayNotFoundAsForAnUnknownId()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var id = (await CreateAsync(sam, "Kickoff")).GetProperty("template").GetString()!;

        var (foreignError, foreign) = await alex.CallToolAsync("get_template", new { template = id });
        var (unknownError, unknown) = await alex.CallToolAsync("get_template", new { template = "tpl_doesnotexist" });

        foreignError.ShouldBeTrue();
        unknownError.ShouldBeTrue();
        foreign.GetString().ShouldBe($"No template '{id}'.");
        unknown.GetString().ShouldBe("No template 'tpl_doesnotexist'.");
    }

    [Test]
    public async Task UpdateTemplate_WhenNotTheOwner_ShouldSayNotFoundAndChangeNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var id = (await CreateAsync(sam, "Kickoff")).GetProperty("template").GetString()!;
        var (_, before) = await sam.CallToolAsync("get_template", new { template = id });

        var (isError, message) = await alex.CallToolAsync("update_template", new { template = id, manifest = new { purpose = "Taken." } });
        var (_, after) = await sam.CallToolAsync("get_template", new { template = id });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"No template '{id}'.");
        after.ToString().ShouldBe(before.ToString());
    }

    [Test]
    public async Task CreateTemplate_WhenTheManifestIsRefused_ShouldLeaveNoLiveDraftBehind()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = await LiveCountAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var (isError, message) = await agent.CallToolAsync("create_template", new { name = "Kickoff", from = "workshop", manifest = new { label = new string('x', 80) } });

            isError.ShouldBeTrue();
            message.GetString()!.ShouldStartWith("label: ");
            message.GetString()!.ShouldContain("60");
            (await LiveCountAsync()).ShouldBe(before);
        }
    }

    // The rows the 50-template cap counts: every template that is not archived.
    private async Task<int> LiveCountAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Templates.CountAsync(t => t.ArchivedAt == null);
    }

    private static async Task<JsonElement> CreateAsync(McpTestClient agent, string name, string? manifestJson = null)
    {
        object args = manifestJson == null
            ? new { name, from = "workshop" }
            : new { name, from = "workshop", manifest = JsonDocument.Parse(manifestJson).RootElement };
        var (isError, result) = await agent.CallToolAsync("create_template", args);
        isError.ShouldBeFalse(result.ToString());
        return result;
    }
}
