using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// What one template may hold (T6). Its files are never removed, so every stored row counts against a file limit and a
// byte limit; and its upload starts are cleaned up by the next start and capped while they are open. The storage
// limits are reached with rows from TemplateStorageSeed, not with 240 MB of uploads.
[TestFixture]
public class TemplateAssetLimitsTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";
    private const string STORAGE_FULL_TEXT = "This template has stored too many files. Start a new template, or ask for unused files to be removed.";
    private const string TOO_MANY_UPLOADS_TEXT = "Too many uploads are in progress for this template. Finish or wait for them to expire.";
    private const int LIVE_STARTS = 20;

    [Test]
    public async Task Upload_WhenTheTemplateIsAtItsFileLimit_ShouldStoreNothing()
    {
        var template = await DraftAsync();
        await TemplateStorageSeed.FillAsync(Factory, template, TemplateLimits.MAX_STORED_ASSETS - 1, 10);
        using var last = await PutAsync(await StartAsync(template, "logo", "last.svg"), Svg(1));
        var url = await StartAsync(template, "font", "inter.woff2");

        using var put = await PutAsync(url, FontFixtures.Normal());
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        using var again = await PutAsync(url, FontFixtures.Normal());

        last.StatusCode.ShouldBe(HttpStatusCode.OK, await last.Content.ReadAsStringAsync());
        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.TEMPLATE_STORAGE_FULL);
        body.GetProperty("error").GetString().ShouldBe("template_storage_full");
        body.GetProperty("message").GetString().ShouldBe(STORAGE_FULL_TEXT);
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.UPLOAD_USED);
        var assets = await AssetsAsync(template);
        assets.Count.ShouldBe(TemplateLimits.MAX_STORED_ASSETS);
        assets.ShouldNotContain(a => a.Kind == TemplateAssetKind.Font);
        FilesOf(template).ShouldHaveSingleItem().ShouldBe(Sha256Of(Svg(1))[..32]);
        TempFiles().ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenTheTemplateIsAtItsByteLimit_ShouldStoreNothing()
    {
        var template = await DraftAsync();
        var font = FontFixtures.Normal();
        var logo = Svg(2);
        logo.Length.ShouldBeLessThan(font.Length);
        // One byte too little room for the font; the logo, which is smaller, still fits.
        await TemplateStorageSeed.FillAsync(Factory, template, 1, TemplateLimits.MAX_STORED_BYTES - font.Length + 1);

        using var put = await PutAsync(await StartAsync(template, "font", "inter.woff2"), font);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        var afterTheFont = await AssetsAsync(template);
        var filesAfterTheFont = FilesOf(template);
        using var smaller = await PutAsync(await StartAsync(template, "logo", "mark.svg"), logo);

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("template_storage_full");
        body.GetProperty("message").GetString().ShouldBe(STORAGE_FULL_TEXT);
        afterTheFont.ShouldHaveSingleItem().Kind.ShouldBe(TemplateAssetKind.Art);
        filesAfterTheFont.ShouldBeEmpty();
        TempFiles().ShouldBeEmpty();
        smaller.StatusCode.ShouldBe(HttpStatusCode.OK, await smaller.Content.ReadAsStringAsync());
        (await AssetsAsync(template)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Upload_WhenAtTheLimitAndTheBytesAreAlreadyStored_ShouldAnswerTheExistingAsset()
    {
        var template = await DraftAsync();
        var font = FontFixtures.Normal();
        using var first = await PutAsync(await StartAsync(template, "font", "inter.woff2"), font);
        // Full by both limits: every place is taken and no byte is left.
        await TemplateStorageSeed.FillAsync(Factory, template, TemplateLimits.MAX_STORED_ASSETS - 2, 10);
        await TemplateStorageSeed.FillAsync(Factory, template, 1, TemplateLimits.MAX_STORED_BYTES);

        using var repeat = await PutAsync(await StartAsync(template, "font", "inter again.woff2"), font);
        var body = await repeat.Content.ReadFromJsonAsync<JsonElement>();
        using var other = await PutAsync(await StartAsync(template, "logo", "mark.svg"), Svg(3));

        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        repeat.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("assetId").GetString().ShouldBe(Sha256Of(font)[..32]);
        body.GetProperty("fileName").GetString().ShouldBe("inter.woff2");
        body.GetProperty("bytes").GetInt64().ShouldBe(font.Length);
        other.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("template_storage_full");
        (await AssetsAsync(template)).Count.ShouldBe(TemplateLimits.MAX_STORED_ASSETS);
        FilesOf(template).ShouldHaveSingleItem().ShouldBe(Sha256Of(font)[..32]);
    }

    [Test]
    public async Task StartUpload_WhenOldStartsHaveExpired_ShouldRemoveThem()
    {
        var template = await DraftAsync();
        var other = await DraftAsync();
        var usedUrl = await StartAsync(template, "logo", "mark.svg");
        using var used = await PutAsync(usedUrl, Svg(4));
        await StartAsync(other, "font", "theirs.woff2");

        await StartAsync(template, "font", "one.woff2");
        var afterAUsedOne = await UploadsAsync(template);
        using var usedAgain = await PutAsync(usedUrl, Svg(4));
        await StartAsync(template, "font", "two.woff2");
        Clock.Advance(TimeSpan.FromSeconds(601));
        await StartAsync(template, "font", "three.woff2");

        used.StatusCode.ShouldBe(HttpStatusCode.OK, await used.Content.ReadAsStringAsync());
        afterAUsedOne.ShouldHaveSingleItem().FileName.ShouldBe("one.woff2");
        usedAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await UploadsAsync(template)).ShouldHaveSingleItem().FileName.ShouldBe("three.woff2");
        // Another template's starts are not this start's to remove, expired or not.
        (await UploadsAsync(other)).ShouldHaveSingleItem().FileName.ShouldBe("theirs.woff2");
    }

    [Test]
    public async Task StartUpload_WhenTwentyAreLive_ShouldRefuse()
    {
        var template = await DraftAsync();
        var other = await DraftAsync();
        for (var i = 0; i < LIVE_STARTS; i++)
        {
            await StartAsync(template, "font", $"font-{i}.woff2");
        }

        using var refused = await PostStartAsync(template, "font", "one-more.woff2");
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        var whileFull = await UploadsAsync(template);
        using var elsewhere = await PostStartAsync(other, "font", "elsewhere.woff2");
        Clock.Advance(TimeSpan.FromSeconds(601));
        using var later = await PostStartAsync(template, "font", "later.woff2");

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(TemplateErrorCodes.TOO_MANY_UPLOADS);
        body.GetProperty("error").GetString().ShouldBe("too_many_uploads");
        body.GetProperty("message").GetString().ShouldBe(TOO_MANY_UPLOADS_TEXT);
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
        whileFull.Count.ShouldBe(LIVE_STARTS);
        whileFull.ShouldNotContain(u => u.FileName == "one-more.woff2");
        elsewhere.StatusCode.ShouldBe(HttpStatusCode.Created);
        later.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await UploadsAsync(template)).ShouldHaveSingleItem().FileName.ShouldBe("later.woff2");
    }

    private TemplateAssetStore Store => Factory.Services.GetRequiredService<TemplateAssetStore>();

    private Task<string> DraftAsync() => TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

    private Task<HttpResponseMessage> PostStartAsync(string template, string kind, string fileName)
        => Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind, fileName, rightsConfirmed = false });

    // Starts an upload as the owner and answers its upload URL.
    private async Task<string> StartAsync(string template, string kind, string fileName)
    {
        using var response = await PostStartAsync(template, kind, fileName);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
    }

    // PUTs the bytes as an agent or a script would: no sign-in, no Origin.
    private async Task<HttpResponseMessage> PutAsync(string uploadUrl, byte[] bytes)
    {
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(bytes);
        return await http.PutAsync(uploadUrl, content);
    }

    private async Task<IReadOnlyList<TemplateAsset>> AssetsAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateAssets.AsNoTracking().Where(a => a.TemplateId == template).ToListAsync();
    }

    private async Task<IReadOnlyList<TemplateUpload>> UploadsAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateUploads.AsNoTracking().Where(u => u.TemplateId == template).ToListAsync();
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
        => Encoding.UTF8.GetBytes($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}"><rect width="{size}" height="{size}"/></svg>""");
}
