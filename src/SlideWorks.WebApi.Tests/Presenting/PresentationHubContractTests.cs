using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// P2's rules, one test each, over real HubConnections against the real registry. Sam owns every deck here; the deck has
// PresentingSeed.SLIDES slides. Time is the fixture's FakeTimeProvider, stepped with Advance.
[TestFixture]
[CancelAfter(30000)]
public class PresentationHubContractTests : ApiTestFixture
{
    private readonly List<PresentationStates> _connections = [];
    private readonly DisconnectWatch _disconnects = new();

    protected override Action<IServiceCollection>? ConfigureTestServices
        => services => services.Configure<HubOptions>(options => options.AddFilter(_disconnects));

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
    public async Task JoinPresentation_WhenOwnerAndNoState_ShouldStartAtSlideOneWithTheClock()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var joined = await owner.JoinAsync(deck, "k-1");

        var state = joined.GetProperty("state");
        (state.GetProperty("slide").GetInt32(), state.GetProperty("step").GetInt32()).ShouldBe((1, 0));
        state.GetProperty("timer").GetProperty("state").GetString().ShouldBe("notStarted");
        PresentationStates.ClientIds(state).ShouldBe(["k-1"]);
        joined.GetProperty("clockMinutes").GetInt32().ShouldBe(MinutesSinceMidnight(PresentationStates.TIME_ZONE));
    }

    [Test]
    public async Task JoinPresentation_WhenUnknownTimeZone_ShouldUseUtc()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var joined = await owner.JoinAsync(deck, "k-1", timeZone: "Not/AZone");

        joined.GetProperty("clockMinutes").GetInt32().ShouldBe(MinutesSinceMidnight("UTC"));
    }

    [Test]
    public async Task JoinPresentation_WhenResumeAndNoState_ShouldStartAtResumeWithItsTimerKept()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var joined = await owner.JoinAsync(deck, "k-1", resume: Resume(2, 1, "running", 60000));

        var state = joined.GetProperty("state");
        (state.GetProperty("slide").GetInt32(), state.GetProperty("step").GetInt32()).ShouldBe((2, 1));
        state.GetProperty("skipDeepDives").GetBoolean().ShouldBeTrue();
        state.GetProperty("timer").GetProperty("state").GetString().ShouldBe("running");
        state.GetProperty("timer").GetProperty("elapsedMs").GetInt64().ShouldBe(60000);
    }

    [Test]
    public async Task JoinPresentation_WhenResumeBeyondTheDeck_ShouldClampIt()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var state = (await owner.JoinAsync(deck, "k-1", resume: Resume(99, 9, "paused", 1000))).GetProperty("state");

        (state.GetProperty("slide").GetInt32(), state.GetProperty("step").GetInt32()).ShouldBe((PresentingSeed.SLIDES, 0));
    }

    [Test]
    public async Task JoinPresentation_WhenStateExists_ShouldIgnoreResume()
    {
        var deck = await OwnDeckAsync();
        var first = await ConnectAsync(TestUser.Sam);
        var second = await ConnectAsync(TestUser.Sam);
        await first.JoinAsync(deck, "k-1");

        var state = (await second.JoinAsync(deck, "k-2", resume: Resume(3, 0, "running", 5000))).GetProperty("state");

        state.GetProperty("slide").GetInt32().ShouldBe(1);
        state.GetProperty("timer").GetProperty("state").GetString().ShouldBe("notStarted");
    }

    // The refusal has no side effects: the owner already in the group receives nothing, so its next state is its own
    // update's, one seq after its join.
    [Test]
    public async Task JoinPresentation_WhenNotTheOwner_ShouldBeRefusedAndBroadcastNothing()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        var alex = await ConnectAsync(TestUser.Alex);
        var joinSeq = (await owner.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("seq").GetInt64();
        await owner.NextAsync();

        var refused = await Should.ThrowAsync<HubException>(() => alex.JoinAsync(deck, "k-2"));
        await owner.UpdateAsync(deck, 2, 0);

        refused.Message.ShouldContain(DeckHub.DECK_NOT_FOUND);
        var next = await owner.NextAsync();
        next.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 1);
        PresentationStates.ClientIds(next).ShouldBe(["k-1"]);
    }

    // DeckHub's CanPresentAsync check refuses a non-owner before the registry is called. The owner already joined
    // receives nothing: its next state is its own update's, one seq after its join (order-proof). The refused caller is
    // in no presentation group, so its check uses a deck-event barrier instead. Deck events come through a background
    // relay, so that barrier can be an earlier queued event and the check can pass falsely, though it never fails falsely.
    [TestCaseSource(nameof(NonOwners))]
    public async Task JoinPresentation_WhenNotTheOwner_ShouldBeRefusedBeforeAnythingIsSent(TestUser caller, string level, string[] viewers)
    {
        var deck = await OwnDeckAsync();
        await SharingApi.PutOkAsync(Client, deck, level, viewers);
        var owner = await ConnectAsync(TestUser.Sam);
        var other = await ConnectAsync(caller);
        await owner.Connection.InvokeAsync("JoinDeck", deck);
        await other.Connection.InvokeAsync("JoinDeck", deck);
        var joinSeq = (await owner.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("seq").GetInt64();
        await owner.NextAsync();

        var refused = await Should.ThrowAsync<HubException>(() => other.JoinAsync(deck, "k-2"));
        await Factory.Services.GetRequiredService<IDeckEvents>().PublishAsync(new DeckChanged(deck));
        await other.NextEventAsync();
        await owner.UpdateAsync(deck, 2, 0);

        refused.Message.ShouldContain(DeckHub.DECK_NOT_FOUND);
        other.Received().ShouldBeEmpty();
        var next = await owner.NextAsync();
        next.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 1);
        PresentationStates.ClientIds(next).ShouldBe(["k-1"]);
    }

    // A refused join creates nothing: P10's GetAsync still has no state, and the owner's later join starts a new epoch
    // at its first seq with only the owner listed.
    [Test]
    public async Task JoinPresentation_WhenNotTheOwnerAndNoState_ShouldCreateNoState()
    {
        var deck = await OwnDeckAsync();
        var alex = await ConnectAsync(TestUser.Alex);
        var owner = await ConnectAsync(TestUser.Sam);

        var refused = await Should.ThrowAsync<HubException>(() => alex.JoinAsync(deck, "k-2"));
        var afterRefusal = await Factory.Services.GetRequiredService<IPresentations>().GetAsync(deck);
        var state = (await owner.JoinAsync(deck, "k-1")).GetProperty("state");

        refused.Message.ShouldContain(DeckHub.DECK_NOT_FOUND);
        afterRefusal.ShouldBeNull();
        state.GetProperty("seq").GetInt64().ShouldBe(1);
        PresentationStates.ClientIds(state).ShouldBe(["k-1"]);
    }

    // D4 puts the per-deck rule for Update and SetTimer in the registry: having joined some presentation is not enough.
    // Alex presents his own deck, then tries to drive Sam's live deck; Sam's next state is his own update's.
    [Test]
    public async Task UpdatePresentation_WhenJoinedToAnotherDeckOnly_ShouldBeRefusedWithNotPresenting()
    {
        var samsDeck = await OwnDeckAsync();
        var alexsDeck = await DeckOwnedByAsync(TestUser.Alex);
        var sam = await ConnectAsync(TestUser.Sam);
        var alex = await ConnectAsync(TestUser.Alex);
        var joinSeq = (await sam.JoinAsync(samsDeck, "k-sam")).GetProperty("state").GetProperty("seq").GetInt64();
        await sam.NextAsync();
        await alex.JoinAsync(alexsDeck, "k-alex");

        var update = await Should.ThrowAsync<HubException>(() => alex.UpdateAsync(samsDeck, 2, 0));
        var timer = await Should.ThrowAsync<HubException>(() => alex.SetTimerAsync(samsDeck, "pause"));
        await sam.UpdateAsync(samsDeck, 3, 0);

        update.Message.ShouldContain(DeckHub.NOT_PRESENTING);
        timer.Message.ShouldContain(DeckHub.NOT_PRESENTING);
        var next = await sam.NextAsync();
        next.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 1);
        next.GetProperty("slide").GetInt32().ShouldBe(3);
        next.GetProperty("timer").GetProperty("state").GetString().ShouldBe("running");
    }

    [Test]
    public async Task JoinPresentation_WhenSameClientIdJoinsAgain_ShouldReplaceTheEntry()
    {
        var deck = await OwnDeckAsync();
        var first = await ConnectAsync(TestUser.Sam);
        var second = await ConnectAsync(TestUser.Sam);
        await first.JoinAsync(deck, "k-1");

        var state = (await second.JoinAsync(deck, "k-1", "audience", label: "Firefox on Linux")).GetProperty("state");

        var connected = state.GetProperty("connected").EnumerateArray().ToList();
        connected.Count.ShouldBe(1);
        connected[0].GetProperty("role").GetString().ShouldBe("audience");
        connected[0].GetProperty("label").GetString().ShouldBe("Firefox on Linux");
    }

    [Test]
    public async Task UpdatePresentation_WhenBeyondTheDeck_ShouldClampSlideAndStep()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");

        var high = await owner.UpdateAsync(deck, 2, 99);
        var past = await owner.UpdateAsync(deck, 99, 0);
        var low = await owner.UpdateAsync(deck, 0, -1);

        (high.GetProperty("slide").GetInt32(), high.GetProperty("step").GetInt32()).ShouldBe((2, PresentingSeed.BULLETS_STEPS));
        past.GetProperty("slide").GetInt32().ShouldBe(PresentingSeed.SLIDES);
        (low.GetProperty("slide").GetInt32(), low.GetProperty("step").GetInt32()).ShouldBe((1, 0));
    }

    [Test]
    public async Task UpdatePresentation_WhenFirstMove_ShouldStartTheTimer()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");

        var blackOnly = await owner.UpdateAsync(deck, 1, 0, black: true);
        var moved = await owner.UpdateAsync(deck, 2, 0);

        blackOnly.GetProperty("timer").GetProperty("state").GetString().ShouldBe("notStarted");
        moved.GetProperty("timer").GetProperty("state").GetString().ShouldBe("running");
    }

    [Test]
    public async Task UpdatePresentation_WhenNotJoined_ShouldBeRefusedWithNotPresenting()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var update = await Should.ThrowAsync<HubException>(() => owner.UpdateAsync(deck, 2, 0));
        var timer = await Should.ThrowAsync<HubException>(() => owner.SetTimerAsync(deck, "pause"));

        update.Message.ShouldContain(DeckHub.NOT_PRESENTING);
        timer.Message.ShouldContain(DeckHub.NOT_PRESENTING);
    }

    [Test]
    public async Task SetTimer_WhenPausedResumedAndReset_ShouldFollowTheCommands()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");
        await owner.UpdateAsync(deck, 2, 0);

        Clock.Advance(TimeSpan.FromSeconds(10));
        var paused = (await owner.SetTimerAsync(deck, "pause")).GetProperty("timer");
        Clock.Advance(TimeSpan.FromSeconds(5));
        var resumed = (await owner.SetTimerAsync(deck, "resume")).GetProperty("timer");
        var reset = (await owner.SetTimerAsync(deck, "reset")).GetProperty("timer");

        (paused.GetProperty("state").GetString(), paused.GetProperty("elapsedMs").GetInt64()).ShouldBe(("paused", 10000L));
        (resumed.GetProperty("state").GetString(), resumed.GetProperty("elapsedMs").GetInt64()).ShouldBe(("running", 10000L));
        (reset.GetProperty("state").GetString(), reset.GetProperty("elapsedMs").GetInt64()).ShouldBe(("notStarted", 0L));
    }

    [Test]
    public async Task Seq_WhenChangesAreAccepted_ShouldRiseByOneWithinTheEpoch()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var joined = (await owner.JoinAsync(deck, "k-1")).GetProperty("state");
        var updated = await owner.UpdateAsync(deck, 2, 0);
        var paused = await owner.SetTimerAsync(deck, "pause");

        var epoch = joined.GetProperty("epoch").GetString();
        new[] { updated, paused }.Select(s => s.GetProperty("epoch").GetString()).ShouldAllBe(e => e == epoch);
        updated.GetProperty("seq").GetInt64().ShouldBe(joined.GetProperty("seq").GetInt64() + 1);
        paused.GetProperty("seq").GetInt64().ShouldBe(joined.GetProperty("seq").GetInt64() + 2);
    }

    // D9: the state outlives its last connection by 2 hours, so a rejoin inside them keeps the epoch and one after them
    // starts a new one.
    [Test]
    public async Task JoinPresentation_WhenRejoinedAfterTheTwoHourDrop_ShouldStartANewEpoch()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        var first = (await owner.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("epoch").GetString();
        await owner.LeaveAsync(deck);

        Clock.Advance(TimeSpan.FromMinutes(119));
        var kept = (await owner.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("epoch").GetString();
        await owner.LeaveAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(121));
        var fresh = (await owner.JoinAsync(deck, "k-1")).GetProperty("state");

        kept.ShouldBe(first);
        fresh.GetProperty("epoch").GetString().ShouldNotBe(first);
        fresh.GetProperty("slide").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task LeavePresentation_WhenOneLeavesAndOneDisconnects_ShouldShrinkConnectedForTheRest()
    {
        var deck = await OwnDeckAsync();
        var stays = await ConnectAsync(TestUser.Sam);
        var leaves = await ConnectAsync(TestUser.Sam);
        var drops = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);
        await stays.JoinAsync(deck, "k-1");
        await leaves.JoinAsync(deck, "k-2", "audience");
        await drops.JoinAsync(deck, "k-3", "phone");
        await DrainAsync(stays, 3);

        await leaves.LeaveAsync(deck);
        var afterLeave = await stays.NextAsync();
        await drops.DisposeAsync();
        var afterDrop = await stays.NextAsync();

        PresentationStates.ClientIds(afterLeave).ShouldBe(["k-1", "k-3"]);
        PresentationStates.ClientIds(afterDrop).ShouldBe(["k-1"]);
    }

    // Only the connection holding an entry removes it. C2 took over k-1, so C1's leave and C1's disconnect remove
    // nothing and send nothing: each later update is one seq after the last. The test waits until the server has
    // finished C1's disconnect (DisconnectWatch) before C2's last update, so the order can't hide a late removal.
    [Test]
    public async Task Disconnect_WhenReplacedConnectionCloses_ShouldKeepTheNewEntry()
    {
        var deck = await OwnDeckAsync();
        var c1 = await PresentationStates.ConnectAsync(Factory, TestUser.Sam);
        var c2 = await ConnectAsync(TestUser.Sam);
        await c1.JoinAsync(deck, "k-1");
        var joinSeq = (await c2.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("seq").GetInt64();
        var c1Id = c1.Connection.ConnectionId!;

        await c1.LeaveAsync(deck);
        var afterLeave = await c2.UpdateAsync(deck, 2, 0);
        await c1.DisposeAsync();
        await _disconnects.DisconnectedAsync(c1Id);
        var afterDrop = await c2.UpdateAsync(deck, 3, 0);

        PresentationStates.ClientIds(afterLeave).ShouldBe(["k-1"]);
        afterLeave.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 1);
        PresentationStates.ClientIds(afterDrop).ShouldBe(["k-1"]);
        afterDrop.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 2);
    }

    [Test]
    public async Task UpdatePresentation_WhenGroupHasThreeMembers_ShouldReachEveryMemberIncludingTheSender()
    {
        var deck = await OwnDeckAsync();
        var members = new[] { await ConnectAsync(TestUser.Sam), await ConnectAsync(TestUser.Sam), await ConnectAsync(TestUser.Sam) };
        await members[0].JoinAsync(deck, "k-1");
        await members[1].JoinAsync(deck, "k-2", "audience");
        await members[2].JoinAsync(deck, "k-3", "phone");
        await DrainAsync(members[0], 3);
        await DrainAsync(members[1], 2);
        await DrainAsync(members[2], 1);

        var sent = await members[2].UpdateAsync(deck, 2, 1);

        foreach (var member in members)
        {
            var received = await member.NextAsync();
            received.GetProperty("seq").GetInt64().ShouldBe(sent.GetProperty("seq").GetInt64());
            (received.GetProperty("slide").GetInt32(), received.GetProperty("step").GetInt32()).ShouldBe((2, 1));
        }
    }

    // A link viewer, a named viewer and an anonymous caller of a deck Sam shares.
    private static IEnumerable<TestCaseData> NonOwners()
    {
        yield return new TestCaseData(TestUser.Alex, "link", Array.Empty<string>()).SetArgDisplayNames("link viewer");
        yield return new TestCaseData(TestUser.Mira, "named", new[] { TestUser.Mira.Email }).SetArgDisplayNames("named viewer");
        yield return new TestCaseData(TestUser.Anonymous, "link", Array.Empty<string>()).SetArgDisplayNames("anonymous");
    }

    private static object Resume(int slide, int step, string timerState, long elapsedMs)
        => new { slide, step, black = false, skipDeepDives = true, timer = new { state = timerState, elapsedMs } };

    private static async Task DrainAsync(PresentationStates connection, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await connection.NextAsync();
        }
    }

    private int MinutesSinceMidnight(string timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(Clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(timeZone));
        return (local.Hour * 60) + local.Minute;
    }

    private Task<string> OwnDeckAsync() => DeckOwnedByAsync(TestUser.Sam);

    private async Task<string> DeckOwnedByAsync(TestUser owner)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, owner);
        return await PresentingSeed.CreateDeckAsync(agent);
    }

    private async Task<PresentationStates> ConnectAsync(TestUser user)
    {
        var connection = await PresentationStates.ConnectAsync(Factory, user);
        _connections.Add(connection);
        return connection;
    }
}
