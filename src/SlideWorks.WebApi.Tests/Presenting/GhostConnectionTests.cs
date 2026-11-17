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
// the server (InvocationWatch). No step waits on a message order.
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
        var closing = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;

        _gate.Arm(deck);
        var join = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        _gate.Release();
        await _invocations.FinishedAsync(closingId, "JoinPresentation");
        await Task.WhenAny(join);
        var after = await Registry.GetAsync(deck);
        var listed = await LiveDeckIdsAsync();
        Clock.Advance(Presentations.KEEP_IDLE + TimeSpan.FromMinutes(1));
        var dropped = await Registry.GetAsync(deck);

        join.IsCompletedSuccessfully.ShouldBeFalse();
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
        var closing = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;

        _gate.Arm(deck);
        var join = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        _gate.Release();
        await _invocations.FinishedAsync(closingId, "JoinPresentation");
        await Task.WhenAny(join);
        var after = await Registry.GetAsync(deck);
        var listed = await LiveDeckIdsAsync();

        join.IsCompletedSuccessfully.ShouldBeFalse();
        after.ShouldBeNull();
        listed.ShouldNotContain(deck);
    }

    // The fix must not refuse a live connection: one that joins while another connection of the same person closes
    // mid-join still gets its entry, and its later update is one seq after its join.
    [Test]
    public async Task JoinPresentation_WhenAnotherConnectionClosesMidJoin_ShouldStillJoinTheLiveOne()
    {
        var deck = await OwnDeckAsync();
        var closing = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);
        var closingId = closing.Connection.ConnectionId!;
        var presenter = await ConnectAsync(TestUser.Sam);

        _gate.Arm(deck);
        var join = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        var joined = (await presenter.JoinAsync(deck, "k-1")).GetProperty("state");
        _gate.Release();
        await _invocations.FinishedAsync(closingId, "JoinPresentation");
        await Task.WhenAny(join);
        var next = await presenter.UpdateAsync(deck, 2, 0);

        join.IsCompletedSuccessfully.ShouldBeFalse();
        PresentationStates.ClientIds(next).ShouldBe(["k-1"]);
        next.GetProperty("seq").GetInt64().ShouldBe(joined.GetProperty("seq").GetInt64() + 1);
        (await LiveDeckIdsAsync()).ShouldContain(deck);
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
