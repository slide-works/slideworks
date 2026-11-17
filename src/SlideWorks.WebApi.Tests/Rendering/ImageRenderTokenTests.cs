using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Rendering;

// GET /api/images/{id} with a render token (L9): a headless render page reads its own deck's images, on a private deck,
// with the token in the X-Render-Token header or the sw_render cookie from POST /api/render/{id}/session. The token opens
// no other deck's images, and without it a private deck's image is 404 to an anonymous caller.
[TestFixture]
public class ImageRenderTokenTests : ApiTestFixture
{
    private string _deck = null!;
    private string _image = null!;
    private byte[] _bytes = null!;
    private string _otherImage = null!;

    [OneTimeSetUp]
    public async Task UploadImages()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        _deck = await DeckSeed.CreateDeckAsync(agent);
        _bytes = ImageSeed.Bytes("image/png");
        _image = await ImageSeed.UploadAsync(agent, Factory, _deck, _bytes);
        var otherDeck = await DeckSeed.CreateDeckAsync(agent, "Another deck");
        _otherImage = await ImageSeed.UploadAsync(agent, Factory, otherDeck, ImageSeed.Bytes("image/png"));
    }

    [TestCase(RenderPurpose.Preview)]
    [TestCase(RenderPurpose.Card)]
    public async Task Get_WhenHeaderTokenForTheImagesDeck_ShouldServeIt(RenderPurpose purpose)
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await GetWithHeaderAsync(anonymous, _image, Issue(_deck, purpose));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(_bytes);
    }

    [Test]
    public async Task Get_WhenSessionCookieForTheImagesDeck_ShouldServeIt()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        await StartSessionAsync(anonymous, _deck, Issue(_deck, RenderPurpose.Preview));

        using var response = await anonymous.GetAsync($"/api/images/{_image}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(_bytes);
    }

    [Test]
    public async Task Get_WhenHeaderTokenForAnotherDeck_ShouldBe404()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await GetWithHeaderAsync(anonymous, _otherImage, Issue(_deck, RenderPurpose.Preview));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenSessionCookieForAnotherDeck_ShouldBe404()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        await StartSessionAsync(anonymous, _deck, Issue(_deck, RenderPurpose.Preview));

        using var response = await anonymous.GetAsync($"/api/images/{_otherImage}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenNoTokenOnAPrivateDecksImage_ShouldBe404()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.GetAsync($"/api/images/{_image}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenHeaderTokenTampered_ShouldBe404()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await GetWithHeaderAsync(anonymous, _image, Issue(_deck, RenderPurpose.Preview) + "x");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private string Issue(string deck, RenderPurpose purpose) =>
        Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, purpose);

    private static async Task<HttpResponseMessage> GetWithHeaderAsync(HttpClient client, string image, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{image}");
        request.Headers.Add(RenderSessions.HEADER, token);
        return await client.SendAsync(request);
    }

    private static async Task StartSessionAsync(HttpClient client, string deck, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/render/{deck}/session");
        request.Headers.Add(RenderSessions.HEADER, token);
        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
