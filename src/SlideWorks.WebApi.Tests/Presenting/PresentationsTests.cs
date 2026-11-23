using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// The registry's rules beyond one hub call each (P2, P4, P7, P10): concurrency, the 2-hour keep, the live list, the
// timer across a resume, clamping against a deck that changed, and SW-5's seam (GetAsync, StartAsync, observers). Two
// observers are registered: one that always throws, ahead of a recording one, so every test here also proves a failing
// observer is skipped. "Nothing was told" is proved with a later change of known seq, never by waiting.
[TestFixture]
[CancelAfter(30000)]
public class PresentationsTests : ApiTestFixture
{
    private readonly List<PresentationStates> _connections = [];
    private readonly RecordingObserver _observer = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services
        .AddSingleton<IPresentationObserver>(new ThrowingObserver())
        .AddSingleton<IPresentationObserver>(_observer);

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

    [Test]
    public async Task GetAsync_WhenBeforeAndAfterAJoin_ShouldBeNullThenTheLiveState()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var before = await Registry.GetAsync(deck);
        var joined = (await owner.JoinAsync(deck, "k-1")).GetProperty("state");
        var after = await Registry.GetAsync(deck);

        before.ShouldBeNull();
        after.ShouldNotBeNull();
        after.Epoch.ShouldBe(joined.GetProperty("epoch").GetString());
        after.Seq.ShouldBe(joined.GetProperty("seq").GetInt64());
        after.Connected.Select(c => c.ClientId).ShouldBe(["k-1"]);
    }

    [Test]
    public async Task StartAsync_WhenCalledTwiceThenJoined_ShouldCreateOneEpochThatTheJoinUses()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var started = await Registry.StartAsync(deck);
        var again = await Registry.StartAsync(deck);
        var joined = (await owner.JoinAsync(deck, "k-1", resume: Resume(3, 0, "running", 5000))).GetProperty("state");

        (started.Slide, started.Step, started.Timer.State, started.Connected.Count).ShouldBe((1, 0, TimerState.NotStarted, 0));
        (again.Epoch, again.Seq).ShouldBe((started.Epoch, started.Seq));
        joined.GetProperty("epoch").GetString().ShouldBe(started.Epoch);
        joined.GetProperty("seq").GetInt64().ShouldBe(started.Seq + 1);
        joined.GetProperty("slide").GetInt32().ShouldBe(1);
        joined.GetProperty("timer").GetProperty("state").GetString().ShouldBe("notStarted");
    }

    // D9: a started state with no connections is kept for 2 hours too, and GetAsync never revives one dropped after them.
    [Test]
    public async Task GetAsync_WhenAStartedStateSatTwoHoursUnjoined_ShouldBeNull()
    {
        var deck = await OwnDeckAsync();

        await Registry.StartAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(119));
        var kept = await Registry.GetAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(2));
        var dropped = await Registry.GetAsync(deck);

        kept.ShouldNotBeNull();
        dropped.ShouldBeNull();
    }

    // Each accepted change reaches the observers once, in seq order, after the throwing observer ahead of it: start,
    // join, update, timer, talk length and leave.
    [Test]
    public async Task Observers_WhenChangesAreAccepted_ShouldBeToldEachOneInOrder()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        var phone = await ConnectAsync(TestUser.Sam);

        var started = await Registry.StartAsync(deck);
        await owner.JoinAsync(deck, "k-1");
        await phone.JoinAsync(deck, "k-2", "phone");
        await owner.UpdateAsync(deck, 2, 0);
        await owner.SetTimerAsync(deck, "pause");
        await Registry.TalkLengthChangedAsync(deck, 25);
        await phone.LeaveAsync(deck);

        var told = new List<PresentationState>();
        for (var i = 0; i < 7; i++)
        {
            told.Add(await _observer.NextAsync(deck));
        }
        told.Select(s => s.Seq).ShouldBe(Enumerable.Range(0, 7).Select(i => started.Seq + i));
        told.Select(s => s.Epoch).ShouldAllBe(e => e == started.Epoch);
        told[3].Slide.ShouldBe(2);
        told[4].Timer.State.ShouldBe(TimerState.Paused);
        told[5].TalkMinutes.ShouldBe(25);
        told[6].Connected.Select(c => c.ClientId).ShouldBe(["k-1"]);
        _observer.Received(deck).ShouldBeEmpty();
    }

    // A refused join and a refused update tell nobody: the next state the observers get is the owner's own update's,
    // one seq after the join.
    [Test]
    public async Task Observers_WhenAJoinOrAnUpdateIsRefused_ShouldBeToldNothing()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        var alex = await ConnectAsync(TestUser.Alex);
        var joinSeq = (await owner.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("seq").GetInt64();
        (await _observer.NextAsync(deck)).Seq.ShouldBe(joinSeq);

        var join = await Should.ThrowAsync<HubException>(() => alex.JoinAsync(deck, "k-2"));
        var update = await Should.ThrowAsync<HubException>(() => alex.UpdateAsync(deck, 2, 0));
        await owner.UpdateAsync(deck, 3, 0);

        join.Message.ShouldContain(DeckHub.DECK_NOT_FOUND);
        update.Message.ShouldContain(DeckHub.NOT_PRESENTING);
        var next = await _observer.NextAsync(deck);
        (next.Seq, next.Slide).ShouldBe((joinSeq + 1, 3));
    }

    // The throwing observer runs first on every change; the caller still gets its state, the group still gets the
    // broadcast, and the recording observer after it is still told.
    [Test]
    public async Task Observers_WhenOneThrows_ShouldNotStopTheUpdateOrTheBroadcast()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        var audience = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");
        await audience.JoinAsync(deck, "k-2", "audience");
        await audience.NextAsync();

        var sent = await owner.UpdateAsync(deck, 2, 1);

        var broadcast = await audience.NextAsync();
        broadcast.GetProperty("seq").GetInt64().ShouldBe(sent.GetProperty("seq").GetInt64());
        (broadcast.GetProperty("slide").GetInt32(), broadcast.GetProperty("step").GetInt32()).ShouldBe((2, 1));
        var told = _observer.Received(deck);
        told[^1].Seq.ShouldBe(sent.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task UpdatePresentation_WhenTwoConnectionsUpdateConcurrently_ShouldEndWithOneSeqPerChange()
    {
        var deck = await OwnDeckAsync();
        var first = await ConnectAsync(TestUser.Sam);
        var second = await ConnectAsync(TestUser.Sam);
        await first.JoinAsync(deck, "k-1");
        var joinSeq = (await second.JoinAsync(deck, "k-2", "phone")).GetProperty("state").GetProperty("seq").GetInt64();

        var updates = Enumerable.Range(0, 10)
            .SelectMany(i => new[] { first.UpdateAsync(deck, 1 + (i % 3), 0), second.UpdateAsync(deck, 1 + ((i + 1) % 3), 0) })
            .ToList();
        var results = await Task.WhenAll(updates);
        var final = await Registry.GetAsync(deck);

        results.Select(r => r.GetProperty("seq").GetInt64()).Order().ShouldBe(Enumerable.Range(1, 20).Select(i => joinSeq + i));
        final.ShouldNotBeNull();
        final.Seq.ShouldBe(joinSeq + 20);
        var last = results.Single(r => r.GetProperty("seq").GetInt64() == final.Seq);
        final.Slide.ShouldBe(last.GetProperty("slide").GetInt32());
    }

    // D9: after the 2-hour drop, the next join starts a new epoch seeded from its resume.
    [Test]
    public async Task JoinPresentation_WhenRejoinedAfterTheDropWithResume_ShouldSeedTheNewEpochFromIt()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        var first = (await owner.JoinAsync(deck, "k-1")).GetProperty("state").GetProperty("epoch").GetString();
        await owner.LeaveAsync(deck);

        Clock.Advance(TimeSpan.FromMinutes(121));
        var fresh = (await owner.JoinAsync(deck, "k-1", resume: Resume(3, 0, "paused", 42000))).GetProperty("state");

        fresh.GetProperty("epoch").GetString().ShouldNotBe(first);
        fresh.GetProperty("slide").GetInt32().ShouldBe(3);
        fresh.GetProperty("timer").GetProperty("state").GetString().ShouldBe("paused");
        fresh.GetProperty("timer").GetProperty("elapsedMs").GetInt64().ShouldBe(42000);
    }

    // P2: calling JoinPresentation again on the same connection refreshes clockMinutes and keeps one entry.
    [Test]
    public async Task JoinPresentation_WhenCalledAgainOnTheSameConnection_ShouldRefreshClockMinutes()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1", timeZone: "UTC");

        Clock.Advance(TimeSpan.FromMinutes(90));
        var again = await owner.JoinAsync(deck, "k-1", timeZone: "UTC");

        again.GetProperty("clockMinutes").GetInt32().ShouldBe(MinutesSinceMidnightUtc());
        PresentationStates.ClientIds(again.GetProperty("state")).ShouldBe(["k-1"]);
    }

    // P2: a resumed running timer keeps running from its elapsedMs, on the server's clock.
    [Test]
    public async Task JoinPresentation_WhenResumeTimerIsRunning_ShouldKeepItRunning()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1", resume: Resume(2, 0, "running", 60000));

        Clock.Advance(TimeSpan.FromSeconds(30));
        var paused = (await owner.SetTimerAsync(deck, "pause")).GetProperty("timer");

        (paused.GetProperty("state").GetString(), paused.GetProperty("elapsedMs").GetInt64()).ShouldBe(("paused", 90000L));
    }

    // P2: the clamp uses the deck's current slides, so a slide deleted while presenting moves the limit.
    [Test]
    public async Task UpdatePresentation_WhenTheDeckLostASlide_ShouldClampToTheSlidesLeft()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await PresentingSeed.CreateDeckAsync(agent);
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");

        var (isError, json) = await agent.CallToolAsync("delete_slide", new { deck, slide = 1 });
        var past = await owner.UpdateAsync(deck, PresentingSeed.SLIDES, 0);
        var step = await owner.UpdateAsync(deck, 1, 99);

        isError.ShouldBeFalse(json.ToString());
        past.GetProperty("slide").GetInt32().ShouldBe(PresentingSeed.SLIDES - 1);
        (step.GetProperty("slide").GetInt32(), step.GetProperty("step").GetInt32()).ShouldBe((1, PresentingSeed.BULLETS_STEPS));
    }

    // The per-deck rule holds for one owner's two decks: joining deck A doesn't let a connection drive deck B.
    [Test]
    public async Task UpdatePresentation_WhenJoinedToTheOwnersOtherDeck_ShouldBeRefusedWithNotPresenting()
    {
        var (deckA, deckB) = (await OwnDeckAsync(), await OwnDeckAsync());
        var onA = await ConnectAsync(TestUser.Sam);
        var onB = await ConnectAsync(TestUser.Sam);
        await onA.JoinAsync(deckA, "k-a");
        var joinSeq = (await onB.JoinAsync(deckB, "k-b")).GetProperty("state").GetProperty("seq").GetInt64();

        var update = await Should.ThrowAsync<HubException>(() => onA.UpdateAsync(deckB, 2, 0));
        var timer = await Should.ThrowAsync<HubException>(() => onA.SetTimerAsync(deckB, "pause"));
        var next = await onB.UpdateAsync(deckB, 3, 0);

        update.Message.ShouldContain(DeckHub.NOT_PRESENTING);
        timer.Message.ShouldContain(DeckHub.NOT_PRESENTING);
        next.GetProperty("seq").GetInt64().ShouldBe(joinSeq + 1);
    }

    // P4: only the caller's decks with a presenter or audience window, newest first, with the server's started label.
    // Another test's deck can still be listed while its teardown disconnect is in flight, so order is checked on this
    // test's decks only.
    [Test]
    public async Task ListLive_WhenSeveralDecksAreLive_ShouldListTheCallersWindowedDecksNewestFirst()
    {
        var (older, newer, phoneOnly) = (await OwnDeckAsync(), await OwnDeckAsync(), await OwnDeckAsync());
        var alexsDeck = await DeckOwnedByAsync(TestUser.Alex);
        var sam = await ConnectAsync(TestUser.Sam);
        var alex = await ConnectAsync(TestUser.Alex);
        await sam.JoinAsync(older, "k-older");
        await sam.UpdateAsync(older, 2, 0);
        Clock.Advance(TimeSpan.FromMinutes(12));
        await sam.JoinAsync(newer, "k-newer", "audience", label: "Firefox on Linux");
        await sam.JoinAsync(phoneOnly, "k-phone", "phone");
        await alex.JoinAsync(alexsDeck, "k-alex");

        var response = await Client.GetAsync("/api/presentations");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var all = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presentations").EnumerateArray().ToList();
        all.Select(p => p.GetProperty("deckId").GetString()).ShouldNotContain(alexsDeck);
        all.Select(p => p.GetProperty("deckId").GetString()).ShouldNotContain(phoneOnly);
        var live = all.Where(p => p.GetProperty("deckId").GetString() == newer || p.GetProperty("deckId").GetString() == older).ToList();
        live.Select(p => p.GetProperty("deckId").GetString()).ShouldBe([newer, older]);
        live.Select(p => p.GetProperty("startedLabel").GetString()).ShouldBe(["started just now", "started 12 min ago"]);
        (live[1].GetProperty("title").GetString(), live[1].GetProperty("slide").GetInt32(), live[1].GetProperty("total").GetInt32())
            .ShouldBe(("Ship smaller", 2, PresentingSeed.SLIDES));
        var window = live[0].GetProperty("windows").EnumerateArray().Single();
        (window.GetProperty("role").GetString(), window.GetProperty("label").GetString()).ShouldBe(("audience", "Firefox on Linux"));
    }

    // C5, D7: a person with write presents a deck they do not own, so it is in their live list; a reader of the same deck
    // does not see it, and neither does the owner's list lose it.
    [Test]
    public async Task ListLive_WhenAWriterPresentsASharedDeck_ShouldListItForTheWriterAndNotForAReader()
    {
        var deck = await OwnDeckAsync();
        var shared = await SharingApi.PutAsync(Client, deck, new { level = "named", people = new[] { new { email = TestUser.Alex.Email, access = "write" }, new { email = TestUser.Mira.Email, access = "read" } } });
        shared.StatusCode.ShouldBe(HttpStatusCode.OK, await shared.Content.ReadAsStringAsync());
        var alexWindow = await ConnectAsync(TestUser.Alex);
        await alexWindow.JoinAsync(deck, "k-writer");
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        var writerList = await DeckIdsLiveAsync(alex);
        var readerList = await DeckIdsLiveAsync(mira);
        var ownerList = await DeckIdsLiveAsync(Client);

        writerList.ShouldContain(deck);
        readerList.ShouldNotContain(deck);
        ownerList.ShouldContain(deck);
    }

    private static async Task<List<string?>> DeckIdsLiveAsync(HttpClient person)
    {
        var view = await person.GetFromJsonAsync<JsonElement>("/api/presentations");
        return [.. view.GetProperty("presentations").EnumerateArray().Select(p => p.GetProperty("deckId").GetString())];
    }

    private static object Resume(int slide, int step, string timerState, long elapsedMs)
        => new { slide, step, black = false, skipDeepDives = false, timer = new { state = timerState, elapsedMs } };

    private int MinutesSinceMidnightUtc()
    {
        var now = Clock.GetUtcNow();
        return (now.Hour * 60) + now.Minute;
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
