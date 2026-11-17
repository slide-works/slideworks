using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Live;

// J3, D17: IAudienceSessions is the one place that decides a session's phase.
[TestFixture]
public class AudienceSessionsTests : ApiTestFixture
{
    [Test]
    public async Task Find_WhenDeckHasNoJoinSlideAndNeverLive_ShouldReturnNull()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        (await FindAsync(await CodeOfAsync(deck))).ShouldBeNull();
        (await FindAsync("000000")).ShouldBeNull();
    }

    [Test]
    public async Task Find_WhenDeckHasAJoinSlide_ShouldBeLobby()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");

        var found = await FindAsync(await CodeOfAsync(deck));

        found.ShouldBe(new AudienceSessionRef(deck, null, AudiencePhase.Lobby));
    }

    [Test]
    public async Task Find_WhenLive_ShouldBeLiveWithTheSessionId()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var live = await PostLiveAsync(deck);

        var found = await FindAsync(live.GetProperty("code").GetString()!);

        found.ShouldBe(new AudienceSessionRef(deck, live.GetProperty("sessionId").GetString(), AudiencePhase.Live));
    }

    [Test]
    public async Task Find_WhenEndedAnHourAgo_ShouldBeEnded()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var code = (await PostLiveAsync(deck)).GetProperty("code").GetString()!;
        await Client.DeleteAsync($"/api/decks/{deck}/live");
        Clock.Advance(TimeSpan.FromHours(1));

        (await FindAsync(code)).ShouldBe(new AudienceSessionRef(deck, null, AudiencePhase.Ended));
    }

    [Test]
    public async Task Find_WhenEndedThreeHoursAgo_ShouldBeLobbyBecauseTheDeckWentLiveBefore()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var code = (await PostLiveAsync(deck)).GetProperty("code").GetString()!;
        await Client.DeleteAsync($"/api/decks/{deck}/live");
        Clock.Advance(TimeSpan.FromHours(3));

        (await FindAsync(code)).ShouldBe(new AudienceSessionRef(deck, null, AudiencePhase.Lobby));
    }

    [Test]
    public async Task ForDeck_WhenDeckIsUnknown_ShouldReturnNull()
    {
        using var scope = Factory.Services.CreateScope();

        (await scope.ServiceProvider.GetRequiredService<IAudienceSessions>().ForDeckAsync("NoSuchDeck123")).ShouldBeNull();
    }

    private async Task<AudienceSessionRef?> FindAsync(string code)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAudienceSessions>().FindAsync(code);
    }

    // The standing code, read from the live state of a deck that is not live.
    private async Task<string> CodeOfAsync(string deck)
        => (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/live")).GetProperty("code").GetString()!;

    private async Task<JsonElement> PostLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }
}
