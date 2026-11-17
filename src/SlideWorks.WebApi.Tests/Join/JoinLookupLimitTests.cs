using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// The real IJoinLookupLimiter (J3, D20): misses from GET /api/join/{code} and from JoinAudience share 10 a minute per caller;
// once they are used up every lookup is refused, a real code included; a hit is never counted. IAudienceSessions is the fake,
// so a code nobody set is a miss. Each test uses its own fixture state through its own caller: the test server's remote
// address is the same for all of them, so the tests reset the window by moving the clock past a minute.
[TestFixture]
[CancelAfter(60000)]
public class JoinLookupLimitTests : ApiTestFixture
{
    private const string CODE = "482913";
    private const string SESSION = "11111111-1111-1111-1111-111111111111";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
    };

    private FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    private FakeAudienceSessions Sessions => Factory.Services.GetRequiredService<FakeAudienceSessions>();

    [SetUp]
    public void StartANewWindow() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task JoinLookup_WhenElevenMissesInAMinuteAcrossGetAndHub_ShouldRefuse()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        using var anonymous = ClientFor(TestUser.Anonymous);
        for (var i = 0; i < 5; i++)
        {
            (await anonymous.GetAsync($"/api/join/99999{i}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync("JoinAudience", $"99998{i}"));
        }

        using var eleventh = await anonymous.GetAsync("/api/join/999990");
        var eleventhOnHub = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync("JoinAudience", "999990"));

        eleventh.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await eleventh.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_attempts"}""");
        eleventhOnHub.Message.ShouldContain("too_many_attempts", Case.Sensitive);
    }

    // D20: the limit is checked before the lookup, so a real code is refused as well once the misses are used up.
    [Test]
    public async Task JoinLookup_WhenOverTheLimit_ShouldRefuseARealCode()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        Sessions.Set(CODE, await DeckSeed.CreateDeckAsync(agent), SESSION, AudiencePhase.Live);
        using var anonymous = ClientFor(TestUser.Anonymous);
        await UseUpMissesAsync(anonymous);

        using var real = await anonymous.GetAsync($"/api/join/{CODE}");

        real.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task JoinLookup_WhenHit_ShouldNotCountTowardTheLimit()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        Sessions.Set(CODE, deckId, SESSION, AudiencePhase.Live);
        Relay.Set(new AudienceState(deckId, 1, AudiencePhase.Live, new AudienceSlide(1, 1, "title", JsonDocument.Parse("""{"title":"Ship smaller"}""").RootElement, 0, 0, false), 0, null));
        using var anonymous = ClientFor(TestUser.Anonymous);
        for (var i = 0; i < 25; i++)
        {
            (await anonymous.GetAsync($"/api/join/{CODE}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var miss = await anonymous.GetAsync("/api/join/999999");

        miss.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // A parallel guesser can have up to MAX_IN_FLIGHT lookups unsettled, so the 404s are bounded, not exactly 10, and the
    // bound is what is asserted: what answered 404 is at most MAX_MISSES + MAX_IN_FLIGHT - 1, and once they have settled the
    // caller is refused.
    [Test]
    public async Task JoinLookup_WhenThirtyParallelMisses_ShouldRefuseTheNextLookup()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => anonymous.GetAsync("/api/join/999999")));

        responses.Count(r => r.StatusCode == HttpStatusCode.NotFound).ShouldBeInRange(10, JoinLookupLimiter.MAX_MISSES + JoinLookupLimiter.MAX_IN_FLIGHT - 1);
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.TooManyRequests);
        (await anonymous.GetAsync("/api/join/999999")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task JoinLookup_WhenThirtyParallelHits_ShouldAllBeServedAndCountNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deckId = await DeckSeed.CreateDeckAsync(agent);
        Sessions.Set(CODE, deckId, SESSION, AudiencePhase.Live);
        Relay.Set(new AudienceState(deckId, 1, AudiencePhase.Live, new AudienceSlide(1, 1, "title", JsonDocument.Parse("""{"title":"Ship smaller"}""").RootElement, 0, 0, false), 0, null));
        using var anonymous = ClientFor(TestUser.Anonymous);

        var hits = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => anonymous.GetAsync($"/api/join/{CODE}")));
        var misses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            misses.Add((await anonymous.GetAsync("/api/join/999999")).StatusCode);
        }

        hits.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        misses.Count(s => s == HttpStatusCode.NotFound).ShouldBe(10);
        misses[^1].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task JoinLookup_WhenTheMinuteEnds_ShouldAllowAgain()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);
        await UseUpMissesAsync(anonymous);
        (await anonymous.GetAsync("/api/join/999999")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        Clock.Advance(TimeSpan.FromSeconds(61));

        (await anonymous.GetAsync("/api/join/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task UseUpMissesAsync(HttpClient anonymous)
    {
        for (var i = 0; i < 10; i++)
        {
            (await anonymous.GetAsync("/api/join/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }
}
