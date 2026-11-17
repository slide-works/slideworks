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
        services.AddSingleton<ReadsThenVanishes>().AddSingleton<IAudienceRelay>(provider => provider.GetRequiredService<ReadsThenVanishes>());
    };

    [Test]
    public async Task JoinAudience_WhenStateVanishesAfterTheGuestJoinedTheGroup_ShouldRefuseAsNoSessionAndLeaveTheGroup()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set("123456", deck, "s1", AudiencePhase.Live);
        var relay = Factory.Services.GetRequiredService<ReadsThenVanishes>();
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<object>("JoinAudience", "123456"));

        refused.Message.ShouldContain(DeckHub.NO_SESSION);
        relay.Reads.ShouldBe(2);
    }

    private sealed class ReadsThenVanishes : IAudienceRelay
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public Task<AudienceState?> CurrentAsync(string deckId)
            => Task.FromResult<AudienceState?>(Interlocked.Increment(ref _reads) == 1 ? new AudienceState(deckId, 1, AudiencePhase.Live, null, 0, null) : null);

        public Task StartedAsync(string deckId) => Task.CompletedTask;

        public Task EndedAsync(string deckId) => Task.CompletedTask;
    }
}
