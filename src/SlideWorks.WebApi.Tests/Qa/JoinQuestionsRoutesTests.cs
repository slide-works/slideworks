using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// A guest's question routes (Q4) over FakeGuestQuestions and FakeQuestionAsking: GET /api/join/{code}/questions, POST
// /api/join/{code}/questions and PUT /api/join/{code}/questions/votes, each result of AskResult and VoteResult on the wire,
// the body limits, the sw_guest cookie and the join-code miss budget (GuestQa). The code is resolved through
// FakeAudienceSessions: a code it doesn't know is a miss and never reaches a seam. The app, and so every fake, is shared by
// the fixture: each test uses a code of its own and a new window of the miss budget.
[TestFixture]
public class JoinQuestionsRoutesTests : PollTestFixture
{
    private const string ROUND = "4f1d2c8e-9a7b-4e3f-8c21-6b5a0d9e7f12";

    private string _code = null!;

    private FakeGuestQuestions Questions => Factory.Services.GetRequiredService<FakeGuestQuestions>();

    private FakeQuestionAsking Asking => Factory.Services.GetRequiredService<FakeQuestionAsking>();

    private FakeAudienceSessions Sessions => Factory.Services.GetRequiredService<FakeAudienceSessions>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceSessions.AddTo(services);
        FakeGuestQuestions.AddTo(services);
        FakeQuestionAsking.AddTo(services);
    }

    // A live code of the test's own, and a new window of the join-code limit: every request here comes from the same address,
    // so the misses earlier tests made would otherwise count against this one.
    [SetUp]
    public void NewCodeAndWindow()
    {
        _code = NewCode();
        Sessions.Set(_code, $"deck-{_code}", Guid.NewGuid().ToString(), AudiencePhase.Live);
        Clock.Advance(TimeSpan.FromMinutes(2));
    }

    [Test]
    public async Task GetQuestions_WhenGuestIsOnTheRoster_ShouldReturnTheView()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetView(_code, View());

        var response = await guest.GetQuestionsAsync(_code);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example("guest-questions.json").GetProperty("view"), "guest-questions.json view", ScreenShapes.GuestQuestions);
        body.GetProperty("room").GetProperty("round").GetString().ShouldBe(ROUND);
        body.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3, 4, 6]);
        body.GetProperty("questions")[3].GetProperty("state").GetString().ShouldBe("waiting");
        body.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(3);
        Questions.ViewsFor(_code).ShouldBe([guest.GuestId]);
    }

    [Test]
    public async Task GetQuestions_WhenNoCookie_ShouldAnswer400NoGuest()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);
        Questions.SetView(_code, View());

        var response = await guest.GetQuestionsAsync(_code);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("guest-questions.json").GetProperty("guestErrors")[0], "guest-questions.json guestErrors");
        body.GetProperty("error").GetString().ShouldBe(GuestPolls.NO_GUEST);
        Questions.ViewsFor(_code).ShouldBeEmpty();
    }

    // An unknown code is a miss: after the budget's worth of them, even this test's live code is 429 too_many_attempts, in the
    // join lookup's own body, and the seam was never asked about an unknown code.
    [Test]
    public async Task GetQuestions_WhenCodeUnknown_ShouldCountAMiss()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetView(_code, View());
        var unknown = NewCode();
        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            var missed = await guest.GetQuestionsAsync(unknown);
            missed.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"miss {i + 1}");
            (await BodyAsync(missed)).GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
        }

        var refused = await guest.GetQuestionsAsync(_code);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await refused.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_attempts"}""");
        Questions.ViewsFor(unknown).ShouldBeEmpty();
        Questions.ViewsFor(_code).ShouldBeEmpty();
    }

    // A known code never counts: a guest the seam doesn't find on the roster is 404 no_session as often as it asks, and the
    // caller still has its whole miss budget afterwards.
    [Test]
    public async Task GetQuestions_WhenGuestNotOnRoster_ShouldAnswer404WithoutAMiss()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        for (var i = 0; i < 2 * JoinLookupLimiter.MAX_MISSES; i++)
        {
            var response = await guest.GetQuestionsAsync(_code);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"read {i + 1}");
            (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
        }

        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            (await guest.GetQuestionsAsync(NewCode())).StatusCode.ShouldBe(HttpStatusCode.NotFound, $"miss {i + 1}");
        }
        (await guest.GetQuestionsAsync(_code)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        Questions.ViewsFor(_code).Count.ShouldBe(2 * JoinLookupLimiter.MAX_MISSES);
    }

    // One budget per caller: misses on the question routes and on GET /api/join/{code} add up.
    [Test]
    public async Task GetQuestions_WhenCallerMissesHereAndOnTheJoinLookup_ShouldShareOneBudget()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        using var anonymous = ClientFor(TestUser.Anonymous);
        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES / 2; i++)
        {
            (await anonymous.GetAsync($"/api/join/{NewCode()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound, $"join miss {i + 1}");
            (await guest.GetQuestionsAsync(NewCode())).StatusCode.ShouldBe(HttpStatusCode.NotFound, $"questions miss {i + 1}");
        }

        var joinRefused = await anonymous.GetAsync($"/api/join/{NewCode()}");
        var questionsRefused = await guest.GetQuestionsAsync(_code);

        joinRefused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        questionsRefused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await questionsRefused.Content.ReadAsStringAsync()).ShouldBe(await joinRefused.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Ask_WhenAccepted_ShouldAnswer201WithTheQuestion()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Asked(
            new GuestQuestion(6, "How do you handle database migrations?", "Priya", "now", QuestionStatus.Open, 1, false, true),
            new MyVotes([1, 6], 4)));

        var response = await guest.AskAsync(_code, new AskRequest("How do you handle database migrations?", "Priya"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonShape.AssertMatches(body, Example("question-ask.json").GetProperty("created"), "question-ask.json created");
        body.GetProperty("question").GetProperty("number").GetInt32().ShouldBe(6);
        body.GetProperty("question").GetProperty("yours").GetBoolean().ShouldBeTrue();
        body.GetProperty("mine").GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1, 6]);
        body.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(4);
        var call = Asking.CallsFor(_code).ShouldHaveSingleItem();
        call.GuestId.ShouldBe(guest.GuestId);
        call.Request.ShouldBe(new AskRequest("How do you handle database migrations?", "Priya"));
    }

    // An anonymous question's name is null on the way in, and missing members bind as null for the seam's rules.
    [TestCase("""{"text":"Why?","name":null}""", "Why?", null)]
    [TestCase("""{"text":"Why?"}""", "Why?", null)]
    [TestCase("""{}""", null, null)]
    public async Task Ask_WhenNameOrTextIsLeftOut_ShouldPassNullToTheSeam(string json, string? text, string? name)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Invalid("text: required"));

        await guest.AskAsync(_code, JsonSerializer.Deserialize<JsonElement>(json));

        Asking.CallsFor(_code).ShouldHaveSingleItem().Request.ShouldBe(new AskRequest(text, name));
    }

    // One test per rule QuestionText names (Q4): the message reaches the wire as 400 { "error": message }.
    [TestCase("text: required")]
    [TestCase("text: at most 200")]
    [TestCase("name: at most 30")]
    [TestCase("name: not allowed")]
    public async Task Ask_WhenInvalid_ShouldAnswer400WithTheRule(string rule)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Invalid(rule));

        var response = await guest.AskAsync(_code, new AskRequest("Why?", null));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("question-ask.json").GetProperty("invalid")[0], "question-ask.json invalid");
        body.GetProperty("error").GetString().ShouldBe(rule);
    }

    [Test]
    public async Task Ask_WhenClosed_ShouldAnswer409QuestionsClosed()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Refused(GuestQaErrors.QUESTIONS_CLOSED));

        var response = await guest.AskAsync(_code, new AskRequest("Why?", null));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example("question-ask.json").GetProperty("refusals")[0], "question-ask.json refusals");
        body.GetProperty("error").GetString().ShouldBe("questions_closed");
    }

    [TestCase(GuestQaErrors.QUESTIONS_OFF)]
    [TestCase(GuestQaErrors.QUESTIONS_OFF_HERE)]
    public async Task Ask_WhenRefused_ShouldAnswer409WithTheCode(string code)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Refused(code));

        var response = await guest.AskAsync(_code, new AskRequest("Why?", null));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example("question-ask.json").GetProperty("refusals")[0], "question-ask.json refusals");
        body.GetProperty("error").GetString().ShouldBe(code);
    }

    [Test]
    public async Task Ask_WhenLimited_ShouldAnswer429WithRetryAfter()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Limited(42));

        var response = await guest.AskAsync(_code, new AskRequest("Why?", null));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        JsonShape.AssertMatches(body, Example("question-ask.json").GetProperty("tooManyQuestions"), "question-ask.json tooManyQuestions");
        body.GetProperty("error").GetString().ShouldBe("too_many_questions");
        body.GetProperty("retryAfter").GetInt32().ShouldBe(42);
    }

    // A known code whose guest isn't on the roster: the seam's NoSession is 404, and no miss.
    [Test]
    public async Task Ask_WhenSeamAnswersNoSession_ShouldAnswer404NoSession()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.NoSession());

        var response = await guest.AskAsync(_code, new AskRequest("Why?", null));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonShape.AssertMatches(body, Example("guest-questions.json").GetProperty("guestErrors")[1], "guest-questions.json guestErrors");
        body.GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    [Test]
    public async Task Ask_WhenCodeUnknown_ShouldAnswer404AndAskNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var unknown = NewCode();

        var response = await guest.AskAsync(unknown, new AskRequest("Why?", null));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
        Asking.CallsFor(unknown).ShouldBeEmpty();
    }

    [Test]
    public async Task Ask_WhenNoCookie_ShouldAnswer400NoGuestAndAskNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);

        var response = await guest.AskAsync(_code, new AskRequest("Why?", null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestPolls.NO_GUEST);
        Asking.CallsFor(_code).ShouldBeEmpty();
    }

    [Test]
    public async Task Ask_WhenBodyIsOver2Kb_ShouldAnswer413()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.AskAsync(_code, new AskRequest(new string('a', AskBody.MAX_BYTES), null));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        body.GetProperty("error").GetString().ShouldBe(BodyLimitAttribute.TooLarge(AskBody.MAX_BYTES));
        Asking.CallsFor(_code).ShouldBeEmpty();
    }

    // The same limit when the body comes in chunks with no Content-Length: it is still read no further than the limit.
    [Test]
    public async Task Ask_WhenChunkedBodyIsOver2Kb_ShouldAnswer413()
    {
        using var client = RawClient();
        using var request = GuestRequest(HttpMethod.Post, $"/api/join/{_code}/questions");
        request.Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes($$"""{"text":"{{new string('a', AskBody.MAX_BYTES)}}"}""")));
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.TransferEncodingChunked = true;

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        Asking.CallsFor(_code).ShouldBeEmpty();
    }

    // A question just under the limit is read whole and reaches the seam.
    [Test]
    public async Task Ask_WhenBodyIsJustUnder2Kb_ShouldAsk()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Asking.Set(_code, new AskResult.Invalid("text: at most 200"));
        var text = new string('a', AskBody.MAX_BYTES - """{"text":"","name":null}""".Length);

        var response = await guest.AskAsync(_code, new AskRequest(text, null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Asking.CallsFor(_code).ShouldHaveSingleItem().Request.Text.ShouldBe(text);
    }

    // A body that isn't JSON gets the framework's 400, and nothing is asked.
    [Test]
    public async Task Ask_WhenBodyIsNotJson_ShouldAnswer400AndAskNothing()
    {
        using var client = RawClient();
        using var request = GuestRequest(HttpMethod.Post, $"/api/join/{_code}/questions");
        request.Content = new StringContent("text=Why", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Asking.CallsFor(_code).ShouldBeEmpty();
    }

    [Test]
    public async Task Votes_WhenVoted_ShouldAnswer200WithPicksAndRevision()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Voted([1, 2, 4], 4));

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [1, 2, 4], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example("question-votes.json").GetProperty("accepted"), "question-votes.json accepted");
        body.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1, 2, 4]);
        body.GetProperty("revision").GetInt32().ShouldBe(4);
        var call = Questions.VotesFor(_code).ShouldHaveSingleItem();
        call.GuestId.ShouldBe(guest.GuestId);
        call.Request.Round.ShouldBe(ROUND);
        call.Request.Picks.ShouldBe([1, 2, 4]);
        call.Request.Revision.ShouldBe(3);
    }

    [Test]
    public async Task Votes_WhenStale_ShouldAnswer409WithTheCurrentView()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Stale(View()));

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [1, 2, 4], 2));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example("question-votes.json").GetProperty("stale"), "question-votes.json stale");
        body.GetProperty("error").GetString().ShouldBe("stale");
        body.GetProperty("current").GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(3);
        body.GetProperty("current").GetProperty("room").GetProperty("listRevision").GetInt64().ShouldBe(57);
    }

    [TestCase("picks: unknown question 7")]
    [TestCase("picks: duplicate")]
    public async Task Votes_WhenInvalid_ShouldAnswer400WithTheRule(string rule)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Invalid(rule));

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [7], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("question-votes.json").GetProperty("invalid")[0], "question-votes.json invalid");
        body.GetProperty("error").GetString().ShouldBe(rule);
    }

    [Test]
    public async Task Votes_WhenQuestionsOff_ShouldAnswer409QuestionsOff()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Refused(GuestQaErrors.QUESTIONS_OFF));

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [1], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example("question-votes.json").GetProperty("refusals")[0], "question-votes.json refusals");
        body.GetProperty("error").GetString().ShouldBe("questions_off");
    }

    [Test]
    public async Task Votes_WhenLimited_ShouldAnswer429TooManyVotes()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Limited());

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [1], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        JsonShape.AssertMatches(body, Example("question-votes.json").GetProperty("tooManyVotes"), "question-votes.json tooManyVotes");
        body.GetProperty("error").GetString().ShouldBe("too_many_votes");
    }

    [Test]
    public async Task Votes_WhenSeamAnswersNoSession_ShouldAnswer404NoSession()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.NoSession());

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [1], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    [Test]
    public async Task Votes_WhenCodeUnknown_ShouldAnswer404AndVoteNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var unknown = NewCode();

        var response = await guest.VoteAsync(unknown, new VoteRequest(ROUND, [1], 3));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
        Questions.VotesFor(unknown).ShouldBeEmpty();
    }

    [Test]
    public async Task Votes_WhenNoCookie_ShouldAnswer400NoGuestAndVoteNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [1], 3));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestPolls.NO_GUEST);
        Questions.VotesFor(_code).ShouldBeEmpty();
    }

    [Test]
    public async Task Votes_WhenMoreThan1000Picks_ShouldAnswer400AndVoteNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [.. Enumerable.Range(1, VotesBody.MAX_PICKS + 1)], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(VotesBody.TOO_MANY_PICKS);
        Questions.VotesFor(_code).ShouldBeEmpty();
    }

    // The most picks a guest may send fits inside the body limit and reaches the seam.
    [Test]
    public async Task Votes_When1000Picks_ShouldVote()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Invalid("picks: unknown question 1000"));

        var response = await guest.VoteAsync(_code, new VoteRequest(ROUND, [.. Enumerable.Range(9001, VotesBody.MAX_PICKS)], 3));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Questions.VotesFor(_code).ShouldHaveSingleItem().Request.Picks.Count.ShouldBe(VotesBody.MAX_PICKS);
    }

    [Test]
    public async Task Votes_WhenBodyIsOver16Kb_ShouldAnswer413AndVoteNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.VoteAsync(_code, new VoteRequest(new string('r', VotesBody.MAX_BYTES), [1], 3));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        body.GetProperty("error").GetString().ShouldBe(BodyLimitAttribute.TooLarge(VotesBody.MAX_BYTES));
        Questions.VotesFor(_code).ShouldBeEmpty();
    }

    // Every member is required: a missing one, a revision that isn't a whole number or a pick that isn't one gets the
    // framework's 400, and a missing revision never becomes a first vote.
    [TestCase("""{"picks":[1],"revision":3}""")]
    [TestCase("""{"round":"r","revision":3}""")]
    [TestCase("""{"round":"r","picks":[1]}""")]
    [TestCase("""{"round":null,"picks":[1],"revision":3}""")]
    [TestCase("""{"round":"r","picks":null,"revision":3}""")]
    [TestCase("""{"round":"r","picks":["1"],"revision":3}""")]
    [TestCase("""{"round":"r","picks":[1],"revision":1.5}""")]
    [TestCase("""[1]""")]
    public async Task Votes_WhenBodyBreaksItsShape_ShouldAnswer400AndVoteNothing(string json)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Questions.SetVote(_code, new VoteResult.Voted([1], 4));

        var response = await guest.VoteAsync(_code, JsonSerializer.Deserialize<JsonElement>(json));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Questions.VotesFor(_code).ShouldBeEmpty();
    }

    // J7: the guest's page may be served from the join host; a write from another site is refused before the route.
    [Test]
    public async Task Ask_WhenFromForeignOrigin_ShouldBeRefused()
    {
        using var client = RawClient();
        using var request = GuestRequest(HttpMethod.Post, $"/api/join/{_code}/questions", "http://attacker.example");
        request.Content = JsonContent.Create(new AskRequest("Why?", null));

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Asking.CallsFor(_code).ShouldBeEmpty();
    }

    private static string NewCode() => Random.Shared.Next(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private HttpClient RawClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    // A guest's request with a cookie and the given Origin (the public one by default).
    private static HttpRequestMessage GuestRequest(HttpMethod method, string path, string origin = TestAppFactory.PUBLIC_URL)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={GuestCookie.NewId()}");
        return request;
    }

    // The "view" sample of guest-questions.json.
    private static GuestQuestionsView View() => new(
        new QaRoom(ROUND, QaRoomQuestions.Open, true, true, 57),
        [
            new GuestQuestion(1, "How do you split a big change?", "Priya", "12 min", QuestionStatus.Open, 14, false, false),
            new GuestQuestion(2, "What is the smallest slice you have shipped?", null, "9 min", QuestionStatus.Open, 9, true, false),
            new GuestQuestion(3, "Do feature flags count as shipping?", "Sam K", "8 min", QuestionStatus.Answered, 6, false, false),
            new GuestQuestion(4, "Who reviews the slices?", null, "2 min", QuestionStatus.Waiting, 1, false, true),
            new GuestQuestion(6, "How do you handle database migrations?", null, "now", QuestionStatus.Open, 3, false, false),
        ],
        new MyVotes([1, 4], 3));

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
