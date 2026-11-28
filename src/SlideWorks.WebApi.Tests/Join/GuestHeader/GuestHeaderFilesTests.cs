using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join.GuestHeader;

// The guest file routes with the app's X-SlideWorks-Guest header instead of the sw_guest cookie (K7): a deck's image and
// its template's asset, as JoinImageTests and TemplateAssetGuestTests have them with the cookie. Those two classes build
// their own requests, so GuestClient's header mode doesn't reach them; these are their header variants.
[TestFixture]
[CancelAfter(30000)]
public class GuestHeaderFilesTests : JoinFixture
{
    private const string GUEST = "0123456789abcdef0123456789abcdef";
    private const string MANIFEST = """{"name":"Northwind"}""";

    [Test]
    public async Task GetImage_WhenGuestHeaderAndSessionOpen_ShouldServe()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var bytes = ImageSeed.Bytes("image/png");
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, bytes);
        Open(CODE, deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var guest = await GetAsync(anonymous, $"/api/images/{image}", GUEST);
        using var malformed = await GetAsync(anonymous, $"/api/images/{image}", "not-a-guest-id");

        guest.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await guest.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        malformed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTemplateAsset_WhenGuestHeaderAndSessionOpen_ShouldServe()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var font = await UploadFontAsync(template);
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutDeckOnAsync(deck, template);
        Open(CODE, deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var guest = await GetAsync(anonymous, $"/api/templates/{template}/assets/{font}", GUEST);
        using var malformed = await GetAsync(anonymous, $"/api/templates/{template}/assets/{font}", "not-a-guest-id");

        guest.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await guest.Content.ReadAsByteArrayAsync()).ShouldBe(FontFixtures.Normal());
        malformed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

    // No route can put a deck on an uploaded template yet, so the deck's row is pointed at it here, as TemplateAssetGuestTests does.
    private async Task PutDeckOnAsync(string deck, string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var moved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks
            .Where(d => d.Id == deck)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.Template, template));
        moved.ShouldBe(1);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string guest)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(GuestCookie.HEADER, guest);
        return client.SendAsync(request);
    }
}
