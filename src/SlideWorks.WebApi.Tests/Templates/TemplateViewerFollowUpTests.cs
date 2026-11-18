using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Two small server follow-ups from the SW-426 review: "Use this template" on a template with nothing published is the
// 404 an unknown one gets and writes no viewer row, and a named viewer stored in mixed case still reads the template's
// files and is rewritten in lower case when a sharing change keeps them.
[TestFixture]
public class TemplateViewerFollowUpTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";

    [Test]
    public async Task Use_WhenALinkTemplateHasOnlyADraft_ShouldBeNotFoundAndWriteNothing()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        using var share = await Client.PutAsJsonAsync($"/api/templates/{template}/sharing", new { level = "link" });
        share.StatusCode.ShouldBe(HttpStatusCode.OK, await share.Content.ReadAsStringAsync());
        using var alex = ClientFor(TestUser.Alex);

        using var response = await alex.PostAsync($"/api/templates/{template}/use", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ViewersAsync(template)).ShouldBeEmpty();
    }

    [Test]
    public async Task Assets_WhenAViewerRowIsStoredInMixedCase_ShouldStillServeThem()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var font = await UploadFontAsync(template);
        using var share = await Client.PutAsJsonAsync($"/api/templates/{template}/sharing", new { level = "named", viewers = Array.Empty<string>() });
        share.StatusCode.ShouldBe(HttpStatusCode.OK, await share.Content.ReadAsStringAsync());
        await TemplateRowSeed.ViewerAsync(Factory, template, "Alex@TEAM.example");
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        using var named = await alex.GetAsync($"/api/templates/{template}/assets/{font}");
        using var other = await mira.GetAsync($"/api/templates/{template}/assets/{font}");

        TestUser.Alex.Email.ShouldBe("alex@team.example");
        named.StatusCode.ShouldBe(HttpStatusCode.OK);
        other.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Share_WhenAKeptViewerRowIsMixedCase_ShouldRewriteItInLowerCase()
    {
        var template = await PublishedAsync();
        await TemplateRowSeed.ViewerAsync(Factory, template, "Alex@TEAM.example");

        using var put = await Client.PutAsJsonAsync($"/api/templates/{template}/sharing", new { level = "named", viewers = new[] { "alex@team.example" } });

        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        (await ViewersAsync(template)).ShouldBe(["alex@team.example"]);
    }

    private async Task<string> PublishedAsync()
        => await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, MANIFEST);

    private async Task<List<string>> ViewersAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateViewers.AsNoTracking()
            .Where(v => v.TemplateId == template).Select(v => v.Email).ToListAsync();
    }

    private async Task<string> UploadFontAsync(string template)
    {
        using var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "font", fileName = "inter.woff2", rightsConfirmed = false });
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(FontFixtures.Normal());
        using var put = await http.PutAsync(url, content);
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetId").GetString()!;
    }
}
