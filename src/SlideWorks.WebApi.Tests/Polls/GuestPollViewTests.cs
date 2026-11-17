using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// GET /api/join/{code}/poll on the real answers (V5), and the 409's current view: the guest's own answer, the tally as
// PollVisibility lets this guest see it ("hidden means hidden", asserted on the raw bodies), joinedLate, the roster rule and
// CanWatchAsync. An open round's counts come from FakePollTallies, a closed round's from its Final; the activity on screen
// from FakeAudienceActivity. The fakes are shared by the fixture's app, so every test seeds a deck, a code and a round of its
// own. Each test starts a new minute, as the 404s here count as join-code misses for the test server's one address.
[TestFixture]
[CancelAfter(60000)]
public class GuestPollViewTests : PollTestFixture
{
    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(30);

    // Open-round counts no real tally could make from these tests' answers, so seeing them means they came from the tally.
    private static readonly int[] _openCounts = [41, 73, 59];

    private const string ANY_COUNTS = "\"counts\":[";

    private FakeAudienceActivity Activity => Factory.Services.GetRequiredService<FakeAudienceActivity>();

    private FakePollTallies Tallies => Factory.Services.GetRequiredService<FakePollTallies>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceActivity.AddTo(services);
        FakePollTallies.AddTo(services);
    }

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task View_WhenResultsAreAfterCloseAndOpen_ShouldHaveNoCounts()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.AfterClose);
        await using var guest = await JoinAsync(poll);

        var before = await RawAsync(guest.GetPollAsync(poll.Code));
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await RawAsync(guest.GetPollAsync(poll.Code));
        var conflict = await RawAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2], 0)));

        foreach (var raw in new[] { before, after, conflict })
        {
            raw.ShouldNotContain(ANY_COUNTS);
        }
        Tally(before).GetProperty("answered").GetInt32().ShouldBe(7);
        Tally(before).GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        Tally(after).GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
        Parse(after).GetProperty("mine").GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1]);
        Parse(conflict).GetProperty("error").GetString().ShouldBe("stale");
        Parse(conflict).GetProperty("current").GetProperty("tally").GetProperty("answered").GetInt32().ShouldBe(7);
        Parse(conflict).GetProperty("current").GetProperty("tally").GetProperty("counts").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task View_WhenResultsAreAfterAnswer_ShouldHaveCountsOnlyOnceAnswered()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.AfterAnswer);
        await using var guest = await JoinAsync(poll);

        var before = await RawAsync(guest.GetPollAsync(poll.Code));
        var conflictBefore = await RawAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 3)));
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await RawAsync(guest.GetPollAsync(poll.Code));
        var conflictAfter = await RawAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2], 0)));

        before.ShouldNotContain(ANY_COUNTS);
        conflictBefore.ShouldNotContain(ANY_COUNTS);
        Tally(before).GetProperty("answered").GetInt32().ShouldBe(7);
        Counts(Tally(after)).ShouldBe(_openCounts);
        Counts(Parse(conflictAfter).GetProperty("current").GetProperty("tally")).ShouldBe(_openCounts);
    }

    [TestCase(PollResults.Live)]
    [TestCase(PollResults.AfterAnswer)]
    [TestCase(PollResults.AfterClose)]
    public async Task View_WhenRoundCloses_ShouldShowEveryGuestTheFinalCounts(PollResults results)
    {
        var (poll, round) = await OpenRoundAsync(results);
        await using var answered = await JoinAsync(poll);
        await using var silent = await JoinAsync(poll);
        (await BoundedAsync(answered.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Rows.CloseAsync(round.Id, joinedAtClose: 2);
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Closed, results, roomSees: true));

        var answeredView = Parse(await RawAsync(answered.GetPollAsync(poll.Code)));
        var silentView = Parse(await RawAsync(silent.GetPollAsync(poll.Code)));
        var conflict = Parse(await RawAsync(silent.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 0))));

        Counts(answeredView.GetProperty("tally")).ShouldBe([0, 0, 1]);
        Counts(silentView.GetProperty("tally")).ShouldBe([0, 0, 1]);
        Counts(conflict.GetProperty("current").GetProperty("tally")).ShouldBe([0, 0, 1]);
        silentView.GetProperty("tally").GetProperty("answered").GetInt32().ShouldBe(1);
        silentView.GetProperty("joinedLate").GetBoolean().ShouldBeFalse();
    }

    [TestCase(PollResults.Live, true)]
    [TestCase(PollResults.AfterAnswer, false)]
    [TestCase(PollResults.AfterClose, false)]
    public async Task View_WhenOpenAndNotAnswered_ShouldShowCountsOnlyWhenTheRoomSeesThem(PollResults results, bool sees)
    {
        var (poll, round) = await OpenRoundAsync(results);
        await using var guest = await JoinAsync(poll);

        var view = await RawAsync(guest.GetPollAsync(poll.Code));
        var conflict = await RawAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 5)));

        foreach (var raw in new[] { view, conflict })
        {
            raw.Contains(ANY_COUNTS).ShouldBe(sees);
        }
        Tally(view).GetProperty("answered").GetInt32().ShouldBe(7);
        Parse(conflict).GetProperty("current").GetProperty("tally").GetProperty("answered").GetInt32().ShouldBe(7);
    }

    [Test]
    public async Task View_WhenRevealedAndResultsAreAfterClose_ShouldShowCounts()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, Settings(PollResults.AfterClose));
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId, revealed: true);
        Tallies.Set(round.Id.ToString(), new PollCounts(7, _openCounts));
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Open, PollResults.AfterClose, roomSees: true));
        await using var guest = await JoinAsync(poll);

        var view = Parse(await RawAsync(guest.GetPollAsync(poll.Code)));

        Counts(view.GetProperty("tally")).ShouldBe(_openCounts);
    }

    [Test]
    public async Task View_WhenAnotherGuestAnswered_ShouldNotContainTheirPicks()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.Live);
        await using var other = await JoinAsync(poll);
        await using var guest = await JoinAsync(poll);
        (await BoundedAsync(other.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var raw = await RawAsync(guest.GetPollAsync(poll.Code));
        var view = Parse(raw);

        view.GetProperty("mine").ValueKind.ShouldBe(JsonValueKind.Null);
        raw.ShouldNotContain(other.GuestId);
        raw.ShouldNotContain(guest.GuestId);
        view.GetProperty("tally").EnumerateObject().Select(p => p.Name).ShouldBe(["type", "round", "answered", "counts"]);
    }

    [Test]
    public async Task View_WhenGuestJoinedAfterTheRoundClosed_ShouldBeJoinedLate()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.AfterClose);
        await using var early = await JoinAsync(poll);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Closed, PollResults.AfterClose, roomSees: true));
        Clock.Advance(TimeSpan.FromSeconds(5));
        await using var late = await JoinAsync(poll);

        var lateView = Parse(await RawAsync(late.GetPollAsync(poll.Code)));
        var earlyView = Parse(await RawAsync(early.GetPollAsync(poll.Code)));

        lateView.GetProperty("joinedLate").GetBoolean().ShouldBeTrue();
        lateView.GetProperty("mine").ValueKind.ShouldBe(JsonValueKind.Null);
        earlyView.GetProperty("joinedLate").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task View_WhenRoundIsDraft_ShouldHaveNoTally()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Draft, PollResults.Live, roomSees: true));
        await using var guest = await JoinAsync(poll);

        var view = Parse(await RawAsync(guest.GetPollAsync(poll.Code)));

        view.GetProperty("activity").GetProperty("phase").GetString().ShouldBe("draft");
        view.GetProperty("mine").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("tally").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("joinedLate").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task View_WhenGuestIsNotOnTheRoster_ShouldReturn404NoSession()
    {
        var (poll, _) = await OpenRoundAsync(PollResults.Live);
        await using var stranger = await GuestClient.ConnectAsync(Factory);

        var response = await BoundedAsync(stranger.GetPollAsync(poll.Code));
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Parse(raw).GetProperty("error").GetString().ShouldBe("no_session");
    }

    [Test]
    public async Task CanWatch_WhenResultsAreAfterAnswerAndGuestAnswered_ShouldReturnTheRound()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.AfterAnswer);
        await using var guest = await JoinAsync(poll);
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var watch = await CanWatchAsync(poll.Code, guest.GuestId);

        watch.ShouldBe(round.Id.ToString());
    }

    [Test]
    public async Task CanWatch_WhenGuestHasNotAnswered_ShouldReturnNull()
    {
        var (poll, _) = await OpenRoundAsync(PollResults.AfterAnswer);
        await using var guest = await JoinAsync(poll);

        var watch = await CanWatchAsync(poll.Code, guest.GuestId);

        watch.ShouldBeNull();
    }

    [TestCase(PollResults.Live)]
    [TestCase(PollResults.AfterClose)]
    public async Task CanWatch_WhenResultsAreNotAfterAnswer_ShouldReturnNull(PollResults results)
    {
        var (poll, round) = await OpenRoundAsync(results);
        await using var guest = await JoinAsync(poll);
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var watch = await CanWatchAsync(poll.Code, guest.GuestId);

        watch.ShouldBeNull();
    }

    [Test]
    public async Task CanWatch_WhenRoundIsClosed_ShouldReturnNull()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.AfterAnswer);
        await using var guest = await JoinAsync(poll);
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);

        var watch = await CanWatchAsync(poll.Code, guest.GuestId);

        watch.ShouldBeNull();
    }

    [Test]
    public async Task CanWatch_WhenGuestIsNotOnTheRoster_ShouldReturnNull()
    {
        var (poll, round) = await OpenRoundAsync(PollResults.AfterAnswer);
        await Rows.AnswerAsync(round.Id, GuestCookie.NewId(), 0);

        var watch = await CanWatchAsync(poll.Code, GuestCookie.NewId());

        watch.ShouldBeNull();
    }

    private async Task<string?> CanWatchAsync(string code, string guestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPollAnswers>().CanWatchAsync(code, guestId);
    }

    // A live poll of the test's own with these results, an open round 1 whose tally reads 7 answered and _openCounts, and
    // the activity on screen for it.
    private async Task<(SeededPoll Poll, PollRound Round)> OpenRoundAsync(PollResults results)
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, Settings(results));
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        Tallies.Set(round.Id.ToString(), new PollCounts(7, _openCounts));
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Open, results, PollVisibility.RoomSees(results, false, false)));
        return (poll, round);
    }

    // A guest who has joined the live session, and so is on its roster.
    private async Task<GuestClient> JoinAsync(SeededPoll poll)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        return guest;
    }

    private static PollSettings Settings(PollResults results) => PollSettings.Default with { Results = results };

    private static PollActivity ActivityOf(PollRound round, PollPhase phase, PollResults results, bool roomSees) => new(
        phase == PollPhase.Draft ? null : round.Id.ToString(),
        round.Number,
        phase,
        PollSeed.QUESTION,
        PollSeed.OPTIONS,
        PollPick.One,
        null,
        results,
        roomSees,
        null);

    // The raw body of a GET or a PUT, as the phone receives it.
    private static async Task<string> RawAsync(Task<HttpResponseMessage> request)
    {
        var response = await BoundedAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    // The task's result, failing the test when it doesn't come within WAIT rather than hanging it.
    private static async Task<T> BoundedAsync<T>(Task<T> task)
    {
        var first = await Task.WhenAny(task, Task.Delay(WAIT, CancellationToken.None));
        first.ShouldBeSameAs(task, $"no result within {WAIT}");
        return await task;
    }

    private static JsonElement Parse(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static JsonElement Tally(string raw) => Parse(raw).GetProperty("tally");

    private static int[] Counts(JsonElement tally) => [.. tally.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32())];
}
