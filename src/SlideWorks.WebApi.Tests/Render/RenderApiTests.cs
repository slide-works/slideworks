using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// L9: GET /api/render/{id} and /api/render/{id}/card are anonymous and read the X-Render-Token header only. A token
// opens exactly one deck for exactly one purpose; anything else is 404 "Deck not found". The card is generic unless the
// deck is link-shared (D12).
[TestFixture]
public class RenderApiTests : ApiTestFixture
{
    private const string HEADER = "X-Render-Token";

    private static readonly (HttpStatusCode, string?) NOT_FOUND = (HttpStatusCode.NotFound, "Deck not found");

    // The render tokens with a test key. Registering them here holds whether or not the app registers them too.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton(new RenderTokenOptions { Key = new string('k', RenderTokenOptions.MINIMUM_KEY_LENGTH) });
        services.AddSingleton<IRenderTokens, RenderTokens>();
    };

    [Test]
    public async Task Get_WhenPreviewTokenForThePrivateDeck_ShouldReturnThePlayView()
    {
        var deck = await CreateDeckAsync("Private");
        using var browser = Browser(Issue(deck, RenderPurpose.Preview));

        var response = await browser.GetAsync($"/api/render/{deck}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var play = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(play, "deck-play.json", ScreenShapes.DeckPlay);
        play.GetProperty("id").GetString().ShouldBe(deck);
        play.GetProperty("slides").GetArrayLength().ShouldBe(1);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("SECRET-SPEAKER-NOTE");
    }

    [Test]
    public async Task Get_WhenNoToken_ShouldReturn404()
    {
        var deck = await CreateDeckAsync("No token");
        using var browser = Browser(token: null);

        (await OutcomeAsync(await browser.GetAsync($"/api/render/{deck}"))).ShouldBe(NOT_FOUND);
        (await OutcomeAsync(await browser.GetAsync($"/api/render/{deck}/card"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task Get_WhenAnotherDecksToken_ShouldReturn404()
    {
        var deck = await CreateDeckAsync("Mine");
        var other = await CreateDeckAsync("Other");
        using var browser = Browser(Issue(other, RenderPurpose.Preview));

        (await OutcomeAsync(await browser.GetAsync($"/api/render/{deck}"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task Get_WhenTokenForTheWrongPurpose_ShouldReturn404()
    {
        var deck = await CreateDeckAsync("Purpose");
        using var card = Browser(Issue(deck, RenderPurpose.Card));
        using var preview = Browser(Issue(deck, RenderPurpose.Preview));

        (await OutcomeAsync(await card.GetAsync($"/api/render/{deck}"))).ShouldBe(NOT_FOUND);
        (await OutcomeAsync(await preview.GetAsync($"/api/render/{deck}/card"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task Get_WhenTokenInTheQueryString_ShouldIgnoreIt()
    {
        var deck = await CreateDeckAsync("Query");
        using var browser = Browser(token: null);
        var token = Uri.EscapeDataString(Issue(deck, RenderPurpose.Preview));

        (await OutcomeAsync(await browser.GetAsync($"/api/render/{deck}?token={token}"))).ShouldBe(NOT_FOUND);
        (await OutcomeAsync(await browser.GetAsync($"/api/render/{deck}?{HEADER}={token}"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task Get_WhenUnknownDeckWithItsOwnIdsToken_ShouldReturn404()
    {
        using var browser = Browser(Issue("AbCdEfGhIjKl", RenderPurpose.Preview));

        (await OutcomeAsync(await browser.GetAsync("/api/render/AbCdEfGhIjKl"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task Card_WhenPrivateDeck_ShouldBeGeneric()
    {
        var deck = await CreateDeckAsync("Secret title");
        using var browser = Browser(Issue(deck, RenderPurpose.Card));

        var response = await browser.GetAsync($"/api/render/{deck}/card");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("Secret title");
        var card = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(card, "card.json", ScreenShapes.Card);
        card.GetProperty("generic").GetBoolean().ShouldBeTrue();
        card.GetProperty("title").ValueKind.ShouldBe(JsonValueKind.Null);
        card.GetProperty("subtitle").ValueKind.ShouldBe(JsonValueKind.Null);
        card.GetProperty("author").ValueKind.ShouldBe(JsonValueKind.Null);
        card.GetProperty("slideCount").GetInt32().ShouldBe(0);
        card.GetProperty("look").GetProperty("template").GetProperty("name").GetString().ShouldBe("talk");
    }

    [Test]
    public async Task Card_WhenLinkDeck_ShouldNameTheDeck()
    {
        var deck = await CreateDeckAsync("Ship smaller", template: "workshop", palette: "ember");
        (await Client.PutAsJsonAsync($"/api/decks/{deck}/sharing", new { level = "link", viewers = Array.Empty<string>() })).EnsureSuccessStatusCode();
        using var browser = Browser(Issue(deck, RenderPurpose.Card));

        var response = await browser.GetAsync($"/api/render/{deck}/card");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var card = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(card, "card.json", ScreenShapes.Card);
        card.GetProperty("generic").GetBoolean().ShouldBeFalse();
        card.GetProperty("title").GetString().ShouldBe("Ship smaller");
        card.GetProperty("subtitle").GetString().ShouldBe("Why small batches win");
        card.GetProperty("author").GetString().ShouldBe("Sam Rivera");
        card.GetProperty("slideCount").GetInt32().ShouldBe(1);
        card.GetProperty("look").GetProperty("palette").GetProperty("name").GetString().ShouldBe("ember");
    }

    private async Task<string> CreateDeckAsync(string title, string? template = null, string? palette = null)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, created) = await agent.CallToolAsync("create_deck", new { title, subtitle = "Why small batches win", author = "Sam Rivera", template, palette });
        isError.ShouldBeFalse(created.ToString());
        var deck = created.GetProperty("deck").GetString()!;
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "One", "items": ["a"] }""", notes: "SECRET-SPEAKER-NOTE");
        return deck;
    }

    private string Issue(string deck, RenderPurpose purpose) => Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, purpose);

    // A headless browser: no sign-in, only the render token header when it has one.
    private HttpClient Browser(string? token)
    {
        var client = ClientFor(TestUser.Anonymous);
        if (token is not null)
        {
            client.DefaultRequestHeaders.Add(HEADER, token);
        }
        return client;
    }

    private static async Task<(HttpStatusCode Status, string? Error)> OutcomeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var error = body.Length > 0 && JsonDocument.Parse(body).RootElement.TryGetProperty("error", out var text) ? text.GetString() : null;
        return (response.StatusCode, error);
    }
}
