using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Templates;

namespace SlideWorks.WebApi.Tests.RequestLimits;

// K6: the shared 1 MB body limit leaves the template routes to their own, larger caps. One body at each documented maximum:
// a 10 MB logo through the one-time asset upload URL, and a 60 MB bundle through the import route.
[TestFixture]
[CancelAfter(120000)]
public class TemplateBodyLimitTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplateBundles.Register;

    [Test]
    public async Task TemplateAssetUpload_WhenExactlyTenMb_ShouldStillBeStored()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, """{"name":"Northwind"}""");
        using var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "logo", fileName = "big.svg", rightsConfirmed = false });
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(BigSvg(TemplateLimits.MAX_IMAGE_BYTES));

        using var put = await http.PutAsync(url, content);

        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task TemplateBundleImport_WhenExactlySixtyMb_ShouldStillBeAccepted()
    {
        var bundles = Factory.Services.GetRequiredService<FakeTemplateBundles>();
        bundles.AnswerImport("tpl_imported00001");
        var zip = new byte[TemplateLimits.MAX_BUNDLE_BYTES];
        zip[0] = 0x50;
        zip[1] = 0x4B;
        using var content = new ByteArrayContent(zip);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var response = await Client.PostAsync("/api/templates/import", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        bundles.Imports().ShouldHaveSingleItem().Length.ShouldBe(TemplateLimits.MAX_BUNDLE_BYTES);
        bundles.Reset();
    }

    // A valid SVG of exactly this many bytes: a comment of random base64 around one rectangle.
    private static byte[] BigSvg(int bytes)
    {
        var head = Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10"/><!--""");
        var tail = Encoding.UTF8.GetBytes("--></svg>");
        var random = new byte[(bytes - head.Length - tail.Length) / 4 * 3];
        new Random(1).NextBytes(random);
        var filler = Encoding.ASCII.GetBytes(Convert.ToBase64String(random).Replace('-', '+'));
        return [.. head, .. filler, .. tail];
    }
}
