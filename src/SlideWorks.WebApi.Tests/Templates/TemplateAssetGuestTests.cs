using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Join;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// GET /api/templates/{id}/assets/{assetId} for a guest, as JoinImageTests has it for a deck's images (J3): the sw_guest
// cookie plus a live session on a deck that is on the template serves the template's file though the deck and the
// template are private; without either it is the 404 every unauthorised caller gets.
[TestFixture]
[CancelAfter(30000)]
public class TemplateAssetGuestTests : JoinFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";
    private const string GUEST = "0123456789abcdef0123456789abcdef";

    [Test]
    public async Task Serve_WhenAGuestOfTheDecksLiveSessionAsks_ShouldServeTheFile()
    {
        var (template, font, deck) = await SeedAsync();
        Open(CODE, deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var guest = await GetAsync(anonymous, template, font, GUEST);
        using var noCookie = await GetAsync(anonymous, template, font, null);
        using var forged = await GetAsync(anonymous, template, font, "not-a-guest-id");

        guest.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await guest.Content.ReadAsByteArrayAsync()).ShouldBe(FontFixtures.Normal());
        noCookie.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        forged.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // A live session opens the files of the template its own deck is on, and of no other template.
    [Test]
    public async Task Serve_WhenAGuestOfAnotherDeckAsks_ShouldBeNotFound()
    {
        var (template, font, _) = await SeedAsync();
        var (otherTemplate, otherFont, otherDeck) = await SeedAsync();
        var builtIn = await SharingApi.CreateDeckAsync(Client);
        Open("777777", otherDeck);
        Open("888888", builtIn);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await GetAsync(anonymous, template, font, GUEST);
        using var own = await GetAsync(anonymous, otherTemplate, otherFont, GUEST);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Serve_WhenTheSessionHasEnded_ShouldBeNotFound()
    {
        var (template, font, deck) = await SeedAsync();
        Open("999999", deck);
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var live = await GetAsync(anonymous, template, font, GUEST);

        Sessions.Set("999999", deck, null, AudiencePhase.Ended);
        using var ended = await GetAsync(anonymous, template, font, GUEST);
        Sessions.Set("999999", deck, null, AudiencePhase.Lobby);
        using var lobby = await GetAsync(anonymous, template, font, GUEST);
        Sessions.Clear("999999");
        using var none = await GetAsync(anonymous, template, font, GUEST);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ended.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        lobby.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        none.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Sam's private template with one font, and Sam's private deck on it.
    private async Task<(string Template, string Font, string Deck)> SeedAsync()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        using var start = await Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "font", fileName = "inter.woff2", rightsConfirmed = false });
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var url = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(FontFixtures.Normal());
        using var put = await http.PutAsync(url, content);
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        var font = (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetId").GetString()!;

        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutDeckOnAsync(deck, template);
        return (template, font, deck);
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

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string template, string asset, string? guest)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/templates/{template}/assets/{asset}");
        if (guest is not null)
        {
            request.Headers.Add("Cookie", $"sw_guest={guest}");
        }
        return client.SendAsync(request);
    }
}
