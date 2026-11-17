using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The bundle routes over FakeTemplateBundles: the wire shape of import and export, that the posted bytes and the caller
// reach the seam, and who is turned away before it. The app, and so the fake, is shared by the fixture: it is reset after
// each test.
[TestFixture]
public class TemplateBundleRoutesTests : ApiTestFixture
{
    private const string ZIP = "application/zip";
    private const string TEMPLATE = "tpl_northwind0001";

    private static readonly byte[] BUNDLE = [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5];

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplateBundles.Register;

    private FakeTemplateBundles Bundles => Factory.Services.GetRequiredService<FakeTemplateBundles>();

    [TearDown]
    public void ResetTheFake() => Bundles.Reset();

    [Test]
    public async Task Import_WhenAZipIsPosted_ShouldAnswer201WithAnId()
    {
        Bundles.AnswerImport("tpl_imported00001");

        var response = await Client.PostAsync("/api/templates/import", Zip(BUNDLE));
        var body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["id"]);
        body.GetProperty("id").GetString().ShouldBe("tpl_imported00001");
        Bundles.Imports().ShouldHaveSingleItem().ShouldBe(BUNDLE);
        Bundles.Calls().ShouldBe([$"Import {TestUser.Sam.Email}"]);
    }

    [Test]
    public async Task Import_WhenSignedOut_ShouldBe401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsync("/api/templates/import", Zip(BUNDLE));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Bundles.Calls().ShouldBeEmpty();
    }

    [Test]
    public async Task Import_WhenPending_ShouldBe403()
    {
        using var pat = ClientFor(TestUser.NewPending());

        var response = await pat.PostAsync("/api/templates/import", Zip(BUNDLE));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Bundles.Calls().ShouldBeEmpty();
    }

    [Test]
    public async Task Import_WhenTheSeamRefuses_ShouldAnswerTheMappedStatus()
    {
        Bundles.Refuse(new BadRequestException("That file is not a template bundle."));

        var response = await Client.PostAsync("/api/templates/import", Zip(BUNDLE));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Bundles.Calls().ShouldBeEmpty();
    }

    [Test]
    public async Task Export_WhenAsked_ShouldAnswerAZipAttachment()
    {
        Bundles.AnswerExport(BUNDLE, "northwind.zip");

        var response = await Client.GetAsync($"/api/templates/{TEMPLATE}/bundle");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ZIP);
        var disposition = response.Content.Headers.ContentDisposition.ShouldNotBeNull();
        disposition.DispositionType.ShouldBe("attachment");
        disposition.FileName?.Trim('"').ShouldBe("northwind.zip");
        bytes.ShouldBe(BUNDLE);
        Bundles.Calls().ShouldBe([$"Export {TEMPLATE} {TestUser.Sam.Email}"]);
    }

    [Test]
    public async Task Export_WhenSignedOut_ShouldBe401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/templates/{TEMPLATE}/bundle");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Bundles.Calls().ShouldBeEmpty();
    }

    [Test]
    public async Task Export_WhenTheSeamRefuses_ShouldAnswerTheMappedStatus()
    {
        Bundles.Refuse(new NotFoundException("Template not found"));

        var response = await Client.GetAsync($"/api/templates/{TEMPLATE}/bundle");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    private static ByteArrayContent Zip(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(ZIP);
        return content;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
