using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// A template's files over HTTP (T6): starting an upload, the one-time upload URL, reading a file and confirming font
// rights. Fonts and logos go through the app's own checks; art and layouts go through a RefusingAssetCheck each, so a
// test can make the art check refuse and can store a layout, which the app's own layout check never lets in. Every
// font is FontFixtures.Normal() and every logo or art file a real SVG or PNG, so the tests hold with any real check.
[TestFixture]
public class TemplateAssetRoutesTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";
    private const string SVG_NAMESPACE = "http://www.w3.org/2000/svg";

    private readonly RefusingAssetCheck _art = new(TemplateAssetKind.Art);
    private readonly RefusingAssetCheck _layout = new(TemplateAssetKind.Layout);

    protected override Action<IServiceCollection>? ConfigureTestServices
        => services => services.AddSingleton<IAssetCheck>(_art).AddSingleton<IAssetCheck>(_layout);

    [TearDown]
    public void AcceptAgain()
    {
        _art.Accept();
        _layout.Accept();
    }

    [Test]
    public async Task Upload_WhenNoCookieAndNoOrigin_ShouldBeAccepted()
    {
        var template = await DraftAsync();
        var font = FontFixtures.Normal();
        var url = await StartAsync(template, "font", "Inter Regular.woff2");

        using var put = await PutAsync(url, font);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["assetId", "kind", "fileName", "contentType", "bytes", "meta"]);
        body.GetProperty("assetId").GetString().ShouldBe(Sha256Of(font)[..32]);
        body.GetProperty("kind").GetString().ShouldBe("font");
        body.GetProperty("fileName").GetString().ShouldBe("Inter Regular.woff2");
        body.GetProperty("contentType").GetString().ShouldBe("font/woff2");
        body.GetProperty("bytes").GetInt64().ShouldBe(font.Length);
        body.GetProperty("meta").ValueKind.ShouldBe(JsonValueKind.Object);
        var asset = (await AssetsAsync(template)).ShouldHaveSingleItem();
        asset.Sha256.ShouldBe(Sha256Of(font));
        asset.RightsConfirmed.ShouldBeFalse();
        (await File.ReadAllBytesAsync(Store.PathFor(template, asset.Id))).ShouldBe(font);
    }

    [Test]
    public async Task Start_WhenTheOwnerAsks_ShouldAnswerAOneTimeUrlAndWhenItCloses()
    {
        var template = await DraftAsync();

        var response = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "logo", fileName = "mark.svg", rightsConfirmed = false });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Created, body.ToString());
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["uploadUrl", "expiresLabel"]);
        body.GetProperty("uploadUrl").GetString().ShouldNotBeNull().ShouldMatch($"^{TestAppFactory.PUBLIC_URL}/api/template-uploads/[A-Za-z0-9_-]{{43}}$");
        body.GetProperty("expiresLabel").GetString().ShouldBe("in 10 minutes");
    }

    [TestCase("""{"kind":"video","fileName":"a.mp4","rightsConfirmed":false}""")]
    [TestCase("""{"kind":2,"fileName":"a.svg","rightsConfirmed":false}""")]
    [TestCase("""{"fileName":"a.svg","rightsConfirmed":false}""")]
    [TestCase("""{"kind":"logo","fileName":"a.svg"}""")]
    [TestCase("""{"kind":"logo","fileName":"  ","rightsConfirmed":false}""")]
    [TestCase("""{"kind":"logo","rightsConfirmed":false}""")]
    [TestCase("""{"kind":"logo","fileName":"a\u0000b.svg","rightsConfirmed":false}""")]
    public async Task Start_WhenTheBodyIsNotAnUpload_ShouldBe400(string json)
    {
        var template = await DraftAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync($"/api/templates/{template}/assets", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadCountAsync(template)).ShouldBe(0);
    }

    [Test]
    public async Task Start_WhenTheFileNameIsTooLong_ShouldBe400()
    {
        var template = await DraftAsync();

        var response = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "logo", fileName = new string('a', 121), rightsConfirmed = false });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadCountAsync(template)).ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenTheUrlIsUsedTwice_ShouldRefuseTheSecond()
    {
        var template = await DraftAsync();
        var url = await StartAsync(template, "logo", "mark.svg");
        using var first = await PutAsync(url, Svg(10));

        using var second = await PutAsync(url, Svg(20));
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.UPLOAD_USED);
        body.GetProperty("message").GetString().ShouldBe("This upload has already been used. Upload the file again.");
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
        (await AssetsAsync(template)).ShouldHaveSingleItem().Sha256.ShouldBe(Sha256Of(Svg(10)));
    }

    [Test]
    public async Task Upload_WhenOverTheSizeLimit_ShouldStoreNothing()
    {
        var template = await DraftAsync();
        var url = await StartAsync(template, "font", "huge.woff2");
        var tooBig = new byte[(2 * 1024 * 1024) + 1];
        FontFixtures.Normal().CopyTo(tooBig, 0);

        using var put = await PutAsync(url, tooBig);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        using var again = await PutAsync(url, FontFixtures.Normal());

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.FILE_TOO_LARGE);
        body.GetProperty("message").GetString().ShouldBe("This file is too large. The limit is 2 MB.");
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.UPLOAD_USED);
        (await AssetsAsync(template)).ShouldBeEmpty();
        FilesOf(template).ShouldBeEmpty();
        TempFiles().ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenTheSameBytesTwice_ShouldStoreOnce()
    {
        var template = await DraftAsync();
        var font = FontFixtures.Normal();

        using var first = await PutAsync(await StartAsync(template, "font", "first.woff2"), font);
        using var second = await PutAsync(await StartAsync(template, "font", "second.woff2", rightsConfirmed: true), font);
        using var third = await PutAsync(await StartAsync(template, "font", "third.woff2"), font);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var answered = await second.Content.ReadFromJsonAsync<JsonElement>();
        answered.GetProperty("assetId").GetString().ShouldBe(Sha256Of(font)[..32]);
        answered.GetProperty("fileName").GetString().ShouldBe("first.woff2");
        third.StatusCode.ShouldBe(HttpStatusCode.OK);
        var asset = (await AssetsAsync(template)).ShouldHaveSingleItem();
        asset.FileName.ShouldBe("first.woff2");
        asset.RightsConfirmed.ShouldBeTrue("the second upload turned it on and the third must not turn it off");
        FilesOf(template).ShouldHaveSingleItem().ShouldBe(asset.Id);
    }

    [Test]
    public async Task Upload_WhenTheSameBytesTwice_ShouldKeepTheFirstMeta()
    {
        var template = await DraftAsync();
        _art.Accept("image/png", """{"width":1,"height":1}""");
        using var first = await PutAsync(await StartAsync(template, "art", "dot.png"), Png());
        _art.Accept("image/webp", """{"width":2,"height":2}""");

        using var second = await PutAsync(await StartAsync(template, "art", "dot-again.png"), Png());
        var answered = await second.Content.ReadFromJsonAsync<JsonElement>();

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        answered.GetProperty("meta").GetProperty("width").GetInt32().ShouldBe(1);
        answered.GetProperty("contentType").GetString().ShouldBe("image/png");
        var asset = (await AssetsAsync(template)).ShouldHaveSingleItem();
        asset.MetaJson.ShouldBe("""{"width":1,"height":1}""");
        asset.ContentType.ShouldBe("image/png");
    }

    [Test]
    public async Task Upload_WhenTheCheckRefuses_ShouldStoreNothing()
    {
        var template = await DraftAsync();
        var url = await StartAsync(template, "art", "title.png");
        _art.Refuse(TemplateErrorCodes.NOT_AN_IMAGE);

        using var put = await PutAsync(url, Png());
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        _art.Accept();
        using var again = await PutAsync(url, Png());

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.NOT_AN_IMAGE);
        body.GetProperty("message").GetString().ShouldBe("Logos and art must be SVG, PNG or WebP files.");
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
        _art.LastLength().ShouldBe(Png().Length);
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.UPLOAD_USED);
        (await AssetsAsync(template)).ShouldBeEmpty();
        FilesOf(template).ShouldBeEmpty();
        TempFiles().ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenTheCheckGivesErrors_ShouldCarryThemInTheBody()
    {
        var template = await DraftAsync();
        var url = await StartAsync(template, "art", "title.svg");
        _art.Refuse(
            TemplateErrorCodes.MANIFEST_INVALID,
            [new TemplateRefusalDetail("markup", "svg_script", "A script."), new TemplateRefusalDetail("css", "svg_external_reference", "A link.")],
            ("reason", "two problems"));

        using var put = await PutAsync(url, Svg(10));
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.MANIFEST_INVALID);
        body.GetProperty("message").GetString().ShouldBe("The template manifest is not valid: two problems");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Select(e => e.GetProperty("path").GetString()).ShouldBe(["markup", "css"]);
        errors.Select(e => e.GetProperty("code").GetString()).ShouldBe(["svg_script", "svg_external_reference"]);
        errors.Select(e => e.GetProperty("message").GetString()).ShouldBe(["A script.", "A link."]);
        (await AssetsAsync(template)).ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenNotTheOwner_ShouldBeNotFound()
    {
        var template = await DraftAsync();
        using var alex = ClientFor(TestUser.Alex);
        using var anonymous = ClientFor(TestUser.Anonymous);
        var request = new { kind = "font", fileName = "inter.woff2", rightsConfirmed = true };

        var asAlex = await alex.PostAsJsonAsync($"/api/templates/{template}/assets", request);
        var unknown = await Client.PostAsJsonAsync("/api/templates/tpl_nosuchthing1/assets", request);
        var signedOut = await anonymous.PostAsJsonAsync($"/api/templates/{template}/assets", request);

        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await asAlex.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Template not found");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.Content.ReadAsStringAsync()).ShouldBe(await asAlex.Content.ReadAsStringAsync());
        signedOut.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await UploadCountAsync(template)).ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenTheTokenHasExpired_ShouldRefuse()
    {
        var template = await DraftAsync();
        var url = await StartAsync(template, "font", "inter.woff2");
        Clock.Advance(TimeSpan.FromSeconds(601));

        using var put = await PutAsync(url, FontFixtures.Normal());

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Upload not found");
        (await AssetsAsync(template)).ShouldBeEmpty();
        FilesOf(template).ShouldBeEmpty();
    }

    [TestCase("no-such-token")]
    [TestCase("..%2F..%2Fetc%2Fpasswd")]
    public async Task Upload_WhenTheTokenIsNotOurs_ShouldBe404(string token)
    {
        using var put = await PutAsync($"/api/template-uploads/{token}", FontFixtures.Normal());

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        TempFiles().ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenTheUploadClaimsAType_ShouldServeTheTypeTheCheckGave()
    {
        var template = await DraftAsync();
        var url = await StartAsync(template, "font", "page.html");

        using var put = await PutAsync(url, FontFixtures.Normal(), contentType: "text/html");
        var asset = (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetId").GetString();
        using var served = await Client.GetAsync($"/api/templates/{template}/assets/{asset}");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("font/woff2");
    }

    [Test]
    public async Task Serve_WhenTheOwnerAsks_ShouldSendTheFileLockedDown()
    {
        var template = await DraftAsync();
        var logo = await UploadAsync(template, "logo", "mark.svg", Svg(10));

        using var served = await Client.GetAsync($"/api/templates/{template}/assets/{logo}");

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(Svg(10));
        served.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        served.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; sandbox"]);
        var cache = served.Headers.CacheControl.ShouldNotBeNull();
        cache.Private.ShouldBeTrue();
        cache.Extensions.ShouldContain(extension => extension.Name == "immutable");
        served.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBeOneOf("image/svg+xml", "application/octet-stream");
        served.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{logo}\"");
    }

    [Test]
    public async Task Serve_WhenTheAssetIsALayout_ShouldBe404()
    {
        var template = await DraftAsync();
        var layout = await UploadAsync(template, "layout", "ladder.json", Encoding.UTF8.GetBytes("""{"name":"ladder"}"""));

        using var served = await Client.GetAsync($"/api/templates/{template}/assets/{layout}");

        served.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AssetsAsync(template)).ShouldHaveSingleItem().Kind.ShouldBe(TemplateAssetKind.Layout);
        FilesOf(template).ShouldBe([layout]);
    }

    [Test]
    public async Task Serve_WhenAStranger_ShouldBe404()
    {
        var template = await DraftAsync();
        var font = await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        using var alex = ClientFor(TestUser.Alex);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var asAlex = await alex.GetAsync($"/api/templates/{template}/assets/{font}");
        using var signedOut = await anonymous.GetAsync($"/api/templates/{template}/assets/{font}");
        using var unknown = await Client.GetAsync($"/api/templates/{template}/assets/{new string('0', 32)}");
        using var asOwner = await Client.GetAsync($"/api/templates/{template}/assets/{font}");

        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        signedOut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await asAlex.Content.ReadAsStringAsync()).ShouldBe(await unknown.Content.ReadAsStringAsync());
        asOwner.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("tpl_nosuchthing1", "00000000000000000000000000000000")]
    [TestCase("..", "00000000000000000000000000000000")]
    [TestCase("tpl_..%2F..%2Fimages", "00000000000000000000000000000000")]
    [TestCase("TEMPLATE", "..%2F..%2F..%2Fetc%2Fpasswd")]
    [TestCase("TEMPLATE", ".uploads")]
    [TestCase("TEMPLATE", "%252e%252e")]
    public async Task Serve_WhenTheIdsAreNotOurs_ShouldBe404(string template, string asset)
    {
        var real = await DraftAsync();
        await UploadAsync(real, "font", "inter.woff2", FontFixtures.Normal());

        using var served = await Client.GetAsync($"/api/templates/{template.Replace("TEMPLATE", real)}/assets/{asset}");

        served.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Serve_WhenTheTemplateIsSharedWithThePerson_ShouldServe()
    {
        var template = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, MANIFEST, access: DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, template, TestUser.Alex.Email);
        var font = await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        using var asAlex = await alex.GetAsync($"/api/templates/{template}/assets/{font}");
        using var asMira = await mira.GetAsync($"/api/templates/{template}/assets/{font}");

        asAlex.StatusCode.ShouldBe(HttpStatusCode.OK);
        asMira.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Serve_WhenTheDeckUsesTheTemplate_ShouldServeToAViewerWithoutTemplateAccess()
    {
        var template = await DraftAsync();
        var font = await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck, "link");
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var before = await anonymous.GetAsync($"/api/templates/{template}/assets/{font}");

        await PutDeckOnAsync(deck, template);
        using var viewer = await anonymous.GetAsync($"/api/templates/{template}/assets/{font}");
        await SharingApi.PutOkAsync(Client, deck, "private");
        using var after = await anonymous.GetAsync($"/api/templates/{template}/assets/{font}");

        before.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        viewer.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.Content.ReadAsByteArrayAsync()).ShouldBe(FontFixtures.Normal());
        after.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // A headless render page reads the files of its own deck's template with its render token, whoever the deck is
    // shared with; a token for another deck opens nothing.
    [Test]
    public async Task Serve_WhenARenderTokenIsForADeckOnTheTemplate_ShouldServe()
    {
        var template = await DraftAsync();
        var font = await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        var deck = await SharingApi.CreateDeckAsync(Client);
        var other = await SharingApi.CreateDeckAsync(Client, "Another deck");
        await PutDeckOnAsync(deck, template);
        var tokens = Factory.Services.GetRequiredService<IRenderTokens>();

        using var withToken = await GetWithRenderTokenAsync($"/api/templates/{template}/assets/{font}", tokens.Issue(deck, RenderPurpose.Preview));
        using var withAnotherDecks = await GetWithRenderTokenAsync($"/api/templates/{template}/assets/{font}", tokens.Issue(other, RenderPurpose.Preview));
        using var withAForgery = await GetWithRenderTokenAsync($"/api/templates/{template}/assets/{font}", "not-a-token");

        withToken.StatusCode.ShouldBe(HttpStatusCode.OK);
        withAnotherDecks.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        withAForgery.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // A render token opens the files of its own deck's template and no other template's, even one of the same owner.
    [Test]
    public async Task Serve_WhenARenderTokenIsForADeckOnAnotherTemplate_ShouldBe404()
    {
        var templateA = await DraftAsync();
        var templateB = await DraftAsync();
        var fontA = await UploadAsync(templateA, "font", "inter-a.woff2", FontFixtures.Normal());
        var fontB = await UploadAsync(templateB, "font", "wide-b.woff2", FontFixtures.Wide());
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutDeckOnAsync(deck, templateA);
        var token = Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Preview);

        using var own = await GetWithRenderTokenAsync($"/api/templates/{templateA}/assets/{fontA}", token);
        using var other = await GetWithRenderTokenAsync($"/api/templates/{templateB}/assets/{fontB}", token);
        using var crossed = await GetWithRenderTokenAsync($"/api/templates/{templateA}/assets/{fontB}", token);
        using var unknown = await GetWithRenderTokenAsync($"/api/templates/{templateB}/assets/{new string('0', 32)}", token);

        fontB.ShouldNotBe(fontA);
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        other.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        crossed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.Content.ReadAsStringAsync()).ShouldBe(await unknown.Content.ReadAsStringAsync());
    }

    // The page's own path: it trades the header for the sw_render cookie, and a browser sends only the cookie after that.
    [Test]
    public async Task Serve_WhenTheRenderCookieIsSent_ShouldServeOnlyTheDecksTemplate()
    {
        var templateA = await DraftAsync();
        var templateB = await DraftAsync();
        var fontA = await UploadAsync(templateA, "font", "inter-a.woff2", FontFixtures.Normal());
        var fontB = await UploadAsync(templateB, "font", "wide-b.woff2", FontFixtures.Wide());
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutDeckOnAsync(deck, templateA);
        var token = Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Preview);
        using var browser = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var before = await browser.GetAsync($"/api/templates/{templateA}/assets/{fontA}");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/api/render/{deck}/session");
        start.Headers.Add(RenderSessions.HEADER, token);
        using var started = await browser.SendAsync(start);

        using var withCookie = await browser.GetAsync($"/api/templates/{templateA}/assets/{fontA}");
        using var otherTemplate = await browser.GetAsync($"/api/templates/{templateB}/assets/{fontB}");

        before.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        started.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        started.Headers.GetValues("Set-Cookie").ShouldContain(cookie => cookie.StartsWith("sw_render=", StringComparison.Ordinal) && cookie.Contains("path=/api/templates", StringComparison.OrdinalIgnoreCase));
        withCookie.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await withCookie.Content.ReadAsByteArrayAsync()).ShouldBe(FontFixtures.Normal());
        otherTemplate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ConfirmRights_WhenTheOwnerAsks_ShouldSetEveryFont()
    {
        var template = await DraftAsync();
        var uploaded = await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        var seeded = await TemplateRowSeed.AssetAsync(Factory, template, TemplateAssetKind.Font, "inter-bold.woff2");
        var logo = await TemplateRowSeed.AssetAsync(Factory, template, TemplateAssetKind.Logo, "mark.svg");

        var response = await Client.PostAsync($"/api/templates/{template}/assets/font-rights", content: null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["confirmed"]);
        body.GetProperty("confirmed").GetInt32().ShouldBe(2);
        var assets = await AssetsAsync(template);
        assets.Where(a => a.RightsConfirmed).Select(a => a.Id).ShouldBe([uploaded, seeded], ignoreOrder: true);
        assets.Single(a => a.Id == logo).RightsConfirmed.ShouldBeFalse();
    }

    [Test]
    public async Task ConfirmRights_WhenNotTheOwner_ShouldChangeNothing()
    {
        var template = await DraftAsync();
        await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        using var alex = ClientFor(TestUser.Alex);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var asAlex = await alex.PostAsync($"/api/templates/{template}/assets/font-rights", content: null);
        var signedOut = await anonymous.PostAsync($"/api/templates/{template}/assets/font-rights", content: null);

        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        signedOut.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AssetsAsync(template)).Count(a => a.RightsConfirmed).ShouldBe(0);
    }

    // An agent's token works on /mcp only, and no MCP tool confirms the right to serve a font.
    [Test]
    public async Task ConfirmRights_WhenCalledWithAnAgentToken_ShouldBeRefused()
    {
        var template = await DraftAsync();
        await UploadAsync(template, "font", "inter.woff2", FontFixtures.Normal());
        var before = (await AssetsAsync(template)).Count(a => a.RightsConfirmed);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/templates/{template}/assets/font-rights");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.Tokens.AccessToken);
        request.Headers.Add("Origin", TestAppFactory.PUBLIC_URL);

        var response = await http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AssetsAsync(template)).Count(a => a.RightsConfirmed).ShouldBe(before);
        before.ShouldBe(0);
    }

    private TemplateAssetStore Store => Factory.Services.GetRequiredService<TemplateAssetStore>();

    private Task<string> DraftAsync() => TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

    // Starts an upload as the owner and answers its upload URL.
    private async Task<string> StartAsync(string template, string kind, string fileName, bool rightsConfirmed = false)
    {
        var response = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind, fileName, rightsConfirmed });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
    }

    // PUTs the bytes as an agent or a script would: no sign-in, no Origin.
    private async Task<HttpResponseMessage> PutAsync(string uploadUrl, byte[] bytes, string? contentType = null)
    {
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(bytes);
        if (contentType != null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
        return await http.PutAsync(uploadUrl, content);
    }

    private async Task<HttpResponseMessage> GetWithRenderTokenAsync(string path, string token)
    {
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(RenderSessions.HEADER, token);
        return await http.SendAsync(request);
    }

    // Start plus the PUT; answers the asset's id.
    private async Task<string> UploadAsync(string template, string kind, string fileName, byte[] bytes)
    {
        using var put = await PutAsync(await StartAsync(template, kind, fileName), bytes);
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetId").GetString()!;
    }

    private async Task<IReadOnlyList<TemplateAsset>> AssetsAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateAssets.AsNoTracking().Where(a => a.TemplateId == template).ToListAsync();
    }

    private async Task<int> UploadCountAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateUploads.CountAsync(u => u.TemplateId == template);
    }

    // No route can put a deck on an uploaded template yet, so the deck's row is pointed at it here.
    private async Task PutDeckOnAsync(string deck, string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var moved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks
            .Where(d => d.Id == deck)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.Template, template));
        moved.ShouldBe(1);
    }

    // The names of the files stored for a template.
    private IReadOnlyList<string> FilesOf(string template)
    {
        var folder = Path.GetDirectoryName(Store.PathFor(template, new string('0', 32)))!;
        return Directory.Exists(folder) ? [.. Directory.GetFiles(folder).Select(Path.GetFileName).OfType<string>()] : [];
    }

    // Files still waiting in the store's temp folder.
    private IReadOnlyList<string> TempFiles()
    {
        var folder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Store.PathFor("tpl_x", new string('0', 32)))!)!, ".uploads");
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

    // A real SVG: one rectangle, in the SVG namespace. Its size makes its bytes differ.
    private static byte[] Svg(int size)
        => Encoding.UTF8.GetBytes($"""<svg xmlns="{SVG_NAMESPACE}" viewBox="0 0 {size} {size}"><rect width="{size}" height="{size}"/></svg>""");

    // A real PNG of one pixel.
    private static byte[] Png()
        => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}
