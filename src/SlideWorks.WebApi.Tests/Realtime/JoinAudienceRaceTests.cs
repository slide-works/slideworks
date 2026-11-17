using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// JoinAudience reads the state again once the guest is in the group (SW-205 review), so a state published in the gap is not
// missed, and a guest whose second read finds nothing is taken out of the group again and refused.
[TestFixture]
[CancelAfter(30000)]
public class JoinAudienceRaceTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        services.AddSingleton<ScriptedRelay>().AddSingleton<IAudienceRelay>(provider => provider.GetRequiredService<ScriptedRelay>());
    };

    [Test]
    public async Task JoinAudience_WhenStateVanishesAfterTheGuestJoinedTheGroup_ShouldRefuseAsNoSessionAndLeaveTheGroup()
    {
        var (deck, relay) = await SeedAsync();
        relay.Script = (read, id) => Task.FromResult(read == 1 ? StateAt(id, seq: 1) : null);
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
        // The presenter moves in the gap: after the first read, before the guest is in the group, so the push itself is missed.
        relay.Script = async (read, id) =>
        {
            if (read == 1)
            {
                await bus.PublishAsync(StateAt(id, seq: 2));
                return StateAt(id, seq: 1);
            }
            return StateAt(id, seq: 2);
        };
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "123456");

        joined.GetProperty("seq").GetInt64().ShouldBe(2);
        relay.Reads.ShouldBe(2);
    }

    private async Task<(string Deck, ScriptedRelay Relay)> SeedAsync()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set("123456", deck, "s1", AudiencePhase.Live);
        var relay = Factory.Services.GetRequiredService<ScriptedRelay>();
        relay.Reset();
        return (deck, relay);
    }

    private static AudienceState StateAt(string deck, long seq) => new(deck, seq, AudiencePhase.Live, null, 0, null);

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
    }
}
