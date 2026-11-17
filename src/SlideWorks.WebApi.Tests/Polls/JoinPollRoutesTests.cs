using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// A guest's poll routes (V5) over FakePollAnswers: GET /api/join/{code}/poll and PUT /api/join/{code}/poll/answer, each
// AnswerResult case on the wire, the body's shape rules, the sw_guest cookie, and the cross-site guard on the join host (J7).
// The join host is configured so a guest's page served from it can answer. The app, and so FakePollAnswers, is shared by the
// fixture: each test uses a code of its own, and what a submit returns is reset after each.
[TestFixture]
public class JoinPollRoutesTests : ApiTestFixture
{
    private const string JOIN = "http://join.example";
    private const string ROUND = "0b7c6a52-1f0e-4f43-9d3a-5a1c2e8b7d10";
    private static readonly string[] _options = ["Under 50 lines", "50 to 200 lines", "200 to 1,000 lines", "Over 1,000 lines"];

    private string _code = null!;

    protected override Dictionary<string, string?> ExtraConfig => new() { ["SLIDEWORKS_JOIN_URL"] = JOIN };

    protected override Action<IServiceCollection>? ConfigureTestServices => FakePollAnswers.AddTo;

    private FakePollAnswers Answers => Factory.Services.GetRequiredService<FakePollAnswers>();

    [SetUp]
    public void NewCode() => _code = Random.Shared.Next(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    [TearDown]
    public void AcceptAgain() => Answers.Reset();

    [Test]
    public async Task GetPoll_WhenGuestHasAnswered_ShouldReturnTheView()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Answers.SetView(_code, Answered());

        var response = await guest.GetPollAsync(_code);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example("guest-poll.json").GetProperty("answered"), "guest-poll.json answered", ScreenShapes.GuestPoll);
        body.GetProperty("activity").GetProperty("round").GetString().ShouldBe(ROUND);
        body.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(2);
        body.GetProperty("tally").GetProperty("type").GetString().ShouldBe("pollTally");
        body.GetProperty("tally").GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([12, 25, 14, 6]);
        body.GetProperty("joinedLate").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task GetPoll_WhenNoGuestCookie_ShouldReturn400NoGuest()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);
        Answers.SetView(_code, Answered());

        var response = await guest.GetPollAsync(_code);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("noGuest"), "poll-answer.json noGuest");
        body.GetProperty("error").GetString().ShouldBe(GuestPolls.NO_GUEST);
    }

    [Test]
    public async Task GetPoll_WhenCodeHasNoSession_ShouldReturn404NoSession()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.GetPollAsync(_code);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("noSession"), "poll-answer.json noSession");
        body.GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    [Test]
    public async Task Answer_WhenResultIsAccepted_ShouldReturn200WithPicksAndRevision()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.AnswerAsync(_code, new AnswerRequest(ROUND, [1], 0));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("accepted"), "poll-answer.json accepted");
        body.GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1]);
        body.GetProperty("revision").GetInt32().ShouldBe(1);
        var submitted = Answers.SubmittedFor(_code).ShouldHaveSingleItem();
        submitted.GuestId.ShouldBe(guest.GuestId);
        submitted.Request.Round.ShouldBe(ROUND);
        submitted.Request.Picks.ShouldBe([1]);
        submitted.Request.Revision.ShouldBe(0);
    }

    [Test]
    public async Task Answer_WhenResultIsStale_ShouldReturn409WithCurrent()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Answers.Answer(_ => new AnswerResult.Stale(Answered()));

        var response = await guest.AnswerAsync(_code, new AnswerRequest(ROUND, [1], 1));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("stale"), "poll-answer.json stale");
        body.GetProperty("error").GetString().ShouldBe(StaleAnswer.ERROR);
        body.GetProperty("current").GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(2);
        body.GetProperty("current").GetProperty("activity").GetProperty("phase").GetString().ShouldBe("closed");
    }

    // One test per rule IPollAnswers names (V5): the message reaches the wire as 400 { "error": message }.
    [TestCase("picks: pick one")]
    [TestCase("picks: at most 2")]
    [TestCase("picks: unknown option 7")]
    [TestCase("picks: duplicate")]
    [TestCase("picks: required")]
    public async Task Answer_WhenResultIsInvalid_ShouldReturn400WithTheRule(string rule)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Answers.Answer(_ => new AnswerResult.Invalid(rule));

        var response = await guest.AnswerAsync(_code, new AnswerRequest(ROUND, [1, 2], 0));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("invalid")[0], "poll-answer.json invalid");
        body.GetProperty("error").GetString().ShouldBe(rule);
    }

    [Test]
    public async Task Answer_WhenResultIsLimited_ShouldReturn429TooManyAnswers()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Answers.Answer(_ => new AnswerResult.Limited());

        var response = await guest.AnswerAsync(_code, new AnswerRequest(ROUND, [1], 0));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("tooManyAnswers"), "poll-answer.json tooManyAnswers");
        body.GetProperty("error").GetString().ShouldBe(AnswerResults.TOO_MANY_ANSWERS);
    }

    [Test]
    public async Task Answer_WhenResultIsNoSession_ShouldReturn404NoSession()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Answers.Answer(_ => new AnswerResult.NoSession());

        var response = await guest.AnswerAsync(_code, new AnswerRequest(ROUND, [1], 0));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("noSession"), "poll-answer.json noSession");
        body.GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    [Test]
    public async Task Answer_WhenNoGuestCookie_ShouldReturn400NoGuestAndSubmitNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);

        var response = await guest.AnswerAsync(_code, new AnswerRequest(ROUND, [1], 0));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("noGuest"), "poll-answer.json noGuest");
        body.GetProperty("error").GetString().ShouldBe(GuestPolls.NO_GUEST);
        Answers.SubmittedFor(_code).ShouldBeEmpty();
    }

    // One test per body rule (AnswerBody): a missing member is named, anything else is the body's; nothing is submitted.
    [TestCase("""{"picks":[1],"revision":0}""", AnswerBody.ROUND_REQUIRED)]
    [TestCase("""{"round":"r","revision":0}""", AnswerBody.PICKS_REQUIRED)]
    [TestCase("""{"round":"r","picks":null,"revision":0}""", AnswerBody.PICKS_REQUIRED)]
    [TestCase("""{"round":"r","picks":[1]}""", AnswerBody.REVISION_REQUIRED)]
    [TestCase("""{"round":"r","picks":["1"],"revision":0}""", AnswerBody.BODY_INVALID)]
    [TestCase("""{"round":7,"picks":[1],"revision":0}""", AnswerBody.BODY_INVALID)]
    [TestCase("""{"round":"r","picks":[1],"revision":1.5}""", AnswerBody.BODY_INVALID)]
    [TestCase("""[1]""", AnswerBody.BODY_INVALID)]
    public async Task Answer_WhenBodyBreaksItsShape_ShouldReturn400NamingTheRuleAndSubmitNothing(string json, string error)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.AnswerAsync(_code, JsonSerializer.Deserialize<JsonElement>(json));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example("poll-answer.json").GetProperty("noGuest"), "an error body");
        body.GetProperty("error").GetString().ShouldBe(error);
        Answers.SubmittedFor(_code).ShouldBeEmpty();
    }

    [Test]
    public async Task Answer_WhenBodyIsEmpty_ShouldReturn400BodyInvalid()
    {
        using var client = RawClient();
        using var request = AnswerRequestTo(TestAppFactory.PUBLIC_URL, TestAppFactory.PUBLIC_URL);
        request.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe(AnswerBody.BODY_INVALID);
        Answers.SubmittedFor(_code).ShouldBeEmpty();
    }

    // J7: the guest's page may be served from the join host, so its Origin is the join origin, which the cross-site guard
    // takes as SlideWorks' own on that host.
    [Test]
    public async Task Answer_WhenFromJoinHostOrigin_ShouldBeAccepted()
    {
        using var client = RawClient();
        using var request = AnswerRequestTo(JOIN, JOIN);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Answers.SubmittedFor(_code).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Answer_WhenFromForeignOrigin_ShouldBeRefused()
    {
        using var client = RawClient();
        using var request = AnswerRequestTo(JOIN, "http://attacker.example");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Answers.SubmittedFor(_code).ShouldBeEmpty();
    }

    // A browser with a guest cookie but no Origin header of the fixture's own, so each test sets the Origin it means.
    private HttpClient RawClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private HttpRequestMessage AnswerRequestTo(string host, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{host}/api/join/{_code}/poll/answer")
        {
            Content = JsonContent.Create(new AnswerRequest(ROUND, [1], 0)),
        };
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={GuestCookie.NewId()}");
        return request;
    }

    // The "answered" sample of guest-poll.json.
    private static GuestPollView Answered() => new(
        new PollActivity(ROUND, 1, PollPhase.Closed, "How big was your last merge request?", _options, PollPick.UpTo, 2, PollResults.AfterAnswer, true, 2),
        new MyAnswer([1, 2], 2),
        new PollTally("deck", PollTallyTarget.Audience, ROUND, 38, [12, 25, 14, 6]),
        false);

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
