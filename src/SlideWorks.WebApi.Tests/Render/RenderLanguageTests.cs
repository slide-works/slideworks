using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// SW-886 K9: GET /api/render/{id}?language= returns the play view merged in that language, behind the same Preview token. The
// language never stands in for the token.
[TestFixture]
[CancelAfter(60000)]
public class RenderLanguageTests : ApiTestFixture
{
    private const string HEADER = "X-Render-Token";

    // The render tokens with a test key. Registering them here holds whether or not the app registers them too.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton(new RenderTokenOptions { Key = new string('k', RenderTokenOptions.MINIMUM_KEY_LENGTH) });
        services.AddSingleton<IRenderTokens, RenderTokens>();
    };

    [Test]
    public async Task Render_WhenLanguageIsOffered_ShouldReturnTheTranslatedSlots()
    {
        var deck = await SeedAsync();
        using var browser = Browser(Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Preview));

        var response = await browser.GetAsync($"/api/render/{deck}?language=th");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var play = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(play, "deck-play.json", ScreenShapes.DeckPlay);
        play.GetProperty("slides")[0].GetProperty("slots").GetProperty("headline").GetString().ShouldBe("แผน");
    }

    [Test]
    public async Task Render_WhenLanguageIsGivenWithoutAToken_ShouldReturn404()
    {
        var deck = await SeedAsync();
        using var browser = Browser(token: null);

        var response = await browser.GetAsync($"/api/render/{deck}?language=th");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetString().ShouldBe("Deck not found");
        body.ShouldNotContain("แผน");
    }

    // A private deck with one bullets slide, translations on in Thai, and its headline translated.
    private async Task<string> SeedAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", """{ "headline": "Plan", "items": ["A"] }""");
        var rows = new TranslationRowSeed(Factory.Services);
        await rows.SettingsAsync(deck, true, "en", "th");
        await rows.LinesAsync(deck, 1, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/headline"] = ("แผน", "Plan") },
        });
        return deck;
    }

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
