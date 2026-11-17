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

// PUT /api/join/{code}/poll/answer on the real answers (V5): revisions, the 409 with the guest's current view, one wire test
// per picks rule, the check order (stale before the picks rules), the 30 a minute and the roster. Rounds come from
// PollRowSeed, the activity on screen from FakeAudienceActivity and the tally from FakePollTallies. The fakes are shared by
// the fixture's app, so every test seeds a deck, a code and a round of its own. Each test starts a new minute: the 404s
// here count as join-code misses for the test server's one address, and the answer limit is per minute too.
[TestFixture]
[CancelAfter(60000)]
public class PollAnswerTests : PollTestFixture
{
    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(30);

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
    public async Task Answer_WhenFirst_ShouldReturnRevisionOne()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1]);
        body.GetProperty("revision").GetInt32().ShouldBe(1);
        var applied = Tallies.Applied.Where(a => a.Round.RoundId == round.Id.ToString()).ToList();
        applied.Count.ShouldBe(1);
        applied[0].Round.ShouldBe(new PollRoundRef(poll.DeckId, poll.Slide, poll.SlideId, poll.SessionId.ToString(), round.Id.ToString(), PollSeed.OPTIONS.Length));
        applied[0].OldPicks.ShouldBeEmpty();
        applied[0].NewPicks.ShouldBe([1]);
    }

    [Test]
    public async Task Answer_WhenReplaced_ShouldKeepOneRowAndRaiseRevision()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2], 1)));
        var body = await BodyAsync(response);
        var closed = await Rows.CloseAsync(round.Id, joinedAtClose: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("revision").GetInt32().ShouldBe(2);
        body.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([2]);
        closed.Final!.Answered.ShouldBe(1);
        closed.Final.Counts.ShouldBe([0, 0, 1]);
        var applied = Tallies.Applied.Where(a => a.Round.RoundId == round.Id.ToString()).ToList();
        applied.Count.ShouldBe(2);
        applied[1].OldPicks.ShouldBe([0]);
        applied[1].NewPicks.ShouldBe([2]);
    }

    [Test]
    public async Task Answer_WhenRevisionIsStale_ShouldReturn409AndWriteNothing()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);
        (await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], 0)))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2], 0)));
        var body = await BodyAsync(response);
        var view = await BodyAsync(await guest.GetPollAsync(poll.Code));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body.GetProperty("current"), Example("poll-answer.json").GetProperty("stale").GetProperty("current"), "poll-answer.json stale.current", ScreenShapes.GuestPoll);
        body.GetProperty("error").GetString().ShouldBe("stale");
        body.GetProperty("current").GetProperty("mine").GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([0]);
        body.GetProperty("current").GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(1);
        view.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(1);
        Tallies.Applied.Count(a => a.Round.RoundId == round.Id.ToString()).ShouldBe(1);
    }

    [Test]
    public async Task Answer_WhenRoundIsClosed_ShouldReturn409WithCurrent()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);
        await Rows.AnswerAsync(round.Id, GuestCookie.NewId(), 2);
        await Rows.CloseAsync(round.Id, joinedAtClose: 2);
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Closed, PollSettings.Default, roomSees: true));

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe("stale");
        var current = body.GetProperty("current");
        current.GetProperty("activity").GetProperty("phase").GetString().ShouldBe("closed");
        current.GetProperty("mine").ValueKind.ShouldBe(JsonValueKind.Null);
        current.GetProperty("tally").GetProperty("answered").GetInt32().ShouldBe(1);
        current.GetProperty("tally").GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([0, 0, 1]);
        current.GetProperty("joinedLate").GetBoolean().ShouldBeFalse();
        Tallies.Applied.ShouldNotContain(a => a.Round.RoundId == round.Id.ToString());
    }

    [Test]
    public async Task Answer_WhenRoundIsDraft_ShouldReturn409()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Draft, PollSettings.Default, roomSees: true));
        await using var guest = await JoinAsync(poll);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("current").GetProperty("activity").GetProperty("phase").GetString().ShouldBe("draft");
        body.GetProperty("current").GetProperty("tally").ValueKind.ShouldBe(JsonValueKind.Null);
        Tallies.Applied.ShouldNotContain(a => a.Round.RoundId == round.Id.ToString());
    }

    [Test]
    public async Task Answer_WhenRoundIsNotTheOpenOne_ShouldReturn409()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(Guid.NewGuid().ToString(), [1], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("current").GetProperty("activity").GetProperty("round").GetString().ShouldBe(round.Id.ToString());
        body.GetProperty("current").GetProperty("mine").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Answer_WhenStaleAndPicksBreakARule_ShouldReturn409NotA400()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [], 0)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Answer_WhenPicksAreEmpty_ShouldReturn400Required()
    {
        var error = await RefusalAsync(PollSettings.Default, []);

        error.ShouldBe("picks: required");
    }

    [Test]
    public async Task Answer_WhenAPickIsNoOption_ShouldReturn400UnknownOption()
    {
        var error = await RefusalAsync(PollSettings.Default, [7]);

        error.ShouldBe("picks: unknown option 7");
    }

    [Test]
    public async Task Answer_WhenAPickRepeats_ShouldReturn400DuplicateBeforePickOne()
    {
        var error = await RefusalAsync(PollSettings.Default, [1, 1]);

        error.ShouldBe("picks: duplicate");
    }

    [Test]
    public async Task Answer_WhenPickOneGetsTwo_ShouldReturn400PickOne()
    {
        var error = await RefusalAsync(PollSettings.Default, [0, 1]);

        error.ShouldBe("picks: pick one");
    }

    [Test]
    public async Task Answer_WhenUpToTwoGetsThree_ShouldReturn400AtMostTwo()
    {
        var error = await RefusalAsync(PollSettings.Default with { Pick = PollPick.UpTo, Max = 2 }, [0, 1, 2]);

        error.ShouldBe("picks: at most 2");
    }

    [Test]
    public async Task Answer_WhenUpToTwoGetsTwo_ShouldStoreThemAscending()
    {
        var (poll, round) = await OpenRoundAsync(PollSettings.Default with { Pick = PollPick.UpTo, Max = 2 });
        await using var guest = await JoinAsync(poll);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [2, 0], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([0, 2]);
    }

    [Test]
    public async Task Answer_WhenPastThirtyAMinute_ShouldReturn429()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);
        for (var revision = 0; revision < AnswerLimiter.PER_MINUTE; revision++)
        {
            var sent = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [revision % 3], revision)));
            sent.StatusCode.ShouldBe(HttpStatusCode.OK, $"answer {revision + 1}");
        }

        var limited = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], AnswerLimiter.PER_MINUTE)));
        var body = await BodyAsync(limited);
        Clock.Advance(TimeSpan.FromMinutes(1));
        var later = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [0], AnswerLimiter.PER_MINUTE)));

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        body.GetProperty("error").GetString().ShouldBe("too_many_answers");
        later.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Answer_WhenGuestIsNotOnTheRoster_ShouldReturn404NoSession()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var stranger = await GuestClient.ConnectAsync(Factory);

        var response = await BoundedAsync(stranger.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), [1], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("error").GetString().ShouldBe("no_session");
        Tallies.Applied.ShouldNotContain(a => a.Round.RoundId == round.Id.ToString());
    }

    [Test]
    public async Task Answer_WhenCodeIsUnknown_ShouldReturn404NoSession()
    {
        var (poll, round) = await OpenRoundAsync();
        await using var guest = await JoinAsync(poll);

        var response = await BoundedAsync(guest.AnswerAsync("000000", new AnswerRequest(round.Id.ToString(), [1], 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("error").GetString().ShouldBe("no_session");
    }

    // The 400 a guest on the roster gets for these picks on an open round with these settings.
    private async Task<string> RefusalAsync(PollSettings settings, int[] picks)
    {
        var (poll, round) = await OpenRoundAsync(settings);
        await using var guest = await JoinAsync(poll);

        var response = await BoundedAsync(guest.AnswerAsync(poll.Code, new AnswerRequest(round.Id.ToString(), picks, 0)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Tallies.Applied.ShouldNotContain(a => a.Round.RoundId == round.Id.ToString());
        return body.GetProperty("error").GetString()!;
    }

    // A live poll of the test's own, with these settings, an open round 1 and the activity on screen for it.
    private async Task<(SeededPoll Poll, PollRound Round)> OpenRoundAsync(PollSettings? settings = null)
    {
        var poll = await SeedPollAsync();
        var used = settings ?? PollSettings.Default;
        await Rows.SettingsAsync(poll.SlideId, used);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        Activity.Set(poll.Code, poll.DeckId, ActivityOf(round, PollPhase.Open, used, PollVisibility.RoomSees(used.Results, false, false)));
        return (poll, round);
    }

    // A guest who has joined the live session, and so is on its roster.
    private async Task<GuestClient> JoinAsync(SeededPoll poll)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        return guest;
    }

    private static PollActivity ActivityOf(PollRound round, PollPhase phase, PollSettings settings, bool roomSees) => new(
        phase == PollPhase.Draft ? null : round.Id.ToString(),
        round.Number,
        phase,
        PollSeed.QUESTION,
        PollSeed.OPTIONS,
        settings.Pick,
        settings.Max,
        settings.Results,
        roomSees,
        null);

    // The task's result, failing the test when it doesn't come within WAIT rather than hanging it.
    private static async Task<T> BoundedAsync<T>(Task<T> task)
    {
        var first = await Task.WhenAny(task, Task.Delay(WAIT, CancellationToken.None));
        first.ShouldBeSameAs(task, $"no result within {WAIT}");
        return await task;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static JsonElement Example(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));
        return document.RootElement.Clone();
    }
}
