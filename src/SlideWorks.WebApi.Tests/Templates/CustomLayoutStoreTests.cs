using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Layouts;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Custom layouts in a template (T10) through HTTP: the upload check, the stored form, the version's list, the owner's
// review and approval, and the publish gate. The compiler is FakeCustomLayoutCompiler: it returns the contract sample
// compiled for the scope of the body just sent, under the name the test gives it, so every stored layout reads back.
// The fake and the app are shared by the fixture, so the tests do not run in parallel.
[TestFixture]
[NonParallelizable]
public class CustomLayoutStoreTests : ApiTestFixture
{
    private const string SAMPLE_NAME = "bar-chart";
    private const string SAMPLE_SCOPE = "0a1b2c3d";

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeCustomLayoutCompiler.AddTo;

    private FakeCustomLayoutCompiler Compiler => Factory.Services.GetRequiredService<FakeCustomLayoutCompiler>();

    [Test]
    public async Task Upload_WhenTheCompilerRefuses_ShouldStoreNothingAndReturnEveryError()
    {
        var id = await CreateAsync();
        Compiler.Refuse(CustomLayoutError.Of("layout.html:2", CustomLayoutErrorCodes.MARKUP_TEXT), CustomLayoutError.Of("layout.css:4", CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE));

        using var put = await PutAsync(await StartAsync(id), Body("alpha"));
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe("layout_refused");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Select(e => e.GetProperty("path").GetString()).ShouldBe(["layout.html:2", "layout.css:4"]);
        errors.Select(e => e.GetProperty("code").GetString()).ShouldBe([CustomLayoutErrorCodes.MARKUP_TEXT, CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE]);
        (await RowsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenAccepted_ShouldKeepTheCompiledFormOnly()
    {
        var id = await CreateAsync();

        var asset = await UploadAsync(id, "alpha");

        var row = (await RowsAsync(id)).ShouldHaveSingleItem();
        row.Id.ShouldBe(asset);
        row.Kind.ShouldBe(TemplateAssetKind.Layout);
        row.MetaJson.ShouldNotBeNull();
        row.MetaJson.ShouldNotContain("<");
        var meta = JsonNode.Parse(row.MetaJson)!;
        meta["review"]!["approved"]!.GetValue<bool>().ShouldBeFalse();
        meta["layout"]!["css"]!.GetValue<string>().ShouldContain("cl-" + asset[..8] + "-alpha__chart");
        meta["layout"]!["tree"]!["root"]!.AsArray().ShouldNotBeEmpty();
        row.MetaJson.ShouldNotContain("<div></div>");
    }

    [Test]
    public async Task Upload_WhenAKeyIsMissing_ShouldRefuse()
    {
        var id = await CreateAsync();

        using var put = await PutAsync(await StartAsync(id), Encoding.UTF8.GetBytes("""{"markup":"<div></div>","css":""}"""));
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe("layout_refused");
        body.GetProperty("errors").EnumerateArray().Single().GetProperty("path").GetString().ShouldBe("layout");
        (await RowsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenAnExtraKeyIsSent_ShouldRefuse()
    {
        var id = await CreateAsync();

        using var put = await PutAsync(await StartAsync(id), Encoding.UTF8.GetBytes("""{"markup":"","css":"","slots":{},"extra":1}"""));

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("layout_refused");
        (await RowsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenOver800000Bytes_ShouldBeFileTooLargeAndStoreNothing()
    {
        var id = await CreateAsync();
        var before = Compiler.LastScope;
        var big = Encoding.UTF8.GetBytes("{\"markup\":\"" + new string('a', 800_000) + "\",\"css\":\"\",\"slots\":{}}");

        using var put = await PutAsync(await StartAsync(id), big);

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("file_too_large");
        Compiler.LastScope.ShouldBe(before);
        (await RowsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenAccepted_ShouldScopeTheClassesByTheAssetId()
    {
        var id = await CreateAsync();

        var asset = await UploadAsync(id, "alpha");

        Compiler.LastScope.ShouldBe(asset[..8]);
    }

    [Test]
    public async Task Upload_WhenTheSameBytesAreSentAgain_ShouldKeepTheApproval()
    {
        var id = await CreateAsync();
        var asset = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", asset));
        (await ApproveAsync(id, "alpha", asset)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var again = await UploadAsync(id, "alpha");

        again.ShouldBe(asset);
        (await StoredAsync(id, asset)).Approved.ShouldBeTrue();
    }

    [Test]
    public async Task Upload_WhenTheSameNameIsReplaced_ShouldNeedApprovalAgain()
    {
        var id = await CreateAsync();
        var first = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", first));
        (await ApproveAsync(id, "alpha", first)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await UploadAsync(id, "alpha", variant: "v2");
        await SetManifestAsync(id, ("alpha", second));
        var publish = await PublishAsync(id);

        second.ShouldNotBe(first);
        (await StoredAsync(id, second)).Approved.ShouldBeFalse();
        await ShouldBeRefusedAsync(publish, "layout_not_approved", id, "alpha");
    }

    [Test]
    public async Task Asset_WhenItIsALayout_ShouldNotBeServed()
    {
        var id = await CreateAsync();
        var asset = await UploadAsync(id, "alpha");

        var response = await Client.GetAsync($"/api/templates/{id}/assets/{asset}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Asset_WhenTheTemplateIsRead_ShouldNotSendTheCompiledLayoutToTheEditor()
    {
        var id = await CreateAsync();
        var asset = await UploadAsync(id, "alpha");

        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}");

        var row = editor.GetProperty("assets").EnumerateArray().Single(a => a.GetProperty("assetId").GetString() == asset);
        row.GetProperty("meta").GetRawText().ShouldBe("{}");
    }

    [Test]
    public async Task ForVersion_WhenTheDraftListsTwoLayouts_ShouldReturnThemInOrder()
    {
        var id = await CreateAsync();
        var beta = await UploadAsync(id, "beta");
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("beta", beta), ("alpha", alpha));

        var layouts = await ForVersionAsync(id, 0);

        layouts.Select(l => l.Layout.Name).ShouldBe(["beta", "alpha"]);
        layouts.Select(l => l.AssetId).ShouldBe([beta, alpha]);
        layouts.ShouldAllBe(l => !l.Approved);
    }

    [Test]
    public async Task ForVersion_WhenAnEntryNamesAFont_ShouldLeaveItOut()
    {
        var id = await CreateAsync();
        var font = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "head-400.woff2");
        var alpha = await UploadAsync(id, "alpha");
        await SetRawManifestAsync(id, ("fonty", font), ("alpha", alpha));

        var layouts = await ForVersionAsync(id, 0);

        layouts.Select(l => l.Layout.Name).ShouldBe(["alpha"]);
    }

    [Test]
    public async Task ForVersion_WhenAnEntryNamesAnotherTemplatesLayoutAsset_ShouldLeaveItOut()
    {
        var mine = await CreateAsync();
        var other = await CreateAsync();
        var foreign = await UploadAsync(other, "alpha");
        await SetRawManifestAsync(mine, ("alpha", foreign));

        var layouts = await ForVersionAsync(mine, 0);

        layouts.ShouldBeEmpty();
    }

    [Test]
    public async Task Publish_WhenALayoutIsNotApproved_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));
        var before = await RecordAsync(id);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "layout_not_approved", id, "alpha");
        (await RecordAsync(id)).Revision.ShouldBe(before.Revision);
        (await RecordAsync(id)).UpdatedAt.ShouldBe(before.UpdatedAt);
    }

    [Test]
    public async Task Publish_WhenAnEntryNamesNoLayout_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        await SetRawManifestAsync(id, ("alpha", "0123456789abcdef0123456789abcdef"));

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "layout_not_found", id, "customLayouts[0].file");
    }

    [Test]
    public async Task Publish_WhenAnEntryNamesAnotherTemplatesAsset_ShouldChangeNothing()
    {
        var mine = await CreateAsync();
        var other = await CreateAsync();
        var foreign = await UploadAsync(other, "alpha");
        await SetRawManifestAsync(mine, ("alpha", foreign));

        var response = await PublishAsync(mine);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "layout_not_found", mine, "customLayouts[0].file");
    }

    [Test]
    public async Task Publish_WhenTheNameDiffersFromTheCompiledName_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetRawManifestAsync(id, ("gamma", alpha));

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "layout_not_found", id, "customLayouts[0].name");
    }

    [Test]
    public async Task Publish_WhenTheSameNameIsListedTwice_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var first = await UploadAsync(id, "alpha");
        var second = await UploadAsync(id, "alpha", variant: "v2");
        await SetRawManifestAsync(id, ("alpha", first), ("alpha", second));

        var response = await PublishAsync(id);

        // ManifestJson.Parse refuses a repeated customLayouts[].name (SW-462) before the layouts row runs.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "manifest_invalid", id, "customLayouts[1].name");
    }

    [Test]
    public async Task Publish_WhenThirteenLayouts_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        await SetRawManifestAsync(id, [.. Enumerable.Range(1, 13).Select(n => ($"layout-{n:00}", "0123456789abcdef0123456789abcd" + n.ToString("00")))]);

        var response = await PublishAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldBeRefusedAsync(response, "too_many_layouts", id, "customLayouts");
    }

    [Test]
    public async Task PublishTemplate_WhenALayoutIsNotApproved_ShouldRefuseOverMcp()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "publish_template", $$"""{"template":"{{id}}"}""");

        result.GetProperty("isError").GetBoolean().ShouldBeTrue(result.GetRawText());
        McpWire.TextOf(result).ShouldContain("alpha");
        (await VersionsAsync(id)).ShouldBe([0]);
    }

    [Test]
    public async Task Checklist_WhenALayoutIsUnapproved_ShouldFailTheLayoutsRow()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        var beta = await UploadAsync(id, "beta");
        await SetManifestAsync(id, ("alpha", alpha), ("beta", beta));
        (await ApproveAsync(id, "beta", beta)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var checklist = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}/checklist");

        var row = checklist.GetProperty("checklist").EnumerateArray().Single(r => r.GetProperty("key").GetString() == "layouts");
        row.GetProperty("state").GetString().ShouldBe("fail");
        row.GetProperty("tab").GetString().ShouldBe("layouts");
        row.GetProperty("detail").GetString().ShouldBe("alpha");
        checklist.GetProperty("canPublish").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Checklist_WhenThereAreNoLayouts_ShouldPassTheLayoutsRow()
    {
        var id = await CreateAsync();

        var checklist = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}/checklist");

        checklist.GetProperty("checklist").EnumerateArray().Single(r => r.GetProperty("key").GetString() == "layouts")
            .GetProperty("state").GetString().ShouldBe("pass");
    }

    [Test]
    public async Task Review_WhenAskedByTheOwner_ShouldMatchTheScreenShape()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));

        var response = await Client.GetAsync($"/api/templates/{id}/layouts/alpha/review");
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, view.ToString());
        JsonShape.AssertMatches(view, TemplateScreenShapes.LAYOUT_REVIEW_FILE, TemplateScreenShapes.LayoutReview);
        view.GetProperty("asset").GetString().ShouldBe(alpha);
        view.GetProperty("approved").GetBoolean().ShouldBeFalse();
        view.GetProperty("steps").GetInt32().ShouldBe(3);
        view.GetProperty("safeArea").GetProperty("ok").GetBoolean().ShouldBeFalse();
        view.GetProperty("safeArea").GetProperty("overflow").GetArrayLength().ShouldBe(0);
        view.GetProperty("contrast").GetProperty("ok").GetBoolean().ShouldBeFalse();
        view.GetProperty("layout").GetProperty("slots").GetProperty("headline").GetProperty("type").GetString().ShouldBe("text");
    }

    [Test]
    public async Task Review_WhenSharedWithButNotTheOwner_ShouldBe404()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));
        await ShareWithAlexAsync(id);
        using var alex = ClientFor(TestUser.Alex);
        using var unknown = await Client.GetAsync("/api/templates/tpl_doesnotexist/layouts/alpha/review");

        var shared = await alex.GetAsync($"/api/templates/{id}/layouts/alpha/review");

        shared.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await shared.Content.ReadAsStringAsync()).Replace(id, "ID", StringComparison.Ordinal)
            .ShouldBe((await unknown.Content.ReadAsStringAsync()).Replace("tpl_doesnotexist", "ID", StringComparison.Ordinal));
    }

    [Test]
    public async Task Review_WhenTheNameIsNotInTheDraft_ShouldBe404()
    {
        var id = await CreateAsync();

        var response = await Client.GetAsync($"/api/templates/{id}/layouts/alpha/review");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Approve_WhenNotTheOwner_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));
        await ShareWithAlexAsync(id);
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        var shared = await alex.PostAsJsonAsync($"/api/templates/{id}/layouts/alpha/approve", new { asset = alpha, overflow = Array.Empty<string>(), contrast = Array.Empty<string>() });
        var stranger = await mira.PostAsJsonAsync($"/api/templates/{id}/layouts/alpha/approve", new { asset = alpha, overflow = Array.Empty<string>(), contrast = Array.Empty<string>() });
        var unknown = await mira.PostAsJsonAsync("/api/templates/tpl_doesnotexist/layouts/alpha/approve", new { asset = alpha, overflow = Array.Empty<string>(), contrast = Array.Empty<string>() });

        shared.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        stranger.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Content.ReadAsStringAsync()).Replace(id, "ID", StringComparison.Ordinal)
            .ShouldBe((await unknown.Content.ReadAsStringAsync()).Replace("tpl_doesnotexist", "ID", StringComparison.Ordinal));
        (await StoredAsync(id, alpha)).Approved.ShouldBeFalse();
    }

    [Test]
    public async Task Approve_WhenTheReportHasOverflow_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));

        var response = await ApproveAsync(id, "alpha", alpha, overflow: ["headline"]);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe("layout_overflows");
        body.GetProperty("errors").EnumerateArray().Single().GetProperty("path").GetString().ShouldBe("headline");
        (await StoredAsync(id, alpha)).Approved.ShouldBeFalse();
    }

    [Test]
    public async Task Approve_WhenTheReportHasLowContrast_ShouldChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));

        var response = await ApproveAsync(id, "alpha", alpha, contrast: ["muted on chip"]);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe("layout_low_contrast");
        (await StoredAsync(id, alpha)).Approved.ShouldBeFalse();
    }

    [Test]
    public async Task Approve_WhenTheAssetIsStale_ShouldBe409()
    {
        var id = await CreateAsync();
        var first = await UploadAsync(id, "alpha");
        var second = await UploadAsync(id, "alpha", variant: "v2");
        await SetManifestAsync(id, ("alpha", second));

        var response = await ApproveAsync(id, "alpha", first);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("layout_changed");
        (await StoredAsync(id, first)).Approved.ShouldBeFalse();
        (await StoredAsync(id, second)).Approved.ShouldBeFalse();
    }

    [Test]
    public async Task Approve_WhenTheNameIsNotInTheDraft_ShouldBe404AndChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");

        var response = await ApproveAsync(id, "alpha", alpha);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredAsync(id, alpha)).Approved.ShouldBeFalse();
    }

    [Test]
    public async Task Approve_WhenTheOwnerReportsClean_ShouldLetPublishPass()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));

        var approved = await ApproveAsync(id, "alpha", alpha);
        var review = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}/layouts/alpha/review");
        var published = await PublishAsync(id);

        approved.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        review.GetProperty("approved").GetBoolean().ShouldBeTrue();
        review.GetProperty("safeArea").GetProperty("ok").GetBoolean().ShouldBeTrue();
        published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        (await VersionsAsync(id)).ShouldBe([0, 1]);
        var layouts = await ForVersionAsync(id, 1);
        layouts.ShouldHaveSingleItem().Approved.ShouldBeTrue();
    }

    [Test]
    public async Task Review_WhenTheEntryNameDiffersFromTheCompiledName_ShouldBe404()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetRawManifestAsync(id, ("gamma", alpha));

        using var asCompiled = await Client.GetAsync($"/api/templates/{id}/layouts/alpha/review");
        using var asListed = await Client.GetAsync($"/api/templates/{id}/layouts/gamma/review");

        asCompiled.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        asListed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Approve_WhenTheEntryNameDiffersFromTheCompiledName_ShouldBe404AndChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetRawManifestAsync(id, ("gamma", alpha));

        var response = await ApproveAsync(id, "gamma", alpha);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredAsync(id, alpha)).Approved.ShouldBeFalse();
    }

    [Test]
    public async Task Checklist_WhenEntriesAreInvalid_ShouldNameEveryAffectedLayoutInTheRow()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetRawManifestAsync(id, ("alpha", alpha), ("gamma", alpha), ("ghost", "0123456789abcdef0123456789abcdef"));

        var checklist = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}/checklist");

        var row = checklist.GetProperty("checklist").EnumerateArray().Single(r => r.GetProperty("key").GetString() == "layouts");
        row.GetProperty("state").GetString().ShouldBe("fail");
        var detail = row.GetProperty("detail").GetString()!;
        detail.ShouldContain("gamma");
        detail.ShouldContain("ghost");
        detail.ShouldContain("alpha"); // valid but unapproved: it blocks publishing too
        checklist.GetProperty("canPublish").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Approve_WhenTheTemplateIsArchived_ShouldRefuseAndChangeNothing()
    {
        var id = await CreateAsync();
        var alpha = await UploadAsync(id, "alpha");
        await SetManifestAsync(id, ("alpha", alpha));
        (await Client.PostAsync($"/api/templates/{id}/archive", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await ApproveAsync(id, "alpha", alpha);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe("template_archived");
        (await StoredAsync(id, alpha)).Approved.ShouldBeFalse();
    }

    private static byte[] Body(string name, string variant = "")
        => Encoding.UTF8.GetBytes($$$"""{"markup":"<div>{{{name}}}{{{variant}}}</div>","css":".a{}","slots":{"name":"{{{name}}}"}}""");

    // The contract sample, compiled for this body: its scope is the start of the body's hash and its name the test's.
    private static CompiledLayout SampleFor(byte[] body, string name, FakeCustomLayoutCompiler compiler)
    {
        var scope = Convert.ToHexStringLower(SHA256.HashData(body))[..8];
        var text = File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "..", "custom-layout-compiled.sample.json"))
            .Replace(SAMPLE_SCOPE, scope, StringComparison.Ordinal)
            .Replace(SAMPLE_NAME, name, StringComparison.Ordinal);
        return compiler.WithDefinition(name, new LayoutDefinition(name, "A test layout that reveals its bars.", false, [], "bars")).Read(text);
    }

    // Uploads one layout document as the owner, with the compiler set to accept it, and returns its asset id.
    private async Task<string> UploadAsync(string template, string name, string variant = "")
    {
        var body = Body(name, variant);
        Compiler.Accept(SampleFor(body, name, Compiler));
        using var put = await PutAsync(await StartAsync(template), body);
        var stored = await put.Content.ReadFromJsonAsync<JsonElement>();
        put.StatusCode.ShouldBe(HttpStatusCode.OK, stored.ToString());
        return stored.GetProperty("assetId").GetString()!;
    }

    private async Task<string> StartAsync(string template)
    {
        var response = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "layout", fileName = "layout.json", rightsConfirmed = false });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
    }

    private async Task<HttpResponseMessage> PutAsync(string uploadUrl, byte[] bytes)
    {
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(bytes);
        return await http.PutAsync(uploadUrl, content);
    }

    private async Task<string> CreateAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private Task<HttpResponseMessage> ApproveAsync(string id, string name, string asset, string[]? overflow = null, string[]? contrast = null)
        => Client.PostAsJsonAsync($"/api/templates/{id}/layouts/{name}/approve", new { asset, overflow = overflow ?? [], contrast = contrast ?? [] });

    private Task<HttpResponseMessage> PublishAsync(string id) => Client.PostAsync($"/api/templates/{id}/publish", null);

    // The draft's customLayouts through the public draft route (GET the editor, PUT the manifest with its revision), so the
    // route's own checks run. Used for every valid manifest.
    private async Task SetManifestAsync(string id, params (string Name, string File)[] entries)
    {
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}");
        var manifest = JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!;
        manifest["customLayouts"] = Entries(entries);
        using var put = await Client.PutAsJsonAsync($"/api/templates/{id}", new { manifest, revision = editor.GetProperty("revision").GetInt32() });
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
    }

    // The draft's customLayouts written straight to the database. Only for malformed states the draft route cannot create
    // (an entry naming no layout, another template's asset, a font, a mismatched or repeated name, too many entries).
    private async Task SetRawManifestAsync(string id, params (string Name, string File)[] entries)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var draft = await db.TemplateVersions.SingleAsync(v => v.TemplateId == id && v.Version == TemplateVersion.DRAFT);
        var manifest = JsonNode.Parse(draft.ManifestJson)!;
        manifest["customLayouts"] = Entries(entries);
        draft.ManifestJson = manifest.ToJsonString();
        await db.SaveChangesAsync();
    }

    private static JsonArray Entries((string Name, string File)[] entries)
        => new([.. entries.Select(e => (JsonNode)new JsonObject { ["name"] = e.Name, ["file"] = e.File })]);

    private async Task ShareWithAlexAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Templates.SingleAsync(t => t.Id == id)).Access = DeckSharing.Named;
        await db.SaveChangesAsync();
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);
    }

    private async Task<IReadOnlyList<TemplateLayout>> ForVersionAsync(string id, int version)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITemplateLayouts>().ForVersionAsync(id, version, CancellationToken.None);
    }

    private async Task<List<TemplateAsset>> RowsAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateAssets.AsNoTracking().Where(a => a.TemplateId == id).ToListAsync();
    }

    private async Task<StoredLayout> StoredAsync(string id, string asset)
    {
        var row = (await RowsAsync(id)).Single(a => a.Id == asset);
        return StoredLayout.Read(row.MetaJson, asset, Compiler);
    }

    private async Task<TemplateRecord> RecordAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Templates.AsNoTracking().SingleAsync(t => t.Id == id);
    }

    private async Task<List<int>> VersionsAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == id).OrderBy(v => v.Version).Select(v => v.Version).ToListAsync();
    }

    // A refused publish answers 400 with the code and the path, and leaves the draft as the only version.
    private async Task ShouldBeRefusedAsync(HttpResponseMessage response, string code, string id, string path)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        body.GetProperty("error").GetString().ShouldBe(code);
        body.GetProperty("path").GetString().ShouldBe(path);
        (await VersionsAsync(id)).ShouldBe([0]);
    }
}
