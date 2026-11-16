using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// GET /api/cards/{id}.png (L9, D12): anonymous. A link-shared deck gets its own card; an unknown, private or
// named-viewer deck gets the generic card and never starts a deck render. The card's RenderKey is the ETag, and a
// renderer that can't run is 503 with its text, never the generic card in disguise.
[TestFixture]
public class CardPngTests : ApiTestFixture
{
    private FakeRenderEngine Engine => Factory.Services.GetRequiredService<FakeRenderEngine>();

    [Test]
    public async Task Png_WhenAnonymousAndLinkDeck_ShouldReturnTheDecksCard()
    {
        var deck = await CreateDeckAsync("Link deck", sharing: "link");
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/cards/{deck}.png");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(FakeRenderEngine.PNG);
        Engine.Calls.ShouldContain(deck);
        response.Headers.ETag!.Tag.ShouldStartWith($"\"{deck}:");
        response.Headers.ETag.Tag.ShouldEndWith(":link\"");
    }

    [TestCase("private")]
    [TestCase("named")]
    public async Task Png_WhenDeckIsNotLinkShared_ShouldReturnTheGenericCardWithoutRenderingTheDeck(string sharing)
    {
        var deck = await CreateDeckAsync("Secret title", sharing);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/cards/{deck}.png");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(FakeRenderEngine.PNG);
        response.Headers.ETag!.Tag.ShouldBe("\"generic\"");
        Engine.Calls.ShouldNotContain(deck);
        Engine.Calls.ShouldContain(FakeRenderEngine.GENERIC);
    }

    [Test]
    public async Task Png_WhenUnknownDeck_ShouldReturnTheGenericCardWithoutRenderingADeck()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/cards/no-such-deck.png");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.ETag!.Tag.ShouldBe("\"generic\"");
        Engine.Calls.ShouldNotContain("no-such-deck");
    }

    [Test]
    public async Task Png_WhenIfNoneMatchIsTheCardsKey_ShouldReturn304()
    {
        var deck = await CreateDeckAsync("Cached", sharing: "link");
        using var anonymous = ClientFor(TestUser.Anonymous);
        var first = await anonymous.GetAsync($"/api/cards/{deck}.png");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var again = new HttpRequestMessage(HttpMethod.Get, $"/api/cards/{deck}.png");
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await anonymous.SendAsync(again);

        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        second.Headers.ETag.ShouldBe(first.Headers.ETag);
        (await second.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Png_WhenTheDeckChanges_ShouldChangeTheETag()
    {
        var deck = await CreateDeckAsync("Before", sharing: "link");
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = (await anonymous.GetAsync($"/api/cards/{deck}.png")).Headers.ETag!;

        (await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { palette = "ember" })).EnsureSuccessStatusCode();
        var after = (await anonymous.GetAsync($"/api/cards/{deck}.png")).Headers.ETag!;

        after.ShouldNotBe(before);
        new EntityTagHeaderValue(after.Tag).Tag.ShouldStartWith($"\"{deck}:");
    }

    [Test]
    public async Task Png_WhenTheRendererIsUnavailable_ShouldReturn503WithItsText()
    {
        var deck = await CreateDeckAsync("Unavailable", sharing: "link");
        using var anonymous = ClientFor(TestUser.Anonymous);
        Engine.ThrowUnavailable();
        try
        {
            var response = await anonymous.GetAsync($"/api/cards/{deck}.png");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("Card rendering isn't available");
        }
        finally
        {
            Engine.ThrowOnRender = null;
        }
    }

    private async Task<string> CreateDeckAsync(string title, string sharing)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, title, author: "Sam Rivera");
        if (sharing != "private")
        {
            var viewers = sharing == "named" ? new[] { "viewer@example.com" } : [];
            (await Client.PutAsJsonAsync($"/api/decks/{deck}/sharing", new { level = sharing, viewers })).EnsureSuccessStatusCode();
        }
        return deck;
    }

    private sealed record ErrorBody(string Error);
}
