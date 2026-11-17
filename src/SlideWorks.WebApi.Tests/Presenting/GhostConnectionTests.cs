using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// A connection that closes while its JoinPresentation is still running (P2: "a closed connection leaves every
// presentation"). SignalR runs OnDisconnectedAsync, and so LeaveAllAsync, without waiting for the connection's in-flight
// hub calls, so the leave can run before the join has added its entry. The join is held at the access check (JoinGate),
// the connection is closed, the server's disconnect is awaited (DisconnectWatch), then the join is let go and awaited on
// the server (InvocationWatch), which also gives the exception the hub method threw. The closed client's own task is no
// evidence (it fails once the client is disposed, whatever the server did), so the tests read the server's refusal
// instead. No step waits on a message order.
[TestFixture]
[CancelAfter(30000)]
public class GhostConnectionTests : ApiTestFixture
{
    private readonly List<PresentationStates> _connections = [];
    private readonly DisconnectWatch _disconnects = new();
    private readonly InvocationWatch _invocations = new();
    private JoinGate _gate = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services
        .Configure<HubOptions>(options =>
        {
            options.AddFilter(_disconnects);
            options.AddFilter(_invocations);
        })
        .AddScoped<DeckAccess>()
        .AddScoped<IDeckAccess>(provider => new GatedDeckAccess(provider.GetRequiredService<DeckAccess>(), _gate));

    private IPresentations Registry => Factory.Services.GetRequiredService<IPresentations>();

    [SetUp]
    public void NewGate() => _gate = new JoinGate();

    [TearDown]
    public async Task DisconnectAsync()
    {
        _gate.Release();
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }

    // The deck already has an idle state (its presenter joined and left). The closed connection's join must add no entry
    // and raise no seq, the live list must not show the deck, and the state must still be dropped after the 2-hour keep.
    [Test]
    public async Task JoinPresentation_WhenTheConnectionClosesMidJoin_ShouldLeaveNoGhostEntry()
    {
        var deck = await OwnDeckAsync();
        var presenter = await ConnectAsync(TestUser.Sam);
        await presenter.JoinAsync(deck, "k-1");
        await presenter.LeaveAsync(deck);
        var idle = await Registry.GetAsync(deck);
        var closing = await ConnectAsync(TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;

        _gate.Arm(deck);
        _ = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        _gate.Release();
        var refused = await _invocations.FinishedAsync(closingId, "JoinPresentation");
        var after = await Registry.GetAsync(deck);
        var listed = await LiveDeckIdsAsync();
        Clock.Advance(Presentations.KEEP_IDLE + TimeSpan.FromMinutes(1));
        var dropped = await Registry.GetAsync(deck);

        refused.ShouldBeOfType<HubException>().Message.ShouldBe("Connection closed");
        idle.ShouldNotBeNull();
        after.ShouldNotBeNull();
        after.Connected.ShouldBeEmpty();
        (after.Epoch, after.Seq).ShouldBe((idle.Epoch, idle.Seq));
        listed.ShouldNotContain(deck);
        dropped.ShouldBeNull();
    }

    // With no state yet, the closed connection's join creates none.
    [Test]
    public async Task JoinPresentation_WhenTheConnectionClosesMidJoinOnANewDeck_ShouldCreateNoState()
    {
        var deck = await OwnDeckAsync();
        var closing = await ConnectAsync(TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;

        _gate.Arm(deck);
        _ = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        _gate.Release();
        var refused = await _invocations.FinishedAsync(closingId, "JoinPresentation");
        var after = await Registry.GetAsync(deck);
        var listed = await LiveDeckIdsAsync();

        refused.ShouldBeOfType<HubException>().Message.ShouldBe("Connection closed");
        after.ShouldBeNull();
        listed.ShouldNotContain(deck);
    }

    // The fix must not refuse a live connection: one that joins while another connection of the same person closes
    // mid-join still gets its entry, and its later update is one seq after its join.
    [Test]
    public async Task JoinPresentation_WhenAnotherConnectionClosesMidJoin_ShouldStillJoinTheLiveOne()
    {
        var deck = await OwnDeckAsync();
        var closing = await ConnectAsync(TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;
        var presenter = await ConnectAsync(TestUser.Sam);

        _gate.Arm(deck);
        _ = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        var joined = (await presenter.JoinAsync(deck, "k-1")).GetProperty("state");
        _gate.Release();
        var refused = await _invocations.FinishedAsync(closingId, "JoinPresentation");
        var next = await presenter.UpdateAsync(deck, 2, 0);

        refused.ShouldBeOfType<HubException>().Message.ShouldBe("Connection closed");
        PresentationStates.ClientIds(next).ShouldBe(["k-1"]);
        next.GetProperty("seq").GetInt64().ShouldBe(joined.GetProperty("seq").GetInt64() + 1);
        (await LiveDeckIdsAsync()).ShouldContain(deck);
    }

    // The other order: the join finishes, and so is indexed, before its connection closes. The disconnect removes the
    // entry, raises seq by one, and sends the smaller connected list to the rest of the group. The test waits on the
    // server's disconnect (DisconnectWatch) and on the known seq, not on message order.
    [Test]
    public async Task Disconnect_WhenTheJoinFinishedFirst_ShouldRemoveTheEntryAndTellTheRest()
    {
        var deck = await OwnDeckAsync();
        var stays = await ConnectAsync(TestUser.Sam);
        var drops = await ConnectAsync(TestUser.Sam);
        var dropsId = drops.Connection.ConnectionId!;
        await stays.JoinAsync(deck, "k-1");
        var joined = (await drops.JoinAsync(deck, "k-2", "audience")).GetProperty("state");
        var joinSeq = joined.GetProperty("seq").GetInt64();

        await drops.DisposeAsync();
        await _disconnects.DisconnectedAsync(dropsId);
        var after = await Registry.GetAsync(deck);
        var told = await StateAtSeqAsync(stays, joinSeq + 1);

        PresentationStates.ClientIds(joined).ShouldBe(["k-1", "k-2"]);
        after.ShouldNotBeNull();
        after.Connected.Select(c => c.ClientId).ShouldBe(["k-1"]);
        after.Seq.ShouldBe(joinSeq + 1);
        told.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 1);
        PresentationStates.ClientIds(told).ShouldBe(["k-1"]);
    }

    // The closed mark lasts KEEP_CLOSED on the registry's monotonic clock: a disconnect just inside it (which prunes old
    // marks) leaves the mark in place, so the late join is still refused.
    [Test]
    public async Task JoinPresentation_WhenTheMarkIsYoungerThanKeepClosed_ShouldStillRefuse()
    {
        var deck = await OwnDeckAsync();
        var closing = await ConnectAsync(TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;
        var pruner = await ConnectAsync(TestUser.Sam);
        var prunerId = pruner.Connection.ConnectionId!;

        _gate.Arm(deck);
        _ = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        Clock.Advance(Presentations.KEEP_CLOSED - TimeSpan.FromMinutes(1));
        await pruner.DisposeAsync();
        await _disconnects.DisconnectedAsync(prunerId);
        _gate.Release();
        var refused = await _invocations.FinishedAsync(closingId, "JoinPresentation");
        var after = await Registry.GetAsync(deck);

        refused.ShouldBeOfType<HubException>().Message.ShouldBe("Connection closed");
        after.ShouldBeNull();
    }

    // Past KEEP_CLOSED the next disconnect prunes the mark, so a join held that long is no longer refused. This is the
    // known gap D17 accepts (closing it needs Context.ConnectionAborted through the frozen DeckHub); the test pins that
    // the mark really expires, so the marks stay bounded.
    [Test]
    public async Task JoinPresentation_WhenTheJoinOutlivesKeepClosed_ShouldNoLongerBeRefused()
    {
        var deck = await OwnDeckAsync();
        var closing = await ConnectAsync(TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;
        var pruner = await ConnectAsync(TestUser.Sam);
        var prunerId = pruner.Connection.ConnectionId!;

        _gate.Arm(deck);
        _ = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        Clock.Advance(Presentations.KEEP_CLOSED);
        await pruner.DisposeAsync();
        await _disconnects.DisconnectedAsync(prunerId);
        _gate.Release();
        var outcome = await _invocations.FinishedAsync(closingId, "JoinPresentation");
        var after = await Registry.GetAsync(deck);

        outcome.ShouldBeNull();
        after.ShouldNotBeNull();
        after.Connected.Select(c => c.ClientId).ShouldBe(["k-ghost"]);
    }

    // Reads this connection's states up to the first one at or past seq. States reach a connection in seq order (they are
    // sent under the deck's lock), so this waits on a known seq rather than on how many messages came first.
    private static async Task<JsonElement> StateAtSeqAsync(PresentationStates connection, long seq)
    {
        var state = await connection.NextAsync();
        while (state.GetProperty("seq").GetInt64() < seq)
        {
            state = await connection.NextAsync();
        }
        return state;
    }

    private async Task<IReadOnlyList<string>> LiveDeckIdsAsync()
    {
        var view = await Client.GetFromJsonAsync<JsonElement>("/api/presentations");
        return view.GetProperty("presentations").EnumerateArray().Select(p => p.GetProperty("deckId").GetString()!).ToList();
    }

    private async Task<string> OwnDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return await PresentingSeed.CreateDeckAsync(agent);
    }

    private async Task<PresentationStates> ConnectAsync(TestUser user)
    {
        var connection = await PresentationStates.ConnectAsync(Factory, user);
        _connections.Add(connection);
        return connection;
    }
}
