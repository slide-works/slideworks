using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// The real roster behind the real hub (J4, J8, D22): guests count once per browser, joins and the peak are written to the
// session, and the owner hears the count at most once a second. Sam owns every deck and the clock is fake, so the throttle
// steps with it.
[TestFixture]
[CancelAfter(60000)]
public class AudienceRosterTests : ApiTestFixture
{
    private readonly List<IAsyncDisposable> _connections = [];

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }

    [Test]
    public async Task JoinAudience_WhenSameGuestTwoTabs_ShouldCountOnce()
    {
        var (deck, code, session) = await GoLiveAsync();
        using var counts = new CountFeed(Bus, deck);
        var guestId = Guid.NewGuid().ToString("N");
        var first = await JoinAsync(code, guestId);
        var second = await JoinAsync(code, guestId);

        var countWithTwoTabs = await Roster.CountAsync(session);
        await first.DisposeAsync();
        await counts.NextAsync();
        await counts.NextAsync();
        var countAfterFirstLeaves = await counts.NextAsync();
        var countWithOneTab = await Roster.CountAsync(session);
        await second.DisposeAsync();
        var countAfterBoth = await counts.NextAsync();

        countWithTwoTabs.ShouldBe(1);
        countAfterFirstLeaves.ShouldBe(1);
        countWithOneTab.ShouldBe(1);
        countAfterBoth.ShouldBe(0);
        (await JoinRowsAsync(session)).ShouldBe([guestId]);
        (await PeakAsync(session)).ShouldBe(1);
    }

    [Test]
    public async Task JoinAudience_WhenGuestReconnects_ShouldNotWriteSecondJoinRow()
    {
        var (deck, code, session) = await GoLiveAsync();
        using var counts = new CountFeed(Bus, deck);
        var guestId = Guid.NewGuid().ToString("N");
        var first = await JoinAsync(code, guestId);
        await counts.NextAsync();
        await first.DisposeAsync();
        (await counts.NextAsync()).ShouldBe(0);
        Clock.Advance(TimeSpan.FromSeconds(30));

        await JoinAsync(code, guestId, HttpTransportType.LongPolling);

        (await JoinRowsAsync(session)).ShouldBe([guestId]);
        (await Roster.CountAsync(session)).ShouldBe(1);
    }

    [Test]
    public async Task JoinedCount_WhenTwentyJoinWithinASecond_ShouldSendOneEvent()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var owner = await WatcherAsync(deck);
        var live = await PostLiveAsync(deck);
        var code = live.GetProperty("code").GetString()!;
        // Go live sent its count at once, which opens the second the joins fall inside.
        (await owner.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(0);
        for (var i = 0; i < 20; i++)
        {
            await JoinAsync(code);
        }
        await PassTheRelayAsync();
        Clock.Advance(TimeSpan.FromSeconds(1));
        var burst = await owner.NextAudienceAsync();
        await JoinAsync(code);
        await PassTheRelayAsync();
        Clock.Advance(TimeSpan.FromSeconds(1));
        var next = await owner.NextAudienceAsync();

        burst.GetProperty("count").GetInt32().ShouldBe(20);
        next.GetProperty("count").GetInt32().ShouldBe(21);
        (await Roster.CountAsync(live.GetProperty("sessionId").GetString()!)).ShouldBe(21);
    }

    [Test]
    public async Task Peak_WhenGuestsLeave_ShouldKeepMaximum()
    {
        var (deck, code, session) = await GoLiveAsync();
        using var counts = new CountFeed(Bus, deck);
        var guests = new List<HubConnection>();
        for (var i = 0; i < 3; i++)
        {
            guests.Add((await JoinAsync(code)).Connection);
        }
        for (var i = 0; i < 3; i++)
        {
            await counts.NextAsync();
        }
        await guests[0].StopAsync();
        await guests[1].StopAsync();
        await counts.NextAsync();
        (await counts.NextAsync()).ShouldBe(1);

        (await PeakAsync(session)).ShouldBe(3);
        (await JoinRowsAsync(session)).Count.ShouldBe(3);
    }

    // D22: guests who join before the session opens (the deck has a join slide, so the code opens a lobby) are counted, and
    // written as joined, when Go live opens it, and the owner is told.
    [Test]
    public async Task OpenedAsync_WhenGuestsWaitedInTheLobby_ShouldCountThemWriteTheirJoinsAndPublish()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
        var code = (await GetLiveAsync(deck)).GetProperty("code").GetString()!;
        var first = Guid.NewGuid().ToString("N");
        var second = Guid.NewGuid().ToString("N");
        await JoinAsync(code, first);
        await JoinAsync(code, second);
        await JoinAsync(code, second);
        var owner = await WatcherAsync(deck);

        var live = await PostLiveAsync(deck);

        var session = live.GetProperty("sessionId").GetString()!;
        live.GetProperty("joinedCount").GetInt32().ShouldBe(2);
        (await owner.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(2);
        (await JoinRowsAsync(session)).Order().ShouldBe(new[] { first, second }.Order());
        (await PeakAsync(session)).ShouldBe(2);
    }

    private IAudienceRoster Roster => Factory.Services.GetRequiredService<IAudienceRoster>();

    private IAudienceEvents Bus => Factory.Services.GetRequiredService<IAudienceEvents>();

    private async Task<(string Deck, string Code, string Session)> GoLiveAsync()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var live = await PostLiveAsync(deck);
        return (deck, live.GetProperty("code").GetString()!, live.GetProperty("sessionId").GetString()!);
    }

    private async Task<JsonElement> PostLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        response.IsSuccessStatusCode.ShouldBeTrue();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<JsonElement> GetLiveAsync(string deck)
    {
        var response = await Client.GetAsync($"/api/decks/{deck}/live");
        response.IsSuccessStatusCode.ShouldBeTrue();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<GuestClient> JoinAsync(string code, string? guestId = null, HttpTransportType transport = HttpTransportType.WebSockets)
    {
        var guest = await GuestClient.ConnectAsync(Factory, transport, guestId);
        _connections.Add(guest);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        return guest;
    }

    private async Task<HubReceiver> WatcherAsync(string deck)
    {
        var owner = await HubReceiver.StartAsync(Factory, SignedInAs);
        _connections.Add(owner);
        await owner.Connection.InvokeAsync("WatchAudience", deck);
        return owner;
    }

    private async Task<List<string>> JoinRowsAsync(string session)
    {
        var id = Guid.Parse(session);
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AudienceJoins.Where(j => j.SessionId == id).Select(j => j.GuestId).ToListAsync();
    }

    private async Task<int> PeakAsync(string session)
    {
        var id = Guid.Parse(session);
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AudienceSessions.Where(s => s.Id == id).Select(s => s.PeakJoined).SingleAsync();
    }

    // The relay forwards events in order on one task, so once it has sent a first count for a deck nobody has counted yet it
    // has handled everything published before it. Each call uses a new deck.
    private async Task PassTheRelayAsync()
    {
        var marker = await SharingApi.CreateDeckAsync(Client);
        var watcher = await WatcherAsync(marker);
        await Bus.PublishAsync(new JoinedCount(marker, 0));
        (await watcher.NextAudienceAsync()).GetProperty("count").GetInt32().ShouldBe(0);
    }

    // The joinedCount events published on the bus for a deck, in order: a barrier for "the hub has handled that join or leave".
    private sealed class CountFeed : IDisposable
    {
        private readonly IDisposable _subscription;
        private readonly Channel<int> _counts = Channel.CreateUnbounded<int>();

        public CountFeed(IAudienceEvents bus, string deck)
        {
            _subscription = bus.Subscribe(deck, e => e is JoinedCount count ? WriteAsync(count.Count) : Task.CompletedTask);
        }

        // Reads the next count: each join and each leave on the deck is one.
        public async Task<int> NextAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await _counts.Reader.ReadAsync(timeout.Token);
        }

        public void Dispose() => _subscription.Dispose();

        private Task WriteAsync(int count)
        {
            _counts.Writer.TryWrite(count);
            return Task.CompletedTask;
        }
    }
}
