using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// The registry's durations run on the monotonic clock, so a wall-clock step (NTP, a VM or WSL clock sync) changes none
// of them: the running talk timer, the 2-hour keep and the live list's started label. The host's TimeProvider is a
// WallStepClock over Clock: Clock.Advance moves both clocks, Wall.StepWall moves only the wall clock.
[TestFixture]
[CancelAfter(30000)]
public class PresentationWallClockTests : ApiTestFixture
{
    private readonly List<PresentationStates> _connections = [];

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services
        .AddSingleton<TimeProvider>(new WallStepClock(Clock));

    private WallStepClock Wall => (WallStepClock)Factory.Services.GetRequiredService<TimeProvider>();

    private IPresentations Registry => Factory.Services.GetRequiredService<IPresentations>();

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }

    // The acceptance run that found it: a resume at 4216 ms, 4224 ms of real time, and the wall clock stepped back about
    // 1067 ms in between. The timer must read 8440, not 7373.
    [Test]
    public async Task SetTimer_WhenTheWallClockStepsBackWhileARunningTimerRuns_ShouldCountOnlyTheTimeThatPassed()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1", resume: new { slide = 2, step = 0, black = false, skipDeepDives = false, timer = new { state = "running", elapsedMs = 4216L } });

        Clock.Advance(TimeSpan.FromMilliseconds(4224));
        Wall.StepWall(TimeSpan.FromMilliseconds(-1067));
        var paused = (await owner.SetTimerAsync(deck, "pause")).GetProperty("timer");

        (paused.GetProperty("state").GetString(), paused.GetProperty("elapsedMs").GetInt64()).ShouldBe(("paused", 8440L));
    }

    // The timer started by the first slide change gains nothing when the wall clock steps forward.
    [Test]
    public async Task UpdatePresentation_WhenTheWallClockStepsForwardAfterTheTimerStarted_ShouldCountOnlyTheTimeThatPassed()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");
        await owner.UpdateAsync(deck, 2, 0);

        Clock.Advance(TimeSpan.FromSeconds(30));
        Wall.StepWall(TimeSpan.FromMinutes(5));
        var state = await owner.UpdateAsync(deck, 3, 0);

        var timer = state.GetProperty("timer");
        (timer.GetProperty("state").GetString(), timer.GetProperty("elapsedMs").GetInt64()).ShouldBe(("running", 30000L));
    }

    // D9's 2-hour keep counts time that passed, so a wall-clock step past it doesn't drop a state left a minute ago.
    [Test]
    public async Task GetAsync_WhenTheWallClockStepsPastTheTwoHourKeep_ShouldKeepTheState()
    {
        var deck = await OwnDeckAsync();

        var started = await Registry.StartAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(1));
        Wall.StepWall(Presentations.KEEP_IDLE);
        var kept = await Registry.GetAsync(deck);

        kept.ShouldNotBeNull();
        kept.Epoch.ShouldBe(started.Epoch);
    }

    // P4's startedLabel is the state's age, so a wall-clock step doesn't age it.
    [Test]
    public async Task ListLive_WhenTheWallClockStepsForwardAfterTheJoin_ShouldLabelItByTheTimeThatPassed()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");

        Clock.Advance(TimeSpan.FromMinutes(3));
        Wall.StepWall(TimeSpan.FromMinutes(30));
        var response = await Client.GetAsync("/api/presentations");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var live = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presentations").EnumerateArray()
            .Single(p => p.GetProperty("deckId").GetString() == deck);
        live.GetProperty("startedLabel").GetString().ShouldBe("started 3 min ago");
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
