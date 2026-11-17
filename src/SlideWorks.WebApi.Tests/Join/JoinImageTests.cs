using System.Net;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// GET /api/images/{id} for a guest (J3): the sw_guest cookie plus an open session on the image's deck serves an image of a
// private deck, and without either it is the 404 every unauthorised caller gets.
[TestFixture]
[CancelAfter(30000)]
public class JoinImageTests : JoinFixture
{
    private const string GUEST = "0123456789abcdef0123456789abcdef";

    [Test]
    public async Task Get_WhenGuestAndSessionOpen_ShouldServe()
    {
        var (deck, image, bytes) = await SeedAsync();
        Open(CODE, deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await GetAsync(anonymous, image, GUEST);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    [Test]
    public async Task Get_WhenGuestAndNoSession_ShouldReturn404()
    {
        var (deck, image, _) = await SeedAsync();
        Sessions.Set("555555", deck, null, AudiencePhase.Lobby);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var lobby = await GetAsync(anonymous, image, GUEST);
        Sessions.Clear("555555");
        using var none = await GetAsync(anonymous, image, GUEST);

        lobby.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        none.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenSessionOpenButNoGuestCookie_ShouldReturn404()
    {
        var (deck, image, _) = await SeedAsync();
        Open("666666", deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await GetAsync(anonymous, image, null);
        using var forged = await GetAsync(anonymous, image, "not-a-guest-id");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        forged.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(string Deck, string Image, byte[] Bytes)> SeedAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var bytes = ImageSeed.Bytes("image/png");
        return (deck, await ImageSeed.UploadAsync(agent, Factory, deck, bytes), bytes);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string image, string? guest)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{image}");
        if (guest is not null)
        {
            request.Headers.Add("Cookie", $"sw_guest={guest}");
        }
        return client.SendAsync(request);
    }
}
