using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Sharing;

namespace SlideWorks.WebApi.Tests.Join.GuestHeader;

// /hubs/deck for the app (K7): a WebSocket can't send a header, so the app's guest id rides in the guest query, and the hub's
// guest calls read it through the same reader as the cookie. A malformed id is no guest.
[TestFixture]
[CancelAfter(30000)]
public class GuestHeaderHubTests : JoinFixture
{
    private const string GUEST = "0123456789abcdef0123456789abcdef";

    [Test]
    public async Task Hub_WhenGuestQuery_ShouldJoinAudience()
    {
        Open(CODE, await SharingApi.CreateDeckAsync(Client));
        await using var guest = await HubReceiver.StartAsync(Factory, new HubReceiverOptions { Guest = GUEST });

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", CODE);

        joined.GetProperty("seq").GetInt64().ShouldBe(1);
    }

    [Test]
    public async Task Hub_WhenGuestQueryMalformed_ShouldRefuseNoGuest()
    {
        Open(CODE, await SharingApi.CreateDeckAsync(Client));
        await using var guest = await HubReceiver.StartAsync(Factory, new HubReceiverOptions { Guest = GUEST.ToUpperInvariant() });

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("JoinAudience", CODE));

        refused.Message.ShouldContain(DeckHub.NO_GUEST, Case.Sensitive);
    }
}
