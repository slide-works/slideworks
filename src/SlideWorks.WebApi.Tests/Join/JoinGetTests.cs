using System.Net;
using System.Text.Json;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// GET /api/join/{code} (J3): anonymous, a code with no session is the same 404 whatever the reason, the first successful
// read gives the phone its sw_guest cookie, and a lookup that finds nothing counts as a miss for the rate limit.
[TestFixture]
[CancelAfter(30000)]
public class JoinGetTests : JoinFixture
{
    [Test]
    public async Task Get_WhenUnknownCode_ShouldReturnNoSessionWithSameBody()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Sessions.Set("222222", await DeckSeed.CreateDeckAsync(agent), null, SlideWorks.WebApi.Realtime.AudiencePhase.Live);
        Sessions.Clear("222222");
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var neverSet = await anonymous.GetAsync("/api/join/111111");
        using var cleared = await anonymous.GetAsync("/api/join/222222");
        using var malformed = await anonymous.GetAsync("/api/join/not-a-code");

        neverSet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await neverSet.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_session"}""");
        (await cleared.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_session"}""");
        (await malformed.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_session"}""");
        neverSet.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenNoCookie_ShouldSetGuestCookieOnce()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var first = await anonymous.GetAsync($"/api/join/{CODE}");
        var cookie = first.Headers.GetValues("Set-Cookie").Single();
        using var second = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{CODE}");
        second.Headers.Add("Cookie", cookie.Split(';')[0]);
        using var again = await anonymous.SendAsync(second);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        cookie.ShouldMatch("^sw_guest=[0-9a-f]{32}; ");
        cookie.ToLowerInvariant().ShouldContain("max-age=43200");
        cookie.ToLowerInvariant().ShouldContain("httponly");
        cookie.ToLowerInvariant().ShouldContain("samesite=lax");
        cookie.ToLowerInvariant().ShouldContain("path=/");
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenLive_ShouldMatchTheGuestContract()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.GetAsync($"/api/join/{CODE}");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "guest.json")));
        JsonShape.AssertMatches(body, sample.RootElement.GetProperty("live"), "guest.json", ScreenShapes.Guest);
        body.GetProperty("presenterName").GetString().ShouldBe("Sam Rivera");
        body.GetProperty("state").GetProperty("type").GetString().ShouldBe("audienceState");
        body.GetProperty("state").GetProperty("phase").GetString().ShouldBe("live");
        body.GetProperty("look").GetProperty("joinBar").GetBoolean().ShouldBeFalse();
    }

    // SW-883 K5: ?language= is bound and handed to the relay; the stand-in answers the primary state, translations off.
    [Test]
    public async Task JoinGet_WhenLanguageGiven_ShouldMatchGuestShape()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        Open(CODE, deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.GetAsync($"/api/join/{CODE}?language=th");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "guest.json")));
        JsonShape.AssertMatches(body, sample.RootElement.GetProperty("live"), "guest.json", ScreenShapes.Guest);
        body.GetProperty("translations").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("state").GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
        Relay.LanguagesReadFor(deck).ShouldBe(["th"]);
    }

    [Test]
    public async Task Get_WhenLobby_ShouldHaveNoCurrentSlide()
    {
        var (status, state) = await GetStateAsync(AudiencePhase.Lobby, "lobby");

        status.ShouldBe(HttpStatusCode.OK);
        state.GetProperty("phase").GetString().ShouldBe("lobby");
        state.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        state.GetProperty("room").ValueKind.ShouldBe(JsonValueKind.Null);
        state.GetProperty("activity").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Get_WhenEnded_ShouldHaveNoCurrentSlide()
    {
        var (status, state) = await GetStateAsync(AudiencePhase.Ended, "ended");

        status.ShouldBe(HttpStatusCode.OK);
        state.GetProperty("phase").GetString().ShouldBe("ended");
        state.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        state.GetProperty("room").ValueKind.ShouldBe(JsonValueKind.Null);
        state.GetProperty("activity").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // Reads the guest view in a phase and checks its key set against that phase's sample in guest.json.
    private async Task<(HttpStatusCode Status, JsonElement State)> GetStateAsync(AudiencePhase phase, string phaseName)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        Sessions.Set(CODE, deck, null, phase);
        // The relay is a fake here, so it is given the state the real one gives outside live: no slide, no room, no activity.
        Relay.Set(new AudienceState(deck, 1, phase, null, 0, null, null, null));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.GetAsync($"/api/join/{CODE}");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        using var sample = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "guest.json")));
        JsonShape.AssertMatches(body, sample.RootElement.GetProperty(phaseName), "guest.json", ScreenShapes.Guest);
        return (response.StatusCode, body.GetProperty("state"));
    }

    [Test]
    public async Task Get_WhenNoSession_ShouldCountAMissButAHitNever()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        var before = Limiter.Misses;

        using var hit = await anonymous.GetAsync($"/api/join/{CODE}");
        var afterHit = Limiter.Misses;
        using var miss = await anonymous.GetAsync("/api/join/999999");

        hit.StatusCode.ShouldBe(HttpStatusCode.OK);
        afterHit.ShouldBe(before);
        Limiter.Misses.ShouldBe(before + 1);
    }

    [Test]
    public async Task Get_WhenCallerHasUsedUpItsMisses_ShouldReturn429ForARealCodeToo()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        Limiter.Refuse = true;
        try
        {
            using var miss = await anonymous.GetAsync("/api/join/999999");
            using var hit = await anonymous.GetAsync($"/api/join/{CODE}");

            miss.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            (await miss.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_attempts"}""");
            hit.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            (await hit.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_attempts"}""");
            Limiter.Misses.ShouldBe(0);
        }
        finally
        {
            Limiter.Refuse = false;
        }
    }

    [Test]
    public async Task Get_WhenRefusedWith429_ShouldSetNoGuestCookie()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Open(CODE, await DeckSeed.CreateDeckAsync(agent));
        using var anonymous = ClientFor(TestUser.Anonymous);
        Limiter.Refuse = true;
        try
        {
            using var refused = await anonymous.GetAsync($"/api/join/{CODE}");

            refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            refused.Headers.Contains("Set-Cookie").ShouldBeFalse();
        }
        finally
        {
            Limiter.Refuse = false;
        }
    }

    [Test]
    public async Task GuestView_WhenNotLinkShared_ShouldHaveNullDeckUrl()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var privateDeck = await DeckSeed.CreateDeckAsync(agent);
        var linkDeck = await DeckSeed.CreateDeckAsync(agent, "Shared by link");
        await SharingApi.PutOkAsync(Client, linkDeck, "link");
        Open("333333", privateDeck);
        Open("444444", linkDeck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var closed = JsonDocument.Parse(await anonymous.GetStringAsync("/api/join/333333")).RootElement;
        var shared = JsonDocument.Parse(await anonymous.GetStringAsync("/api/join/444444")).RootElement;

        closed.GetProperty("deckUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        shared.GetProperty("deckUrl").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/d/{linkDeck}/shared-by-link");
    }
}
