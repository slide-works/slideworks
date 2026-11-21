using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The poll lifecycle (V4) through the presenter's routes and IPolls: each action's phase rule and refusals, that a refused
// action changes nothing, the close's Final from the answer rows, new rounds and drafts, ending the live session, and the
// order of what a phase change calls. Rounds and answers come from PollRowSeed; the tally, the publisher and the audience
// relay are the settable fakes, wrapped so the order of the relay's refresh and the publisher's call can be read. The app,
// and so the fakes, is shared by the fixture: each test makes its own deck.
[TestFixture]
public class PollLifecycleTests : PollTestFixture
{
    private const string SECOND_QUESTION = "Which part should we cut?";

    private static readonly string[] _secondOptions = ["The intro", "The demo", "The recap"];

    // How long two racing requests may take before they are cancelled and the test fails rather than hangs.
    private static readonly TimeSpan _raceBound = TimeSpan.FromSeconds(30);

    // Each refused action: the action, the state it is tried in, and the code it gives.
    private static readonly object[] _refusals =
    [
        new object[] { "open", "notLive", PollRefusals.NOT_LIVE },
        new object[] { "open", "closed", PollRefusals.WRONG_PHASE },
        new object[] { "open", "anotherOpen", PollRefusals.ANOTHER_POLL_OPEN },
        new object[] { "close", "draft", PollRefusals.WRONG_PHASE },
        new object[] { "reveal", "closed", PollRefusals.WRONG_PHASE },
        new object[] { "reveal", "open", PollRefusals.ALREADY_VISIBLE },
        new object[] { "answer", "open", PollRefusals.WRONG_PHASE },
        new object[] { "answer", "closed", PollRefusals.NO_CORRECT },
        new object[] { "round", "open", PollRefusals.WRONG_PHASE },
        new object[] { "round", "anotherOpenTargetClosed", PollRefusals.ANOTHER_POLL_OPEN },
    ];

    private FakePollTallies Tallies => Factory.Services.GetRequiredService<FakePollTallies>();

    private FakePollPublisher Publisher => Factory.Services.GetRequiredService<FakePollPublisher>();

    private FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    private CallOrder Order => Factory.Services.GetRequiredService<CallOrder>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakePollTallies.AddTo(services);
        FakePollPublisher.AddTo(services);
        FakeAudienceRelay.AddTo(services);
        services.AddSingleton<CallOrder>();
        services.AddSingleton<IAudienceRelay>(provider => new OrderedRelay(
            provider.GetRequiredService<FakeAudienceRelay>(), provider.GetRequiredService<CallOrder>(), provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IPollPublisher>(provider => new OrderedPublisher(provider.GetRequiredService<FakePollPublisher>(), provider.GetRequiredService<CallOrder>()));
    }

    [Test]
    public async Task Open_WhenDeckIsNotLive_ShouldReturn409NotLiveAndWriteNothing()
    {
        var seen = await RefuseByRouteAsync("open", "notLive");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.NOT_LIVE);
        seen.After.ShouldBe(seen.Before);
        seen.After.Views[0].ShouldContain("\"phase\":\"draft\"");
    }

    [Test]
    public async Task Open_WhenAnotherSlidesRoundIsOpen_ShouldReturn409()
    {
        var seen = await RefuseByRouteAsync("open", "anotherOpen");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.ANOTHER_POLL_OPEN);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task Open_WhenClosed_ShouldReturn409WrongPhase()
    {
        var seen = await RefuseByRouteAsync("open", "closed");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.WRONG_PHASE);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task Close_WhenNotOpen_ShouldReturn409WrongPhase()
    {
        var seen = await RefuseByRouteAsync("close", "draft");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.WRONG_PHASE);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task Reveal_WhenClosed_ShouldReturn409WrongPhase()
    {
        var seen = await RefuseByRouteAsync("reveal", "closed");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.WRONG_PHASE);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task Reveal_WhenResultsAreLive_ShouldReturn409AlreadyVisible()
    {
        var seen = await RefuseByRouteAsync("reveal", "open");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.ALREADY_VISIBLE);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task ShowAnswer_WhenOpen_ShouldReturn409WrongPhase()
    {
        var seen = await RefuseByRouteAsync("answer", "open");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.WRONG_PHASE);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task ShowAnswer_WhenPollHasNoCorrect_ShouldReturn409NoCorrect()
    {
        var seen = await RefuseByRouteAsync("answer", "closed");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.NO_CORRECT);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task NewRound_WhenOpen_ShouldReturn409WrongPhase()
    {
        var seen = await RefuseByRouteAsync("round", "open");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.WRONG_PHASE);
        seen.After.ShouldBe(seen.Before);
    }

    [Test]
    public async Task NewRound_WhenAnotherSlidesRoundIsOpen_ShouldReturn409AnotherPollOpen()
    {
        var seen = await RefuseByRouteAsync("round", "anotherOpenTargetClosed");

        seen.Status.ShouldBe(HttpStatusCode.Conflict);
        seen.Error.ShouldBe(PollRefusals.ANOTHER_POLL_OPEN);
        seen.After.ShouldBe(seen.Before);
    }

    [TestCaseSource(nameof(_refusals))]
    public async Task Service_WhenActionIsRefused_ShouldThrowTheCodeAndChangeNothing(string action, string state, string code)
    {
        var (poll, target) = await ArrangeAsync(state);
        var before = await SnapshotAsync(poll, target);

        var refused = await Should.ThrowAsync<ConflictException>(() => ActByServiceAsync(action, poll.DeckId, target));

        refused.Message.ShouldBe(code);
        (await SnapshotAsync(poll, target)).ShouldBe(before);
    }

    [TestCase("open")]
    [TestCase("close")]
    [TestCase("reveal")]
    [TestCase("answer")]
    [TestCase("round")]
    public async Task Action_WhenSlideIsNotAPoll_ShouldReturn404AndChangeNothing(string action)
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var before = await SnapshotAsync(poll, poll.Slide);

        var response = await Client.PostAsync(ActionPath(action, poll.DeckId, poll.OtherSlide), null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await SnapshotAsync(poll, poll.Slide)).ShouldBe(before);
    }

    [Test]
    public async Task Open_WhenDraft_ShouldOpenRoundOne()
    {
        var poll = await SeedPollAsync();

        var view = await ActByRouteAsync("open", poll.DeckId, poll.Slide);

        view.GetProperty("phase").GetString().ShouldBe("open");
        view.GetProperty("number").GetInt32().ShouldBe(1);
        view.GetProperty("round").GetString().ShouldNotBeNullOrEmpty();
        view.GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
        view.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ShouldBe(PollSeed.OPTIONS);
        view.GetProperty("rounds").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Control_WhenRoundIsOpen_ShouldReadAnsweredAndCountsFromTheTally()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        Tallies.Set(round.Id.ToString(), new PollCounts(5, [3, 1, 2]));

        var view = await ControlAsync(poll.DeckId, poll.Slide);

        view.GetProperty("round").GetString().ShouldBe(round.Id.ToString());
        view.GetProperty("answered").GetInt32().ShouldBe(5);
        view.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([3, 1, 2]);
        view.GetProperty("roomSeesCounts").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Close_WhenOpen_ShouldWriteFinalFromTheAnswerRowsAndTheJoinedCount()
    {
        var poll = await SeedPollAsync();
        Relay.Set(new AudienceState(poll.DeckId, 1, AudiencePhase.Live, null, 0, null, null));
        await using var first = await GuestClient.ConnectAsync(Factory);
        await using var second = await GuestClient.ConnectAsync(Factory);
        await first.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        await second.Connection.InvokeAsync<JsonElement>("JoinAudience", poll.Code);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, first.GuestId, 0);
        await Rows.AnswerAsync(round.Id, second.GuestId, 2);
        await Rows.AnswerAsync(round.Id, "a guest who left before the close", 2);
        // The tally says something else: a closed round's numbers are counted from the rows, never read from the tally.
        Tallies.Set(round.Id.ToString(), new PollCounts(9, [9, 9, 9]));

        var view = await ActByRouteAsync("close", poll.DeckId, poll.Slide);
        var stored = await StoredRoundAsync(round.Id);

        view.GetProperty("phase").GetString().ShouldBe("closed");
        view.GetProperty("round").GetString().ShouldBe(round.Id.ToString());
        view.GetProperty("answered").GetInt32().ShouldBe(3);
        view.GetProperty("joined").GetInt32().ShouldBe(2);
        view.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([1, 0, 2]);
        view.GetProperty("roomSeesCounts").GetBoolean().ShouldBeTrue();
        view.GetProperty("rounds").EnumerateArray().Select(Summary).ShouldBe(["1: 3 of 2"]);
        stored.ClosedAt.ShouldBe(Clock.GetUtcNow());
        stored.JoinedAtClose.ShouldBe(2);
        stored.Final.ShouldNotBeNull();
        stored.Final.Options.ShouldBe(PollSeed.OPTIONS);
        stored.Final.Counts.ShouldBe([1, 0, 2]);
        stored.Final.Answered.ShouldBe(3);
    }

    [Test]
    public async Task Reveal_WhenResultsShowAfterClose_ShouldLetTheRoomSeeTheCounts()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Results = PollResults.AfterClose });
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);

        var view = await ActByRouteAsync("reveal", poll.DeckId, poll.Slide);

        view.GetProperty("phase").GetString().ShouldBe("open");
        view.GetProperty("revealed").GetBoolean().ShouldBeTrue();
        view.GetProperty("roomSeesCounts").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task ShowAnswer_WhenClosedWithCorrect_ShouldShowIt()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Correct = 2 });
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 4);

        var view = await ActByRouteAsync("answer", poll.DeckId, poll.Slide);

        view.GetProperty("phase").GetString().ShouldBe("closed");
        view.GetProperty("answerShown").GetBoolean().ShouldBeTrue();
        view.GetProperty("joined").GetInt32().ShouldBe(4);
        (await StoredRoundAsync(round.Id)).AnswerShownAt.ShouldBe(Clock.GetUtcNow());
    }

    [Test]
    public async Task ShowAnswer_WhenAnswerIsAlreadyShown_ShouldSucceedAndChangeNothing()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Correct = 1 });
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var shownAt = (await Rows.CloseAsync(round.Id, joinedAtClose: 0, answerShown: true)).AnswerShownAt;
        Clock.Advance(TimeSpan.FromMinutes(1));
        var before = await SnapshotAsync(poll, poll.Slide);

        var view = await ActByRouteAsync("answer", poll.DeckId, poll.Slide);

        view.GetProperty("answerShown").GetBoolean().ShouldBeTrue();
        (await StoredRoundAsync(round.Id)).AnswerShownAt.ShouldBe(shownAt);
        (await SnapshotAsync(poll, poll.Slide)).ShouldBe(before);
    }

    [Test]
    public async Task NewRound_WhenClosed_ShouldOpenRoundTwoAndKeepRoundOne()
    {
        var poll = await SeedPollAsync();
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(first.Id, "guest one", 1);
        await Rows.CloseAsync(first.Id, joinedAtClose: 3);

        var view = await ActByRouteAsync("round", poll.DeckId, poll.Slide);
        var kept = await StoredRoundAsync(first.Id);

        view.GetProperty("phase").GetString().ShouldBe("open");
        view.GetProperty("number").GetInt32().ShouldBe(2);
        view.GetProperty("round").GetString().ShouldNotBe(first.Id.ToString());
        view.GetProperty("answered").GetInt32().ShouldBe(0);
        view.GetProperty("rounds").EnumerateArray().Select(Summary).ShouldBe(["1: 1 of 3"]);
        kept.Final.ShouldNotBeNull();
        kept.Final.Counts.ShouldBe([0, 1, 0]);
    }

    [Test]
    public async Task NewRound_WhenPollIsManual_ShouldStayDraft()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);

        var view = await ActByRouteAsync("round", poll.DeckId, poll.Slide);

        view.GetProperty("phase").GetString().ShouldBe("draft");
        view.GetProperty("number").GetInt32().ShouldBe(2);
        view.GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("rounds").EnumerateArray().Select(Summary).ShouldBe(["1: 0 of 0"]);
    }

    [Test]
    public async Task Open_WhenRoundIsADraft_ShouldOpenThatRound()
    {
        var poll = await SeedPollAsync();
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);
        var draft = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft, number: 2);

        var view = await ActByRouteAsync("open", poll.DeckId, poll.Slide);

        view.GetProperty("phase").GetString().ShouldBe("open");
        view.GetProperty("number").GetInt32().ShouldBe(2);
        view.GetProperty("round").GetString().ShouldBe(draft.Id.ToString());
        (await StoredRoundAsync(draft.Id)).OpenedAt.ShouldBe(Clock.GetUtcNow());
    }

    [Test]
    public async Task Open_WhenAnotherSlidesRoundIsADraft_ShouldDiscardItAndUseUpItsNumber()
    {
        var poll = await SeedPollAsync();
        var (second, _) = await AddSecondPollAsync(poll);
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);
        var draft = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft, number: 2);

        var opened = await ActByRouteAsync("open", poll.DeckId, second);
        var left = await ControlAsync(poll.DeckId, poll.Slide);
        var discarded = await StoredRoundAsync(draft.Id);
        await ActByRouteAsync("close", poll.DeckId, second);
        var next = await ActByRouteAsync("round", poll.DeckId, poll.Slide);

        opened.GetProperty("phase").GetString().ShouldBe("open");
        left.GetProperty("phase").GetString().ShouldBe("closed");
        left.GetProperty("number").GetInt32().ShouldBe(1);
        discarded.ClosedAt.ShouldNotBeNull();
        discarded.OpenedAt.ShouldBeNull();
        discarded.Final.ShouldBeNull();
        discarded.JoinedAtClose.ShouldBeNull();
        next.GetProperty("number").GetInt32().ShouldBe(3);
        next.GetProperty("rounds").EnumerateArray().Select(Summary).ShouldBe(["1: 0 of 0"]);
    }

    [Test]
    public async Task NewRound_WhenAnotherSlidesRoundIsADraft_ShouldDiscardItFirst()
    {
        var poll = await SeedPollAsync();
        var (second, secondId) = await AddSecondPollAsync(poll);
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);
        var other = await Rows.RoundAsync(secondId, poll.SessionId);
        await Rows.CloseAsync(other.Id, joinedAtClose: 0);
        var draft = await Rows.RoundAsync(secondId, poll.SessionId, PollPhase.Draft, number: 2);

        var view = await ActByRouteAsync("round", poll.DeckId, poll.Slide);
        var left = await ControlAsync(poll.DeckId, second);

        view.GetProperty("phase").GetString().ShouldBe("open");
        view.GetProperty("number").GetInt32().ShouldBe(2);
        left.GetProperty("phase").GetString().ShouldBe("closed");
        (await StoredRoundAsync(draft.Id)).Final.ShouldBeNull();
    }

    [TestCase(false, PollRefusals.WRONG_PHASE)]
    [TestCase(true, PollRefusals.ANOTHER_POLL_OPEN)]
    public async Task Open_WhenTwoArriveAtOnce_ShouldOpenOneRoundAndRefuseTheOther(bool otherSlide, string code)
    {
        var poll = await SeedPollAsync();
        var target = otherSlide ? (await AddSecondPollAsync(poll)).Slide : poll.Slide;
        var entries = await ActivityCountAsync(poll.DeckId);

        using var bound = new CancellationTokenSource(_raceBound);
        var responses = await Task.WhenAll(
            Client.PostAsync(ActionPath("open", poll.DeckId, poll.Slide), null, bound.Token),
            Client.PostAsync(ActionPath("open", poll.DeckId, target), null, bound.Token));
        var refused = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        var phases = new List<string?>();
        foreach (var slide in new[] { poll.Slide, target }.Distinct())
        {
            phases.Add((await ControlAsync(poll.DeckId, slide)).GetProperty("phase").GetString());
        }

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(code);
        phases.Count(phase => phase == "open").ShouldBe(1);
        (await ActivityCountAsync(poll.DeckId)).ShouldBe(entries + 1);
    }

    [Test]
    public async Task NewRound_WhenTwoArriveAtOnce_ShouldStartOneRoundAndRefuseTheOther()
    {
        var poll = await SeedPollAsync();
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);

        using var bound = new CancellationTokenSource(_raceBound);
        var responses = await Task.WhenAll(
            Client.PostAsync(ActionPath("round", poll.DeckId, poll.Slide), null, bound.Token),
            Client.PostAsync(ActionPath("round", poll.DeckId, poll.Slide), null, bound.Token));
        var refused = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        var view = await ControlAsync(poll.DeckId, poll.Slide);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(PollRefusals.WRONG_PHASE);
        view.GetProperty("phase").GetString().ShouldBe("open");
        view.GetProperty("number").GetInt32().ShouldBe(2);
        view.GetProperty("rounds").EnumerateArray().Select(Summary).ShouldBe(["1: 0 of 0"]);
    }

    [Test]
    public async Task Open_WhenRefusedWhileAnotherSlideHoldsADraft_ShouldLeaveTheDraftUntouched()
    {
        var poll = await SeedPollAsync();
        var (second, secondId) = await AddSecondPollAsync(poll);
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);
        var other = await Rows.RoundAsync(secondId, poll.SessionId);
        await Rows.CloseAsync(other.Id, joinedAtClose: 0);
        var draft = await Rows.RoundAsync(secondId, poll.SessionId, PollPhase.Draft, number: 2);

        var response = await Client.PostAsync(ActionPath("open", poll.DeckId, poll.Slide), null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var kept = await StoredRoundAsync(draft.Id);
        var view = await ControlAsync(poll.DeckId, second);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(PollRefusals.WRONG_PHASE);
        kept.ClosedAt.ShouldBeNull();
        kept.OpenedAt.ShouldBeNull();
        view.GetProperty("phase").GetString().ShouldBe("draft");
        view.GetProperty("number").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task EndLive_WhenARoundIsOpen_ShouldCloseItFirst()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest one", 1);
        var entries = await ActivityCountAsync(poll.DeckId);

        var response = await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live");
        var stored = await StoredRoundAsync(round.Id);
        var activity = await DeckSeed.ActivityAsync(Client, poll.DeckId);

        response.IsSuccessStatusCode.ShouldBeTrue();
        stored.ClosedAt.ShouldNotBeNull();
        stored.JoinedAtClose.ShouldBe(0);
        stored.Final.ShouldNotBeNull();
        stored.Final.Counts.ShouldBe([0, 1, 0]);
        stored.Final.Answered.ShouldBe(1);
        Order.For(poll.DeckId).ShouldBe([$"refresh after {entries + 1} entries", $"publish {poll.Slide}", "ended"]);
        activity.Select(a => a.GetProperty("summary").GetString()).ShouldContain($"closed poll on slide {poll.Slide} with 1 answer");
    }

    [Test]
    public async Task EndLive_WhenARoundIsADraft_ShouldDiscardIt()
    {
        var poll = await SeedPollAsync();
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);
        var draft = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft, number: 2);

        var response = await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live");
        var stored = await StoredRoundAsync(draft.Id);

        response.IsSuccessStatusCode.ShouldBeTrue();
        stored.ClosedAt.ShouldNotBeNull();
        stored.OpenedAt.ShouldBeNull();
        stored.Final.ShouldBeNull();
        stored.JoinedAtClose.ShouldBeNull();
    }

    [Test]
    public async Task Control_WhenSlideIsInsertedBefore_ShouldFollowThePoll()
    {
        var poll = await SeedPollAsync();
        var opened = await ActByRouteAsync("open", poll.DeckId, poll.Slide);
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs))
        {
            await DeckSeed.AddQuoteAsync(agent, poll.DeckId, "Ship on a Tuesday", position: 1);
        }

        var moved = await ControlAsync(poll.DeckId, poll.Slide + 1);
        var gone = await Client.GetAsync($"/api/decks/{poll.DeckId}/polls/{poll.Slide}");

        moved.GetProperty("slide").GetInt32().ShouldBe(poll.Slide + 1);
        moved.GetProperty("phase").GetString().ShouldBe("open");
        moved.GetProperty("round").GetString().ShouldBe(opened.GetProperty("round").GetString());
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestCase("open", "opened poll on slide 2")]
    [TestCase("close", "closed poll on slide 2 with 0 answers")]
    [TestCase("reveal", "revealed results of poll on slide 2")]
    [TestCase("answer", "showed answer of poll on slide 2")]
    [TestCase("round", "started round 2 of poll on slide 2")]
    public async Task PhaseChange_WhenAccepted_ShouldRecordActivityThenRefreshThenPublishOnce(string action, string summary)
    {
        var poll = await SeedPollAsync();
        await ArrangeForAsync(action, poll);
        var entries = await ActivityCountAsync(poll.DeckId);

        await ActByRouteAsync(action, poll.DeckId, poll.Slide);
        var activity = await DeckSeed.ActivityAsync(Client, poll.DeckId);

        (await ActivityCountAsync(poll.DeckId)).ShouldBe(entries + 1);
        activity[0].GetProperty("summary").GetString().ShouldBe(summary);
        Relay.RefreshedFor(poll.DeckId).ShouldBe(1);
        Publisher.PublishedFor(poll.DeckId).ShouldBe([poll.Slide]);
        Order.For(poll.DeckId).ShouldBe([$"refresh after {entries + 1} entries", $"publish {poll.Slide}"]);
    }

    // A round on the slide from which the action is accepted.
    private async Task ArrangeForAsync(string action, SeededPoll poll)
    {
        if (action == "open")
        {
            return;
        }
        if (action == "reveal")
        {
            await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Results = PollResults.AfterClose });
        }
        if (action == "answer")
        {
            await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Correct = 1 });
        }
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        if (action is "answer" or "round")
        {
            await Rows.CloseAsync(round.Id, joinedAtClose: 0);
        }
    }

    // A deck in the named state, and the slide the refused action is tried on.
    private async Task<(SeededPoll Poll, int Target)> ArrangeAsync(string state)
    {
        var poll = await SeedPollAsync();
        switch (state)
        {
            case "notLive":
                (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();
                return (poll, poll.Slide);
            case "draft":
                return (poll, poll.Slide);
            case "open":
                await Rows.RoundAsync(poll.SlideId, poll.SessionId);
                return (poll, poll.Slide);
            case "closed":
                await Rows.CloseAsync((await Rows.RoundAsync(poll.SlideId, poll.SessionId)).Id, joinedAtClose: 0);
                return (poll, poll.Slide);
            case "anotherOpen":
            {
                var (second, _) = await AddSecondPollAsync(poll);
                await Rows.RoundAsync(poll.SlideId, poll.SessionId);
                return (poll, second);
            }
            case "anotherOpenTargetClosed":
            {
                var (second, secondId) = await AddSecondPollAsync(poll);
                await Rows.CloseAsync((await Rows.RoundAsync(secondId, poll.SessionId)).Id, joinedAtClose: 0);
                await Rows.RoundAsync(poll.SlideId, poll.SessionId);
                return (poll, second);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private async Task<Refused> RefuseByRouteAsync(string action, string state)
    {
        var (poll, target) = await ArrangeAsync(state);
        var before = await SnapshotAsync(poll, target);
        var response = await Client.PostAsync(ActionPath(action, poll.DeckId, target), null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Refused(response.StatusCode, body.GetProperty("error").GetString(), before, await SnapshotAsync(poll, target));
    }

    // A second poll slide, added after the first and so slide 3, and its id.
    private async Task<(int Slide, Guid SlideId)> AddSecondPollAsync(SeededPoll poll)
    {
        int slide;
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs))
        {
            var added = await DeckSeed.AddSlideAsync(agent, poll.DeckId, "poll", JsonSerializer.Serialize(new { question = SECOND_QUESTION, options = _secondOptions }));
            slide = added.GetProperty("slide").GetInt32();
        }
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await db.Slides.AsNoTracking().Where(s => s.DeckId == poll.DeckId && s.DeletedAt == null && s.Position == slide - 1).Select(s => s.Id).SingleAsync();
        return (slide, id);
    }

    // What a refused action must not change: the poll views of the first poll and the target slide (seq left out), the
    // deck's activity, and the publishes and refreshes for the deck.
    private async Task<Snapshot> SnapshotAsync(SeededPoll poll, int target)
    {
        var views = new List<string>();
        foreach (var slide in new[] { poll.Slide, target }.Distinct())
        {
            var view = JsonNode.Parse((await ControlAsync(poll.DeckId, slide)).GetRawText())!.AsObject();
            view.Remove("seq");
            views.Add(view.ToJsonString());
        }
        return new Snapshot(
            views,
            await ActivityCountAsync(poll.DeckId),
            Publisher.PublishedFor(poll.DeckId).Count,
            Relay.RefreshedFor(poll.DeckId));
    }

    private async Task<JsonElement> ControlAsync(string deckId, int slide)
    {
        var response = await Client.GetAsync($"/api/decks/{deckId}/polls/{slide}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ActByRouteAsync(string action, string deckId, int slide)
    {
        var response = await Client.PostAsync(ActionPath(action, deckId, slide), null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task ActByServiceAsync(string action, string deckId, int slide)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var polls = scope.ServiceProvider.GetRequiredService<IPolls>();
        Func<string, int, Task<PollControlView>> act = action switch
        {
            "open" => polls.OpenAsync,
            "close" => polls.CloseAsync,
            "reveal" => polls.RevealAsync,
            "answer" => polls.ShowAnswerAsync,
            "round" => polls.NewRoundAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        await act(deckId, slide);
    }

    private static string ActionPath(string action, string deckId, int slide) => $"/api/decks/{deckId}/polls/{slide}/{action}";

    private async Task<int> ActivityCountAsync(string deckId) => await ActivityCountAsync(Factory.Services.GetRequiredService<IServiceScopeFactory>(), deckId);

    private static async Task<int> ActivityCountAsync(IServiceScopeFactory scopes, string deckId)
    {
        await using var scope = scopes.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IActivityLog>().ListAsync(deckId, 1000)).Count;
    }

    // The round's row as stored, read back to check what an action wrote; no test writes through this.
    private async Task<Features.Polls.Data.PollRound> StoredRoundAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PollRounds.AsNoTracking().SingleAsync(r => r.Id == roundId);
    }

    private static string Summary(JsonElement round)
        => $"{round.GetProperty("number").GetInt32()}: {round.GetProperty("answered").GetInt32()} of {round.GetProperty("joined").GetInt32()}";

    private sealed record Snapshot(IReadOnlyList<string> Views, int Activity, int Published, int Refreshed)
    {
        public bool Equals(Snapshot? other)
            => other != null && Views.SequenceEqual(other.Views) && Activity == other.Activity && Published == other.Published && Refreshed == other.Refreshed;

        public override int GetHashCode() => HashCode.Combine(Activity, Published, Refreshed);

        public override string ToString() => $"{string.Join(" | ", Views)}; activity {Activity}; published {Published}; refreshed {Refreshed}";
    }

    private sealed record Refused(HttpStatusCode Status, string? Error, Snapshot Before, Snapshot After);

    // The relay's refresh, its end and the publisher's call for each deck, in the order they happened. A refresh notes how
    // many activity entries the deck had by then, which shows the entry was recorded before it.
    private sealed class CallOrder
    {
        private readonly Lock _gate = new();
        private readonly List<(string DeckId, string Call)> _calls = [];

        public void Add(string deckId, string call)
        {
            lock (_gate)
            {
                _calls.Add((deckId, call));
            }
        }

        public IReadOnlyList<string> For(string deckId)
        {
            lock (_gate)
            {
                return [.. _calls.Where(c => c.DeckId == deckId).Select(c => c.Call)];
            }
        }
    }

    private sealed class OrderedRelay(FakeAudienceRelay inner, CallOrder order, IServiceScopeFactory scopes) : IAudienceRelay
    {
        public Task<AudienceState?> CurrentAsync(string deckId) => inner.CurrentAsync(deckId);

        public Task StartedAsync(string deckId) => inner.StartedAsync(deckId);

        public Task EndedAsync(string deckId)
        {
            order.Add(deckId, "ended");
            return inner.EndedAsync(deckId);
        }

        public async Task RefreshAsync(string deckId)
        {
            order.Add(deckId, $"refresh after {await ActivityCountAsync(scopes, deckId)} entries");
            await inner.RefreshAsync(deckId);
        }
    }

    private sealed class OrderedPublisher(FakePollPublisher inner, CallOrder order) : IPollPublisher
    {
        public Task PublishAsync(string deckId, int slide)
        {
            order.Add(deckId, $"publish {slide}");
            return inner.PublishAsync(deckId, slide);
        }
    }
}
