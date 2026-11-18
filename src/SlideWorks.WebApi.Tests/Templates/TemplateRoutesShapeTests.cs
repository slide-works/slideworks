using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The template routes (T5) over the three fakes, never the stubs: what each route answers on the wire against its screen
// sample, what it hands to its service, and who may call it. The fixture's app and its fakes are shared, so each test
// uses a template id of its own.
[TestFixture]
public class TemplateRoutesShapeTests : ApiTestFixture
{
    private static readonly JsonSerializerOptions WIRE = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplateDrafts.Register(services);
        FakeTemplatePublishing.Register(services);
        FakeTemplateSharing.Register(services);
    };

    private FakeTemplateDrafts Drafts => Factory.Services.GetRequiredService<FakeTemplateDrafts>();

    private FakeTemplatePublishing Publishing => Factory.Services.GetRequiredService<FakeTemplatePublishing>();

    private FakeTemplateSharing Sharing => Factory.Services.GetRequiredService<FakeTemplateSharing>();

    [Test]
    public async Task GetTemplates_WhenSignedIn_ShouldMatchTheScreenShape()
    {
        var response = await Client.GetAsync("/api/templates");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        JsonShape.AssertMatches(JsonDocument.Parse(body).RootElement, TemplateScreenShapes.TEMPLATES_FILE, TemplateScreenShapes.Templates);
        body.ShouldContain("\"status\":\"published\"", Case.Sensitive);
        body.ShouldContain("\"sharing\":\"named\"", Case.Sensitive);
        body.ShouldContain("\"builtIn\":[", Case.Sensitive);
    }

    [Test]
    public async Task GetTemplate_WhenSignedIn_ShouldMatchTheScreenShape()
    {
        var response = await Client.GetAsync("/api/templates/tpl_editor000001");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var view = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(view, TemplateScreenShapes.EDITOR_FILE, TemplateScreenShapes.Editor);
        view.GetProperty("manifest").GetProperty("palette").GetProperty("accent-2").GetString().ShouldBe("#0e7490");
        body.ShouldContain("\"kind\":\"font\"", Case.Sensitive);
        body.ShouldContain("\"state\":\"todo\"", Case.Sensitive);
        Drafts.Calls().ShouldContain("Get tpl_editor000001");
    }

    [Test]
    public async Task GetChecklist_WhenSignedIn_ShouldMatchTheScreenShape()
    {
        var response = await Client.GetAsync("/api/templates/tpl_checklist001/checklist");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, TemplateScreenShapes.ChecklistExample(), "template-editor.json's checklist", TemplateScreenShapes.Checklist);
        view.GetProperty("checklist").EnumerateArray().Select(row => row.GetProperty("key").GetString())
            .ShouldBe(["palette", "fonts", "fontRights", "logoArt", "limits"]);
        Publishing.Calls().ShouldContain("Checklist tpl_checklist001");
    }

    [Test]
    public async Task GetSharing_WhenSignedIn_ShouldMatchTheScreenShape()
    {
        var response = await Client.GetAsync("/api/templates/tpl_sharing00001/sharing");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        JsonShape.AssertMatches(JsonDocument.Parse(body).RootElement, TemplateScreenShapes.SHARING_FILE, TemplateScreenShapes.Sharing);
        body.ShouldContain("\"level\":\"named\"", Case.Sensitive);
        Sharing.Calls().ShouldContain("Get tpl_sharing00001");
    }

    // The template's page is open to a signed-out caller: the service decides, and sees that nobody is signed in.
    [Test]
    public async Task GetPublic_WhenSignedOut_ShouldMatchTheScreenShape()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/templates/tpl_public000001/public");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, TemplateScreenShapes.PUBLIC_FILE, TemplateScreenShapes.Public);
        var tokens = view.GetProperty("look").GetProperty("palette").GetProperty("tokens");
        tokens.GetProperty("accent-2").GetString().ShouldBe(tokens.GetProperty("accent").GetString());
        Sharing.Calls().ShouldContain("Public tpl_public000001 signedOut");
    }

    [Test]
    public async Task GetPublic_WhenSignedIn_ShouldTellTheServiceWhoAsks()
    {
        var response = await Client.GetAsync("/api/templates/tpl_public000002/public");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Sharing.Calls().ShouldContain("Public tpl_public000002 signedIn");
    }

    [Test]
    public async Task GetLayoutReview_WhenSignedIn_ShouldMatchTheScreenShape()
    {
        var response = await Client.GetAsync("/api/templates/tpl_review000001/layouts/stage-ladder/review");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, TemplateScreenShapes.LAYOUT_REVIEW_FILE, TemplateScreenShapes.LayoutReview);
        view.GetProperty("layout").GetProperty("slots").EnumerateObject().Count().ShouldBe(3);
        Publishing.Calls().ShouldContain("LayoutReview tpl_review000001 stage-ladder");
    }

    [TestCase("/api/templates")]
    [TestCase("/api/templates/tpl_signedout001")]
    [TestCase("/api/templates/tpl_signedout001/checklist")]
    [TestCase("/api/templates/tpl_signedout001/sharing")]
    [TestCase("/api/templates/tpl_signedout001/layouts/stage-ladder/review")]
    public async Task Get_WhenSignedOut_ShouldBe401(string path)
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Calls().ShouldNotContain(call => call.Contains("tpl_signedout001"));
    }

    [TestCase("/api/templates")]
    [TestCase("/api/templates/tpl_pending00001")]
    [TestCase("/api/templates/tpl_pending00001/checklist")]
    [TestCase("/api/templates/tpl_pending00001/sharing")]
    [TestCase("/api/templates/tpl_pending00001/layouts/stage-ladder/review")]
    public async Task Get_WhenPending_ShouldBe403(string path)
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
        Calls().ShouldNotContain(call => call.Contains("tpl_pending00001"));
    }

    [Test]
    public async Task Create_WhenCalled_ShouldAnswer201WithTheId()
    {
        var response = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "workshop", name = "Routes create" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString().ShouldBe(Drafts.Created.Id);
        Drafts.Calls().ShouldContain("Create workshop Routes create");
    }

    [Test]
    public async Task Update_WhenCalled_ShouldHandTheManifestAndRevisionToTheService()
    {
        var response = await Client.PutAsJsonAsync("/api/templates/tpl_update000001", new { manifest = new { name = "routes-update" }, revision = 7 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(await response.Content.ReadFromJsonAsync<JsonElement>(), TemplateScreenShapes.EDITOR_FILE, TemplateScreenShapes.Editor);
        Drafts.Calls().ShouldContain("Update tpl_update000001 7");
        Drafts.LastManifestJson.ShouldBe("""{"name":"routes-update"}""");
    }

    [Test]
    public async Task Publish_WhenCalled_ShouldAnswerVersionAndDecksBehind()
    {
        var response = await Client.PostAsync("/api/templates/tpl_publish00001/publish", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"version":3,"decksBehind":2}""");
        Publishing.Calls().ShouldContain("Publish tpl_publish00001");
    }

    [Test]
    public async Task Archive_WhenCalled_ShouldAnswer204()
    {
        var response = await Client.PostAsync("/api/templates/tpl_archive00001/archive", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Publishing.Calls().ShouldContain("Archive tpl_archive00001");
    }

    [Test]
    public async Task Approve_WhenCalled_ShouldHandTheAssetAndFindingsToTheService()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/templates/tpl_approve00001/layouts/stage-ladder/approve",
            new { asset = "0a1b2c", overflow = new[] { "headline" }, contrast = new[] { "muted on chip", "ink on card" } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Publishing.Calls().ShouldContain("ApproveLayout tpl_approve00001 stage-ladder 0a1b2c overflow=headline contrast=muted on chip,ink on card");
    }

    // An agent's token works on /mcp only: the route answers 401 and the service is never called.
    [Test]
    public async Task Approve_WhenSentWithAnAgentsBearerToken_ShouldBe401()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/templates/tpl_agent0000001/layouts/stage-ladder/approve")
        {
            Content = JsonContent.Create(new { asset = "0a1b2c", overflow = Array.Empty<string>(), contrast = Array.Empty<string>() }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.Tokens.AccessToken);
        request.Headers.Add("Origin", TestAppFactory.PUBLIC_URL);

        var response = await http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Publishing.Calls().ShouldNotContain(call => call.Contains("tpl_agent0000001"));
    }

    [Test]
    public async Task PutSharing_WhenCalled_ShouldHandTheLevelAndViewersToTheService()
    {
        var response = await Client.PutAsJsonAsync("/api/templates/tpl_setshare0001/sharing", new { level = "named", viewers = new[] { "alex@team.example" } });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(await response.Content.ReadFromJsonAsync<JsonElement>(), TemplateScreenShapes.SHARING_FILE, TemplateScreenShapes.Sharing);
        Sharing.Calls().ShouldContain("Set tpl_setshare0001 Named alex@team.example");
    }

    [Test]
    public async Task Use_WhenSignedIn_ShouldAnswer204()
    {
        var response = await Client.PostAsync("/api/templates/tpl_use000000001/use", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Sharing.Calls().ShouldContain("Use tpl_use000000001");
    }

    [Test]
    public async Task Use_WhenSignedOut_ShouldBe401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsync("/api/templates/tpl_use000000002/use", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Sharing.Calls().ShouldNotContain("Use tpl_use000000002");
    }

    // The stubs registered until the real services land read the sample files themselves, so the app runs in the final
    // shape, and a change to a sample changes the stub's answer with it.
    [Test]
    public async Task Stubs_WhenAsked_ShouldMatchTheScreenShapes()
    {
        using var scope = Factory.Services.CreateScope();
        var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();

        var checklist = await new StubTemplatePublishing().ChecklistAsync(actor, "tpl_stub00000001");
        var review = await new StubTemplatePublishing().LayoutReviewAsync(actor, "tpl_stub00000001", "stage-ladder");
        var sharing = await new StubTemplateSharing().GetAsync(actor, "tpl_stub00000001");
        var page = await new StubTemplateSharing().PublicAsync(actor, "tpl_stub00000001");

        JsonShape.AssertMatches(Wire(checklist), TemplateScreenShapes.ChecklistExample(), "template-editor.json's checklist", TemplateScreenShapes.Checklist);
        JsonShape.AssertMatches(Wire(review), TemplateScreenShapes.LAYOUT_REVIEW_FILE, TemplateScreenShapes.LayoutReview);
        JsonShape.AssertMatches(Wire(sharing), TemplateScreenShapes.SHARING_FILE, TemplateScreenShapes.Sharing);
        JsonShape.AssertMatches(Wire(page), TemplateScreenShapes.PUBLIC_FILE, TemplateScreenShapes.Public);
        checklist.Checklist.ShouldNotBeEmpty();
    }

    // The owner is a name only on a template shared with the caller: null on the caller's own and on a built-in.
    [Test]
    public async Task GetTemplates_WhenSignedIn_ShouldNameTheOwnerOnSharedRowsOnly()
    {
        var response = await Client.GetAsync("/api/templates");

        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        view.GetProperty("mine").EnumerateArray().Select(row => row.GetProperty("owner").ValueKind).ShouldBe([JsonValueKind.Null, JsonValueKind.Null]);
        view.GetProperty("builtIn").EnumerateArray().Select(row => row.GetProperty("owner").ValueKind).ShouldBe([JsonValueKind.Null]);
        view.GetProperty("shared").EnumerateArray().Select(row => row.GetProperty("owner").GetString()).ShouldBe(["Mira Okafor"]);
    }

    // An asset's meta is opaque to the shape check, so its members by kind are asserted here: a font's measured width,
    // nothing for an SVG, and the pixel size of a PNG.
    [Test]
    public async Task GetTemplate_WhenSignedIn_ShouldCarryEachAssetsMetaByKind()
    {
        var response = await Client.GetAsync("/api/templates/tpl_meta00000001");

        var assets = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assets").EnumerateArray().ToList();
        var fonts = assets.Where(asset => asset.GetProperty("kind").GetString() == "font").ToList();
        fonts.Count.ShouldBe(4);
        fonts.Select(font => MetaOf(font)).ShouldAllBe(meta => meta == "avgCharEm:Number");
        assets.Where(asset => asset.GetProperty("contentType").GetString() == "image/svg+xml").Select(MetaOf).ShouldBe(["", ""]);
        var art = assets.Single(asset => asset.GetProperty("kind").GetString() == "art");
        art.GetProperty("contentType").GetString().ShouldBe("image/png");
        MetaOf(art).ShouldBe("width:Number,height:Number");
        art.GetProperty("meta").GetProperty("width").TryGetInt32(out _).ShouldBeTrue();
        art.GetProperty("meta").GetProperty("height").TryGetInt32(out _).ShouldBeTrue();
    }

    // The sample tells one story: the font rights are still to confirm, so no font is confirmed and it can't be published.
    [Test]
    public async Task GetTemplate_WhenSignedIn_ShouldAgreeWithItsOwnChecklistOnFontRights()
    {
        var response = await Client.GetAsync("/api/templates/tpl_rights000001");

        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rights = view.GetProperty("checklist").EnumerateArray().Single(row => row.GetProperty("key").GetString() == "fontRights");
        rights.GetProperty("state").GetString().ShouldBe("todo");
        view.GetProperty("assets").EnumerateArray().Where(asset => asset.GetProperty("kind").GetString() == "font")
            .Select(font => font.GetProperty("rightsConfirmed").GetBoolean()).ShouldBe([false, false, false, false]);
        view.GetProperty("canPublish").GetBoolean().ShouldBeFalse();
    }

    private IEnumerable<string> Calls() => Drafts.Calls().Concat(Publishing.Calls()).Concat(Sharing.Calls());

    // A view as the routes write it.
    private static JsonElement Wire<T>(T view) => JsonSerializer.SerializeToElement(view, WIRE);

    // An asset's meta as "name:kind" pairs, in order.
    private static string MetaOf(JsonElement asset)
        => string.Join(",", asset.GetProperty("meta").EnumerateObject().Select(member => $"{member.Name}:{member.Value.ValueKind}"));
}
