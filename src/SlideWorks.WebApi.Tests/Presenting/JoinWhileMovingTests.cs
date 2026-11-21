using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.Timing;

namespace SlideWorks.WebApi.Tests.Presenting;

// docs/testing.md's interleaving checklist for the hub registry: a push event lands while a join is out. The second
// window's JoinPresentation is held at the access check (Gate), the presenter moves on meanwhile, then the join is let go.
// Invariant: a join that finishes after a move sees that move, with a later seq, and the presenter hears of the joiner.
[TestFixture]
public class JoinWhileMovingTests : ApiTestFixture
{
    private readonly List<PresentationStates> _connections = [];
    private Gate _gate = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services
        .AddScoped<DeckAccess>()
        .AddScoped<IDeckAccess>(provider => new GatedDeckAccess(provider.GetRequiredService<DeckAccess>(), _gate));

    [SetUp]
    public void NewGate() => _gate = new Gate();

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

    [Test]
    public async Task JoinPresentation_WhenThePresenterMovesMidJoin_ShouldJoinOnTheMovedSlide()
    {
        var deck = await OwnDeckAsync();
        var presenter = await ConnectAsync(TestUser.Sam);
        var audience = await ConnectAsync(TestUser.Sam);
        await presenter.JoinAsync(deck, "k-presenter");

        _gate.Arm(deck);
        var join = audience.JoinAsync(deck, "k-audience", "audience");
        await _gate.EnteredAsync();
        var moved = await presenter.UpdateAsync(deck, 2, 0);
        _gate.Release();
        var joined = (await join).GetProperty("state");

        joined.GetProperty("slide").GetInt32().ShouldBe(2);
        joined.GetProperty("seq").GetInt64().ShouldBeGreaterThan(moved.GetProperty("seq").GetInt64());
        PresentationStates.ClientIds(joined).ShouldBe(["k-presenter", "k-audience"]);
    }

    private async Task<PresentationStates> ConnectAsync(TestUser user)
    {
        var connection = await PresentationStates.ConnectAsync(Factory, user);
        _connections.Add(connection);
        return connection;
    }

    private async Task<string> OwnDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return await PresentingSeed.CreateDeckAsync(agent);
    }
}
