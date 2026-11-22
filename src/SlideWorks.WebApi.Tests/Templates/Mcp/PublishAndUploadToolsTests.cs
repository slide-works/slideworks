using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates.Mcp;

// publish_template, upload_asset and list_templates on fakes: what each tool asks of the services and how it words the
// answer, over raw JSON-RPC. The same tools on the real services are in PublishAndUploadToolsRealTests. A fake's refusal is
// for every call, so these tests do not run in parallel and each accepts calls again when it is done.
[TestFixture]
[NonParallelizable]
public class PublishAndUploadToolsTests : ApiTestFixture
{
    private const string TEMPLATE = "tpl_Nw4kT8pQz2Lm";
    private readonly RecordingAssets _assets = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplatePublishing.Register(services);
        FakeTemplates.Register(services);
        services.AddSingleton<ITemplateAssets>(_assets);
    };

    private FakeTemplatePublishing Publishing => Factory.Services.GetRequiredService<FakeTemplatePublishing>();

    private FakeTemplates Templates => Factory.Services.GetRequiredService<FakeTemplates>();

    [TearDown]
    public void AcceptCallsAgain()
    {
        Publishing.Refuse(null);
        Publishing.Checklist = new TemplateChecklistView([Row("palette", TemplateCheckState.Pass, "")], true);
        _assets.Refuse = null;
    }

    [Test]
    public async Task UploadAsset_WhenKindIsFontOnTheWire_ShouldReturnAnUploadUrl()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "upload_asset", $$"""{"template":"{{TEMPLATE}}","kind":"font","filename":"brand-sans.woff2"}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.EnumerateObject().Select(p => p.Name).ShouldBe(["upload_url", "curl", "kind", "expires"]);
        content.GetProperty("upload_url").GetString().ShouldBe(RecordingAssets.URL);
        content.GetProperty("curl").GetString().ShouldBe($"curl -T brand-sans.woff2 \"{RecordingAssets.URL}\"");
        content.GetProperty("kind").GetString().ShouldBe("font");
        content.GetProperty("expires").GetString().ShouldBe("in 10 minutes");
    }

    [Test]
    public async Task UploadAsset_WhenKindIsFont_ShouldNeverConfirmRights()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, _) = await agent.CallToolAsync("upload_asset", new { template = TEMPLATE, kind = "font", filename = "brand-sans.woff2" });

        isError.ShouldBeFalse();
        _assets.Starts[^1].ShouldBe((TEMPLATE, TemplateAssetKind.Font, "brand-sans.woff2", false));
    }

    [Test]
    public async Task UploadAsset_WhenKindIsLayout_ShouldStartLikeTheOthers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, content) = await agent.CallToolAsync("upload_asset", new { template = TEMPLATE, kind = "layout", filename = "hero layout.json" });

        isError.ShouldBeFalse();
        _assets.Starts[^1].Kind.ShouldBe(TemplateAssetKind.Layout);
        content.GetProperty("curl").GetString().ShouldBe($"curl -T 'hero layout.json' \"{RecordingAssets.URL}\"");
    }

    [TestCase("-", "./-")]
    [TestCase("-rf.woff2", "./-rf.woff2")]
    [TestCase("-my font.woff2", "'-my font.woff2'")]
    public async Task UploadAsset_WhenTheNameStartsWithADash_ShouldNotLetCurlReadItAsAnOption(string filename, string written)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, content) = await agent.CallToolAsync("upload_asset", new { template = TEMPLATE, kind = "font", filename });

        isError.ShouldBeFalse();
        content.GetProperty("curl").GetString().ShouldBe($"curl -T {written} \"{RecordingAssets.URL}\"");
    }

    [Test]
    public async Task UploadAsset_WhenRightsConfirmedIsSentOnTheWire_ShouldRefuseItAsUnknownAndStartNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = _assets.Starts.Count;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "upload_asset", $$"""{"template":"{{TEMPLATE}}","kind":"font","filename":"brand-sans.woff2","rights_confirmed":true}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldContain("rights_confirmed: unknown argument");
        _assets.Starts.Count.ShouldBe(before);
    }

    [Test]
    public async Task UploadAsset_WhenKindIsUnknown_ShouldNameTheKinds()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = _assets.Starts.Count;

        var (isError, message) = await agent.CallToolAsync("upload_asset", new { template = TEMPLATE, kind = "video", filename = "a.mp4" });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe("kind: must be font, logo, art or layout");
        _assets.Starts.Count.ShouldBe(before);
    }

    [Test]
    public async Task UploadAsset_WhenTheTemplateIsNotMine_ShouldSayNotFound()
    {
        _assets.Refuse = new NotFoundException($"No template '{TEMPLATE}'.");
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);

        var (isError, message) = await agent.CallToolAsync("upload_asset", new { template = TEMPLATE, kind = "logo", filename = "mark.svg" });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"No template '{TEMPLATE}'.");
    }

    [Test]
    public async Task PublishTemplate_WhenChecksPassOnTheWire_ShouldReturnTheVersion()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Publishing.Calls().Count;

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "publish_template", $$"""{"template":"{{TEMPLATE}}"}""");

        result.TryGetProperty("isError", out var isError).ShouldBeFalse(isError.ToString());
        var content = result.GetProperty("structuredContent");
        content.EnumerateObject().Select(p => p.Name).ShouldBe(["template", "version", "url"]);
        content.GetProperty("template").GetString().ShouldBe(TEMPLATE);
        content.GetProperty("version").GetInt32().ShouldBe(Publishing.Published.Version);
        content.GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/templates/{TEMPLATE}/edit");
        Publishing.Calls().Skip(before).ToList().ShouldBe([$"Checklist {TEMPLATE}", $"Publish {TEMPLATE}"]);
    }

    [Test]
    public async Task PublishTemplate_WhenRightsAreNotConfirmed_ShouldPointAtTheEditor()
    {
        Publishing.Checklist = new TemplateChecklistView(
            [Row("palette", TemplateCheckState.Pass, ""), Row("fontRights", TemplateCheckState.Todo, "Confirm you can serve these fonts.")], false);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = Publishing.Calls().Count;

        var (isError, message) = await agent.CallToolAsync("publish_template", new { template = TEMPLATE });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"Can't publish yet: Confirm you can serve these fonts. Ask a person to open {TestAppFactory.PUBLIC_URL}/templates/{TEMPLATE}/edit");
        Publishing.Calls().Skip(before).ToList().ShouldBe([$"Checklist {TEMPLATE}"]);
    }

    [Test]
    public async Task PublishTemplate_WhenAnotherCheckFails_ShouldListEachFailingDetail()
    {
        Publishing.Checklist = new TemplateChecklistView(
            [Row("palette", TemplateCheckState.Fail, "Text on the background is too faint."), Row("fonts", TemplateCheckState.Fail, "Too many fonts."), Row("limits", TemplateCheckState.Pass, "")], false);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, message) = await agent.CallToolAsync("publish_template", new { template = TEMPLATE });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe("Can't publish yet: Text on the background is too faint. Too many fonts.");
    }

    [Test]
    public async Task PublishTemplate_WhenTheTemplateIsNotMine_ShouldSayNotFound()
    {
        Publishing.Refuse(new NotFoundException($"No template '{TEMPLATE}'."));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);

        var (isError, message) = await agent.CallToolAsync("publish_template", new { template = TEMPLATE });

        isError.ShouldBeTrue();
        message.GetString().ShouldBe($"No template '{TEMPLATE}'.");
    }

    [Test]
    public async Task ListTemplates_WhenOneIsSharedWithMe_ShouldListItsVersion()
    {
        var owner = Guid.NewGuid();
        Templates.Add(Uploaded("tpl_sharedwithsam", "Harbour talks", 3, owner, "Alex Chen"), TestUser.Sam.Email);
        Templates.Add(Uploaded("tpl_ownedbysam", "Sam's own", 1, null, null), TestUser.Sam.Email);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Templates.Add(Uploaded("tpl_ownedbysam2", "Sam's second", 2, agent.PresenterId, "Sam Rivera"), TestUser.Sam.Email);

        var (isError, result) = await agent.CallToolAsync("list_templates");
        var entries = result.GetProperty("templates").EnumerateArray().ToList();

        isError.ShouldBeFalse();
        var shared = entries.Single(e => e.GetProperty("ref").GetString() == "tpl_sharedwithsam");
        shared.GetProperty("source").GetString().ShouldBe("shared");
        shared.GetProperty("version").GetInt32().ShouldBe(3);
        shared.GetProperty("owner").GetString().ShouldBe("Alex Chen");
        shared.GetProperty("name").GetString().ShouldBe("tpl_sharedwithsam");
        shared.GetProperty("label").GetString().ShouldBe("Harbour talks");
        var mine = entries.Single(e => e.GetProperty("ref").GetString() == "tpl_ownedbysam2");
        mine.GetProperty("source").GetString().ShouldBe("yours");
        mine.GetProperty("version").GetInt32().ShouldBe(2);
        mine.GetProperty("owner").ValueKind.ShouldBe(JsonValueKind.Null);
        entries.Select(e => e.GetProperty("source").GetString()).SkipWhile(s => s == "builtin").ShouldNotContain("builtin");
    }

    [Test]
    public async Task ListTemplates_WhenOnlyBuiltIns_ShouldKeepTodaysEntries()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Mira);

        var (isError, result) = await agent.CallToolAsync("list_templates");
        var entries = result.GetProperty("templates").EnumerateArray().ToList();

        isError.ShouldBeFalse();
        entries.Select(e => e.GetProperty("name").GetString()).ShouldBe(new TemplateCatalogue().All.Select(t => t.Name));
        entries.Select(e => e.EnumerateObject().Select(p => p.Name).ToList()).ShouldAllBe(names => names.SequenceEqual(new[]
        {
            "name", "label", "purpose", "is_default", "layouts", "chrome", "outline", "house_style", "type", "locked_palette", "ref", "source", "version", "owner",
        }));
        foreach (var entry in entries)
        {
            entry.GetProperty("ref").GetString().ShouldBe(entry.GetProperty("name").GetString());
            entry.GetProperty("source").GetString().ShouldBe("builtin");
            entry.GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);
            entry.GetProperty("owner").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    private static ResolvedTemplate Uploaded(string reference, string label, int version, Guid? owner, string? ownerName)
        => new(new TemplateCatalogue().Default with { Name = reference, Label = label, IsDefault = false }, reference, version, false)
        {
            OwnerId = owner ?? Guid.NewGuid(),
            OwnerName = ownerName,
            LatestVersion = version,
        };

    private static TemplateChecklistRow Row(string key, TemplateCheckState state, string detail) => new(key, key, state, detail, "basics");

    // ITemplateAssets for these tests: records every start and answers a fixed address.
    private sealed class RecordingAssets : ITemplateAssets
    {
        public const string URL = "https://slides.example/api/template-uploads/token123";

        private readonly Lock _gate = new();
        private readonly List<(string Template, TemplateAssetKind Kind, string? FileName, bool RightsConfirmed)> _starts = [];

        public Exception? Refuse { get; set; }

        public IReadOnlyList<(string Template, TemplateAssetKind Kind, string? FileName, bool RightsConfirmed)> Starts
        {
            get
            {
                lock (_gate)
                {
                    return [.. _starts];
                }
            }
        }

        public Task<AssetUploadStarted> StartUploadAsync(ICurrentActor actor, string templateId, TemplateAssetKind kind, string? fileName, bool rightsConfirmed)
        {
            if (Refuse != null)
            {
                throw Refuse;
            }
            lock (_gate)
            {
                _starts.Add((templateId, kind, fileName, rightsConfirmed));
            }
            return Task.FromResult(new AssetUploadStarted(URL, "in 10 minutes"));
        }

        public Task<StoredAsset> CompleteAsync(string token, Stream content, CancellationToken ct) => throw new NotSupportedException();

        public Task<AssetFile> OpenAsync(string templateId, string assetId, string? guestId = null) => throw new NotSupportedException();

        public Task<int> ConfirmFontRightsAsync(ICurrentActor actor, string templateId) => throw new NotSupportedException();

        public Task<StoredAsset> StoreAsync(string templateId, TemplateAssetKind kind, string fileName, Stream content, CancellationToken ct) => throw new NotSupportedException();
    }
}

// The same three tools on the app's real services: upload_asset through TemplateAssets and the upload route, publish_template
// through TemplatePublishing, list_templates through Templates. Sam owns the templates; Alex is a person who may not see them,
// and what he is told is exactly what an unknown id gets.
[TestFixture]
[NonParallelizable]
public class PublishAndUploadToolsRealTests : ApiTestFixture
{
    [Test]
    public async Task UploadAsset_WhenCalledOnTheRealService_ShouldStoreTheFontOnceWithRightsUnconfirmed()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = await CreateAsync(agent, "Kickoff");

        var (isError, started) = await agent.CallToolAsync("upload_asset", new { template = id, kind = "font", filename = "normal.woff2" });
        var url = started.GetProperty("upload_url").GetString()!;
        using var first = await PutAsync(url, FontFixtures.Normal());
        using var second = await PutAsync(url, FontFixtures.Normal());
        var stored = await first.Content.ReadFromJsonAsync<JsonElement>();

        isError.ShouldBeFalse(started.ToString());
        url.ShouldMatch($"^{TestAppFactory.PUBLIC_URL}/api/template-uploads/[A-Za-z0-9_-]{{43}}$");
        started.GetProperty("curl").GetString().ShouldBe($"curl -T normal.woff2 \"{url}\"");
        started.GetProperty("expires").GetString().ShouldBe("in 10 minutes");
        first.StatusCode.ShouldBe(HttpStatusCode.OK, stored.ToString());
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.UPLOAD_USED);
        var assets = await AssetsAsync(id);
        assets.Count.ShouldBe(1);
        assets[0].Id.ShouldBe(stored.GetProperty("assetId").GetString());
        assets[0].Kind.ShouldBe(TemplateAssetKind.Font);
        assets[0].RightsConfirmed.ShouldBeFalse();
    }

    [Test]
    public async Task UploadAsset_WhenTheTemplateIsNotMine_ShouldSayNotFoundAsForAnUnknownIdAndStartNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var id = await CreateAsync(sam, "Kickoff");

        var (foreignError, foreign) = await alex.CallToolAsync("upload_asset", new { template = id, kind = "logo", filename = "mark.svg" });
        var (unknownError, unknown) = await alex.CallToolAsync("upload_asset", new { template = "tpl_doesnotexist", kind = "logo", filename = "mark.svg" });

        foreignError.ShouldBeTrue();
        unknownError.ShouldBeTrue();
        unknown.GetString().ShouldBe("Template not found");
        foreign.GetString().ShouldBe(unknown.GetString());
        await using var scope = Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateUploads.CountAsync(u => u.TemplateId == id)).ShouldBe(0);
    }

    [Test]
    public async Task UploadAsset_WhenKindIsLayout_ShouldLeaveTheRefusalToThePut()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = await CreateAsync(agent, "Kickoff");

        var (isError, started) = await agent.CallToolAsync("upload_asset", new { template = id, kind = "layout", filename = "hero.json" });
        using var put = await PutAsync(started.GetProperty("upload_url").GetString()!, "{\"markup\":\"<div></div>\",\"css\":\"\",\"slots\":[]}"u8.ToArray());
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        isError.ShouldBeFalse(started.ToString());
        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.LAYOUT_REFUSED);
    }

    [Test]
    public async Task PublishTemplate_WhenCalledOnTheRealService_ShouldPublishTheNextVersion()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = await CreateAsync(agent, "Kickoff");

        var first = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "publish_template", $$"""{"template":"{{id}}"}""");
        var (secondError, second) = await agent.CallToolAsync("publish_template", new { template = id });
        var (readError, read) = await agent.CallToolAsync("get_template", new { template = id });

        first.TryGetProperty("isError", out var firstError).ShouldBeFalse(firstError.ToString());
        first.GetProperty("structuredContent").GetProperty("version").GetInt32().ShouldBe(1);
        first.GetProperty("structuredContent").GetProperty("template").GetString().ShouldBe(id);
        first.GetProperty("structuredContent").GetProperty("url").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/templates/{id}/edit");
        secondError.ShouldBeFalse(second.ToString());
        second.GetProperty("version").GetInt32().ShouldBe(2);
        readError.ShouldBeFalse(read.ToString());
        read.GetProperty("status").GetString().ShouldBe("published");
        read.GetProperty("version").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task PublishTemplate_WhenAFontIsNotConfirmed_ShouldPointAtTheEditorAndPublishNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = await CreateAsync(agent, "Kickoff");
        var (_, started) = await agent.CallToolAsync("upload_asset", new { template = id, kind = "font", filename = "normal.woff2" });
        using var put = await PutAsync(started.GetProperty("upload_url").GetString()!, FontFixtures.Normal());
        var asset = (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetId").GetString()!;
        var fonts = new[] { new { role = "head", family = "Brand Sans", file = asset, weight = 700, style = "normal" } };
        var (updateError, update) = await agent.CallToolAsync("update_template", new { template = id, manifest = new { fonts } });

        var (isError, message) = await agent.CallToolAsync("publish_template", new { template = id });
        var (_, read) = await agent.CallToolAsync("get_template", new { template = id });

        updateError.ShouldBeFalse(update.ToString());
        isError.ShouldBeTrue();
        message.GetString().ShouldStartWith("Can't publish yet: ");
        message.GetString().ShouldEndWith($"Ask a person to open {TestAppFactory.PUBLIC_URL}/templates/{id}/edit");
        read.GetProperty("status").GetString().ShouldBe("draft");
    }

    [Test]
    public async Task PublishTemplate_WhenTheTemplateIsNotMine_ShouldSayNotFoundAsForAnUnknownIdAndPublishNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var id = await CreateAsync(sam, "Kickoff");

        var (foreignError, foreign) = await alex.CallToolAsync("publish_template", new { template = id });
        var (unknownError, unknown) = await alex.CallToolAsync("publish_template", new { template = "tpl_doesnotexist" });
        var (_, read) = await sam.CallToolAsync("get_template", new { template = id });

        foreignError.ShouldBeTrue();
        unknownError.ShouldBeTrue();
        foreign.GetString().ShouldBe($"No template '{id}'.");
        unknown.GetString().ShouldBe("No template 'tpl_doesnotexist'.");
        read.GetProperty("status").GetString().ShouldBe("draft");
    }

    [Test]
    public async Task ListTemplates_WhenOneIsMineAndOneIsShared_ShouldListBothAfterTheBuiltIns()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var mine = await CreateAsync(alex, "Alex's own");
        _ = await alex.CallToolAsync("publish_template", new { template = mine });
        var draftOnly = await CreateAsync(alex, "Alex's draft");
        var manifest = await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "..", "template-manifest.sample.json"));
        var shared = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, manifest, version: 2, access: DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, shared, TestUser.Alex.Email);

        var (_, forAlex) = await alex.CallToolAsync("list_templates");
        var (_, forSam) = await sam.CallToolAsync("list_templates");
        var entries = forAlex.GetProperty("templates").EnumerateArray().ToList();

        var builtIns = new TemplateCatalogue().All.Count;
        entries.Take(builtIns).Select(e => e.GetProperty("source").GetString()).ShouldAllBe(s => s == "builtin");
        var own = entries.Single(e => e.GetProperty("ref").GetString() == mine);
        own.GetProperty("source").GetString().ShouldBe("yours");
        own.GetProperty("version").GetInt32().ShouldBe(1);
        own.GetProperty("owner").ValueKind.ShouldBe(JsonValueKind.Null);
        var other = entries.Single(e => e.GetProperty("ref").GetString() == shared);
        other.GetProperty("source").GetString().ShouldBe("shared");
        other.GetProperty("version").GetInt32().ShouldBe(2);
        other.GetProperty("owner").GetString().ShouldBe(TestUser.Sam.Name);
        entries.Select(e => e.GetProperty("ref").GetString()).ShouldNotContain(draftOnly);
        forSam.GetProperty("templates").EnumerateArray().Select(e => e.GetProperty("ref").GetString()).ShouldNotContain(mine);
        forSam.GetProperty("templates").EnumerateArray().Select(e => e.GetProperty("ref").GetString()).ShouldContain(shared);
    }

    private async Task<IReadOnlyList<TemplateAsset>> AssetsAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateAssets.AsNoTracking().Where(a => a.TemplateId == template).ToListAsync();
    }

    // PUTs the bytes as an agent or a script would: no sign-in, no Origin.
    private async Task<HttpResponseMessage> PutAsync(string uploadUrl, byte[] bytes)
    {
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(bytes);
        return await http.PutAsync(uploadUrl, content);
    }

    private static async Task<string> CreateAsync(McpTestClient agent, string name)
    {
        var (isError, result) = await agent.CallToolAsync("create_template", new { name, from = "workshop" });
        isError.ShouldBeFalse(result.ToString());
        return result.GetProperty("template").GetString()!;
    }
}
