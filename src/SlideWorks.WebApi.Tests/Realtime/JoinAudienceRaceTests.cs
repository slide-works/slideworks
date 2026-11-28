using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// JoinAudience checks the session exists, joins the group, then reads the state once (SW-205 review), so a state published in
// the gap between the check and the join is not missed, and a guest whose read then finds nothing is taken out of the group
// again and refused.
[TestFixture]
[CancelAfter(30000)]
public class JoinAudienceRaceTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        services.AddSingleton<HookedSessions>().AddSingleton<IAudienceSessions>(provider => provider.GetRequiredService<HookedSessions>());
        services.AddSingleton<ScriptedRelay>().AddSingleton<IAudienceRelay>(provider => provider.GetRequiredService<ScriptedRelay>());
    };

    [Test]
    public async Task JoinAudience_WhenStateVanishesAfterTheGuestJoinedTheGroup_ShouldRefuseAsNoSessionAndLeaveTheGroup()
    {
        var (deck, relay) = await SeedAsync();
        relay.Script = (_, _) => Task.FromResult<AudienceState?>(null);
        await using var vanished = await GuestClient.ConnectAsync(Factory);

        var refused = await Should.ThrowAsync<HubException>(() => vanished.Connection.InvokeAsync<object>("JoinAudience", "123456"));
        // A second guest joins normally, so a state published now goes to the group: only the guest that left misses it.
        relay.Script = (_, id) => Task.FromResult<AudienceState?>(StateAt(id, seq: 5));
        await using var control = await GuestClient.ConnectAsync(Factory);
        await control.Connection.InvokeAsync<JsonElement>("JoinAudience", "123456");
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();
        await bus.PublishAsync(StateAt(deck, seq: 6));
        var received = await control.NextAudienceAsync();
        await bus.PublishAsync(StateAt(deck, seq: 7));
        await control.NextAudienceAsync();
        var vanishedNext = vanished.NextAudienceAsync();
        var first = await Task.WhenAny(vanishedNext, Task.Delay(500));

        refused.Message.ShouldContain(DeckHub.NO_SESSION);
        received.GetProperty("seq").GetInt64().ShouldBe(6);
        first.ShouldNotBe(vanishedNext);
    }

    [Test]
    public async Task JoinAudience_WhenAStateIsPushedBetweenTheFirstReadAndTheGroupJoin_ShouldReturnItToTheGuest()
    {
        var (_, relay) = await SeedAsync();
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();
        var latest = 1L;
        relay.Script = (_, id) => Task.FromResult<AudienceState?>(StateAt(id, Interlocked.Read(ref latest)));
        // The presenter moves in the gap: after the existence check, before the guest is in the group, so the push itself is missed.
        Factory.Services.GetRequiredService<HookedSessions>().SetOnFirstForDeck(async id =>
        {
            Interlocked.Exchange(ref latest, 2);
            await bus.PublishAsync(StateAt(id, seq: 2));
        });
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "123456");

        joined.GetProperty("seq").GetInt64().ShouldBe(2);
        relay.Reads.ShouldBe(1);
    }

    private async Task<(string Deck, ScriptedRelay Relay)> SeedAsync()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set("123456", deck, "s1", AudiencePhase.Live);
        var relay = Factory.Services.GetRequiredService<ScriptedRelay>();
        relay.Reset();
        Factory.Services.GetRequiredService<HookedSessions>().SetOnFirstForDeck(null);
        return (deck, relay);
    }

    private static AudienceState StateAt(string deck, long seq) => new(deck, seq, AudiencePhase.Live, null, 0, null, null, null);

    // The fake sessions, plus a hook that runs once on the first ForDeckAsync (JoinAudience's existence check).
    private sealed class HookedSessions(FakeAudienceSessions inner) : IAudienceSessions
    {
        private Func<string, Task>? _hook;

        public void SetOnFirstForDeck(Func<string, Task>? hook) => Volatile.Write(ref _hook, hook);

        public Task<AudienceSessionRef?> FindAsync(string code) => inner.FindAsync(code);

        public async Task<AudienceSessionRef?> ForDeckAsync(string deckId)
        {
            var hook = Interlocked.Exchange(ref _hook, null);
            if (hook != null)
            {
                await hook(deckId);
            }
            return await inner.ForDeckAsync(deckId);
        }
    }

    // Answers each CurrentAsync read from a script (the read's number, from 1, and the deck), so a test decides what each of
    // JoinAudience's two reads sees.
    private sealed class ScriptedRelay : IAudienceRelay
    {
        private int _reads;

        public Func<int, string, Task<AudienceState?>> Script { get; set; } = (_, _) => Task.FromResult<AudienceState?>(null);

        public int Reads => Volatile.Read(ref _reads);

        public void Reset()
        {
            Volatile.Write(ref _reads, 0);
        }

        public Task<AudienceState?> CurrentAsync(string deckId) => Script(Interlocked.Increment(ref _reads), deckId);

        public Task StartedAsync(string deckId) => Task.CompletedTask;

        public Task EndedAsync(string deckId) => Task.CompletedTask;

        public Task RefreshAsync(string deckId) => Task.CompletedTask;
    }
}
