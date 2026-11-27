using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Render.Print;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// P1: GET /api/render/{id}/print reads the X-Render-Token header only, with a Print token for that deck. PrintSessions picks
// the deck's latest session, and PrintViews.ViewAsync is the same view without a token, equal for unchanged data.
[TestFixture]
public class PrintViewTests : ApiTestFixture
{
    private const string HEADER = "X-Render-Token";

    [Test]
    public async Task GetPrint_WhenTokenIsForPrint_ShouldReturnTheDeckAndEmptyResults()
    {
        var deck = await CreateDeckWithASlideAsync("Print me");
        using var browser = Browser(Issue(deck, RenderPurpose.Print));

        var response = await browser.GetAsync($"/api/render/{deck}/print");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, "deck-print.json", ScreenShapes.DeckPrint);
        view.GetProperty("deck").GetProperty("id").GetString().ShouldBe(deck);
        view.GetProperty("deck").GetProperty("slides").GetArrayLength().ShouldBe(1);
        view.GetProperty("polls").GetArrayLength().ShouldBe(0);
        view.GetProperty("wordClouds").GetArrayLength().ShouldBe(0);
        view.GetProperty("openTexts").GetArrayLength().ShouldBe(0);
        view.GetProperty("qa").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [TestCase("preview")]
    [TestCase("card")]
    [TestCase("missing")]
    [TestCase("another deck")]
    public async Task GetPrint_WhenTokenIsPreviewOrMissingOrForAnotherDeck_ShouldBe404(string token)
    {
        var deck = await CreateDeckAsync("Refused");
        var other = await CreateDeckAsync("Other");
        using var browser = Browser(token switch
        {
            "preview" => Issue(deck, RenderPurpose.Preview),
            "card" => Issue(deck, RenderPurpose.Card),
            "another deck" => Issue(other, RenderPurpose.Print),
            _ => null,
        });

        var response = await browser.GetAsync($"/api/render/{deck}/print");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Deck not found");
    }

    [Test]
    public async Task GetPrint_WhenThePrintTokenIsInTheQueryStringOnly_ShouldBe404()
    {
        var deck = await CreateDeckAsync("Query token");
        var token = Issue(deck, RenderPurpose.Print);
        using var browser = Browser(null);

        var response = await browser.GetAsync($"/api/render/{deck}/print?token={Uri.EscapeDataString(token)}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Deck not found");
    }

    [Test]
    public async Task NewestAsync_WhenTheDeckRanTwoSessions_ShouldPickTheLaterOne()
    {
        var deck = await CreateDeckAsync("Two talks");
        await GoLiveAsync(deck);
        await EndAsync(deck);
        var first = await NewestAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(30));
        await GoLiveAsync(deck);

        var second = await NewestAsync(deck);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        second.ShouldNotBe(first);
    }

    [Test]
    public async Task NewestAsync_WhenNeverLive_ShouldBeNull()
    {
        var deck = await CreateDeckAsync("Never live");

        (await NewestAsync(deck)).ShouldBeNull();
    }

    [Test]
    public async Task ViewAsync_WhenCalledTwiceOnAnUnchangedDeck_ShouldGiveEqualJson()
    {
        var deck = await CreateDeckAsync("Twice");
        await GoLiveAsync(deck);

        var first = await ViewJsonAsync(deck);
        var second = await ViewJsonAsync(deck);

        second.ShouldBe(first);
    }

    [Test]
    public async Task ViewAsync_WhenTheDeckIsUnknown_ShouldThrowNotFound()
    {
        using var scope = Factory.Services.CreateScope();

        var thrown = await Should.ThrowAsync<NotFoundException>(
            () => scope.ServiceProvider.GetRequiredService<PrintViews>().ViewAsync("NoSuchDeck01", CancellationToken.None));

        thrown.Message.ShouldBe("Deck not found");
    }

    private async Task<Guid?> NewestAsync(string deck)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PrintSessions>().NewestAsync(deck, CancellationToken.None);
    }

    private async Task<string> ViewJsonAsync(string deck)
    {
        using var scope = Factory.Services.CreateScope();
        var view = await scope.ServiceProvider.GetRequiredService<PrintViews>().ViewAsync(deck, CancellationToken.None);
        return JsonSerializer.Serialize(view, JsonSerializerOptions.Web);
    }

    private async Task GoLiveAsync(string deck) => (await Client.PostAsync($"/api/decks/{deck}/live", null)).EnsureSuccessStatusCode();

    private async Task EndAsync(string deck) => (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();

    private async Task<string> CreateDeckAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<string> CreateDeckWithASlideAsync(string title)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, title);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "One", "items": ["a"] }""");
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
}
