using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// GET /api/decks/{id}/presenter (P3): the deck's owner gets the play view plus notes, headlines, the talk length and the
// audience and phone URLs. Everyone else gets the same 404 as an unknown deck (P9: notes reach only the owner).
[TestFixture]
public class PresenterViewTests : ApiTestFixture
{
    private static readonly (HttpStatusCode, string?) NOT_FOUND = (HttpStatusCode.NotFound, "Deck not found");

    [Test]
    public async Task GetPresenter_WhenOwner_ShouldReturnThePresenterView()
    {
        var deck = await OwnDeckAsync();

        var response = await Client.GetAsync($"/api/decks/{deck}/presenter");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, "presenter.json", ScreenShapes.Presenter);
        view.GetProperty("id").GetString().ShouldBe(deck);
        view.GetProperty("audienceUrl").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{deck}/ship-smaller?present");
        view.GetProperty("remoteUrl").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/remote?deck={deck}");
        view.GetProperty("talkMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
        var slides = view.GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => s.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3]);
        slides[0].GetProperty("notes").GetString().ShouldBe(PresentingSeed.TITLE_NOTES);
        slides[0].GetProperty("headline").GetString().ShouldBe("Ship smaller");
        slides[1].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
        slides[1].GetProperty("headline").GetString().ShouldBe("Every slip we had last year started as a big batch");
        slides[1].GetProperty("steps").GetInt32().ShouldBe(PresentingSeed.BULLETS_STEPS);
        slides[2].GetProperty("deepDive").GetBoolean().ShouldBeTrue();
        slides[2].GetProperty("notes").GetString().ShouldBe(PresentingSeed.QUOTE_NOTES);
    }

    // The headlines are the deck page's, slide for slide.
    [Test]
    public async Task GetPresenter_WhenOwner_ShouldUseTheDeckPagesHeadlines()
    {
        var deck = await OwnDeckAsync();

        var presenter = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/presenter");
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        Headlines(presenter).ShouldBe(Headlines(page));
    }

    // The join slide in the presenter window and on the phone shows the deck's code and whether it is live.
    [Test]
    public async Task GetPresenter_WhenDeckIsLive_ShouldCarryTheJoinAsLive()
    {
        var deck = await OwnDeckAsync();
        var before = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/presenter");
        (await Client.PostAsync($"/api/decks/{deck}/live", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/presenter");

        before.GetProperty("join").GetProperty("live").GetBoolean().ShouldBeFalse();
        after.GetProperty("join").GetProperty("live").GetBoolean().ShouldBeTrue();
        after.GetProperty("join").GetProperty("code").GetString().ShouldBe(before.GetProperty("join").GetProperty("code").GetString());
    }

    [Test]
    public async Task GetPresenter_WhenLinkViewer_ShouldReturn404()
    {
        var deck = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "link");
        using var alex = ClientFor(TestUser.Alex);

        (await RefusalAsync(await alex.GetAsync($"/api/decks/{deck}/presenter"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task GetPresenter_WhenNamedViewer_ShouldReturn404()
    {
        var deck = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Mira.Email);
        using var mira = ClientFor(TestUser.Mira);

        (await RefusalAsync(await mira.GetAsync($"/api/decks/{deck}/presenter"))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task GetPresenter_WhenAnonymous_ShouldReturn404()
    {
        var deck = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, "link");
        using var anonymous = ClientFor(TestUser.Anonymous);

        (await RefusalAsync(await anonymous.GetAsync($"/api/decks/{deck}/presenter"))).ShouldBe(NOT_FOUND);
    }

    // The owner's own agent doesn't get the notes view either: presenting is for the person.
    [Test]
    public async Task GetPresenter_WhenOwnersAgentBearer_ShouldReturn404()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await PresentingSeed.CreateDeckAsync(agent);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/decks/{deck}/presenter");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.Tokens.AccessToken);

        (await RefusalAsync(await http.SendAsync(request))).ShouldBe(NOT_FOUND);
    }

    [Test]
    public async Task GetPresenter_WhenUnknownDeck_ShouldReturn404()
    {
        (await RefusalAsync(await Client.GetAsync("/api/decks/nOsUcHdEcK00/presenter"))).ShouldBe(NOT_FOUND);
    }

    // The status and the K4 error text, which a refusal shares with an unknown deck.
    private static async Task<(HttpStatusCode, string?)> RefusalAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var error = response.Content.Headers.ContentType?.MediaType == "application/json"
            ? JsonDocument.Parse(body).RootElement.GetProperty("error").GetString()
            : body;
        return (response.StatusCode, error);
    }

    private static List<string?> Headlines(JsonElement view)
        => view.GetProperty("slides").EnumerateArray().Select(s => s.GetProperty("headline").GetString()).ToList();

    private async Task<string> OwnDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return await PresentingSeed.CreateDeckAsync(agent);
    }
}
