using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// What a template's logo or art is served with (D6): only as an image, with the sandbox policy and nosniff, whatever
// the file is, through the real logo and art checks.
[TestFixture]
public class AssetServeHeaderTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";
    private const string SVG = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 4 4"><rect width="4" height="4"/></svg>""";
    private const string PNG_BASE64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [TestCase("logo")]
    [TestCase("art")]
    public async Task Serve_WhenSvg_ShouldSendTheSandboxPolicy(string kind)
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var asset = await UploadAsync(template, kind, "mark.svg", Encoding.UTF8.GetBytes(SVG));

        using var served = await Client.GetAsync($"/api/templates/{template}/assets/{asset}");

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; sandbox"]);
    }

    [Test]
    public async Task Serve_WhenSvg_ShouldSendNosniffAndTheImageType()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var svg = await UploadAsync(template, "logo", "mark.svg", Encoding.UTF8.GetBytes(SVG));
        var png = await UploadAsync(template, "art", "named-svg.svg", Convert.FromBase64String(PNG_BASE64));

        using var servedSvg = await Client.GetAsync($"/api/templates/{template}/assets/{svg}");
        using var servedPng = await Client.GetAsync($"/api/templates/{template}/assets/{png}");

        servedSvg.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        servedSvg.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("image/svg+xml");
        (await servedSvg.Content.ReadAsStringAsync()).ShouldBe(SVG);
        servedPng.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        servedPng.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; sandbox"]);
        servedPng.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("image/png");
    }

    [Test]
    public async Task Upload_WhenSvgHasAScript_ShouldStoreNothing()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "logo", fileName = "bad.svg", rightsConfirmed = false });
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;

        using var put = await PutAsync(url, Encoding.UTF8.GetBytes(SVG.Replace("<rect", "<script>alert(1)</script><rect", StringComparison.Ordinal)));
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("svg_script");
        body.GetProperty("message").GetString().ShouldBe("This SVG contains a script. Export it again without scripts, or upload a PNG.");
    }

    private async Task<string> UploadAsync(string template, string kind, string fileName, byte[] bytes)
    {
        var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind, fileName, rightsConfirmed = false });
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
        using var put = await PutAsync(url, bytes);
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetId").GetString()!;
    }

    private async Task<HttpResponseMessage> PutAsync(string uploadUrl, byte[] bytes)
    {
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(bytes);
        return await http.PutAsync(uploadUrl, content);
    }
}
