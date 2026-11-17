using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The presenter's Q&A routes (Q6) over FakeQuestionModeration and FakeQaSettings: who may call them (a person who can
// present the deck; 404 when the deck can't be viewed, 403 when it can be viewed but not presented), that a refused call
// reaches no seam, each refusal on the wire, and the settings routes answering the view read after the change. The app, and
// so every fake, is shared by the fixture: each test makes its own deck, and a refusal is reset after each.
[TestFixture]
public class QaRoutesTests : PollTestFixture
{
    private const string ROUND = "4f1d2c8e-9a7b-4e3f-8c21-6b5a0d9e7f12";

    private FakeQuestionModeration Moderation => Factory.Services.GetRequiredService<FakeQuestionModeration>();

    private FakeQaSettings Settings => Factory.Services.GetRequiredService<FakeQaSettings>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeQuestionModeration.AddTo(services);
        FakeQaSettings.AddTo(services);
    }

    [TearDown]
    public void AcceptAgain()
    {
        Moderation.FailWith(null);
        Settings.FailWith(null);
    }

    [Test]
    public async Task Get_WhenCallerCanPresent_ShouldReturnTheControlView()
    {
        var deck = await QaDeckAsync();

        var response = await Client.GetAsync($"/api/decks/{deck}/qa");
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example().GetProperty("live"), "qa-control.json live", ScreenShapes.QaControl);
        body.GetProperty("seq").GetInt64().ShouldBe(212);
        body.GetProperty("round").GetString().ShouldBe(ROUND);
        body.GetProperty("settings").GetProperty("showOn").GetString().ShouldBe("anySlide");
        body.GetProperty("questions")[4].GetProperty("hidden").GetBoolean().ShouldBeTrue();
        body.GetProperty("reactions").GetProperty("clap").GetInt32().ShouldBe(28);
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenCallerCannotPresent_ShouldReturn403()
    {
        var deck = await QaDeckAsync(sharing: "link");
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.GetAsync($"/api/decks/{deck}/qa");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Get_WhenDeckNotVisible_ShouldReturn404()
    {
        var deck = await QaDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.GetAsync($"/api/decks/{deck}/qa");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestCase("approve", "Approve")]
    [TestCase("hide", "Hide")]
    [TestCase("restore", "Restore")]
    [TestCase("answer", "Answer")]
    [TestCase("reopen", "Reopen")]
    [TestCase("show", "Show")]
    [TestCase("unshow", "Unshow")]
    public async Task Post_WhenCallerCanPresent_ShouldCallTheActionOnceAndReturnItsView(string route, string action)
    {
        var deck = await QaDeckAsync();

        var response = await Client.PostAsync($"/api/decks/{deck}/qa/questions/3/{route}", null);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example().GetProperty("live"), "qa-control.json live", ScreenShapes.QaControl);
        body.GetProperty("round").GetString().ShouldBe(ROUND);
        Moderation.CallsFor(deck).ShouldBe([$"{action} 3"]);
    }

    [Test]
    public async Task Approve_WhenCallerCanViewButNotPresent_ShouldAnswer403()
    {
        var deck = await QaDeckAsync(sharing: "link");
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsync($"/api/decks/{deck}/qa/questions/3/approve", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Approve_WhenDeckNotVisible_ShouldAnswer404()
    {
        var deck = await QaDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsync($"/api/decks/{deck}/qa/questions/3/approve", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    // Q6: someone who can view the deck but not present it is refused before any seam is called, on every route.
    [TestCase("POST", "/questions/3/hide")]
    [TestCase("POST", "/questions/3/restore")]
    [TestCase("POST", "/questions/3/answer")]
    [TestCase("POST", "/questions/3/reopen")]
    [TestCase("POST", "/questions/3/show")]
    [TestCase("POST", "/questions/3/unshow")]
    [TestCase("PUT", "/settings")]
    [TestCase("PUT", "/slides/3")]
    public async Task QaRoute_WhenCallerCannotPresent_ShouldAnswer403AndCallNothing(string method, string route)
    {
        var deck = await QaDeckAsync(sharing: "link");
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.SendAsync(RequestTo(method, $"/api/decks/{deck}/qa{route}"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Moderation.CallsFor(deck).ShouldBeEmpty();
        Settings.CallsFor(deck).ShouldBeEmpty();
    }

    [TestCase("POST", "/questions/3/hide")]
    [TestCase("POST", "/questions/3/restore")]
    [TestCase("POST", "/questions/3/answer")]
    [TestCase("POST", "/questions/3/reopen")]
    [TestCase("POST", "/questions/3/show")]
    [TestCase("POST", "/questions/3/unshow")]
    [TestCase("PUT", "/settings")]
    [TestCase("PUT", "/slides/3")]
    public async Task QaRoute_WhenDeckNotVisible_ShouldAnswer404AndCallNothing(string method, string route)
    {
        var deck = await QaDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.SendAsync(RequestTo(method, $"/api/decks/{deck}/qa{route}"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Moderation.CallsFor(deck).ShouldBeEmpty();
        Settings.CallsFor(deck).ShouldBeEmpty();
    }

    // The presenter's routes are not anonymous: a caller with no sign-in meets the sign-in gate, never a seam.
    [TestCase("GET", "")]
    [TestCase("POST", "/questions/3/approve")]
    [TestCase("PUT", "/settings")]
    [TestCase("PUT", "/slides/3")]
    public async Task QaRoute_WhenNotSignedIn_ShouldAnswer401AndCallNothing(string method, string route)
    {
        var deck = await QaDeckAsync();
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.SendAsync(RequestTo(method, $"/api/decks/{deck}/qa{route}"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Moderation.CallsFor(deck).ShouldBeEmpty();
        Settings.CallsFor(deck).ShouldBeEmpty();
    }

    // One test per refusal code (Q6): each reaches the wire as 409 { "error": code }, and the refused action records nothing.
    [TestCase("not_live")]
    [TestCase("wrong_state")]
    public async Task Hide_WhenModerationRefuses_ShouldAnswer409WithTheCode(string code)
    {
        var deck = await QaDeckAsync();
        Moderation.RefuseWith(code);

        var response = await Client.PostAsync($"/api/decks/{deck}/qa/questions/3/hide", null);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example().GetProperty("refusals")[0], "qa-control.json refusals");
        body.GetProperty("error").GetString().ShouldBe(code);
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Answer_WhenNumberUnknown_ShouldAnswer404()
    {
        var deck = await QaDeckAsync();
        Moderation.FailWith(new NotFoundException("Question not found"));

        var response = await Client.PostAsync($"/api/decks/{deck}/qa/questions/99/answer", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Approve_WhenNumberIsZero_ShouldAnswer404AndCallNothing()
    {
        var deck = await QaDeckAsync();

        var response = await Client.PostAsync($"/api/decks/{deck}/qa/questions/0/approve", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    // Settings work live or not: the change goes to IQaSettings, and the route answers the view read after it.
    [Test]
    public async Task PutSettings_WhenNotLive_ShouldSucceed()
    {
        var deck = await QaDeckAsync();
        Moderation.Set(deck, NotLive());

        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/qa/settings", Example().GetProperty("settingsRequest"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example().GetProperty("notLive"), "qa-control.json notLive", ScreenShapes.QaControl);
        body.GetProperty("live").GetBoolean().ShouldBeFalse();
        body.GetProperty("seq").GetInt64().ShouldBe(198);
        Settings.CallsFor(deck).ShouldBe(["Update"]);
        (await Settings.GetAsync(deck)).ShouldBe(new QaSettings(QaQuestions.Closed, true, false, true, QaShowOn.QaSlide));
        Moderation.CallsFor(deck).ShouldBeEmpty();
    }

    // A field left out keeps its value.
    [Test]
    public async Task PutSettings_WhenOneFieldIsGiven_ShouldPassOnlyThatField()
    {
        var deck = await QaDeckAsync();

        var response = await Client.PutAsync($"/api/decks/{deck}/qa/settings", JsonBody("""{"review":true}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Settings.GetAsync(deck)).ShouldBe(QaSettings.Default with { Review = true });
    }

    [TestCase("""{"questions":"maybe"}""")]
    [TestCase("""{"showOn":1}""")]
    [TestCase("""{"review":"yes"}""")]
    [TestCase("""not json""")]
    public async Task PutSettings_WhenBodyBreaksItsShape_ShouldAnswer400AndChangeNothing(string json)
    {
        var deck = await QaDeckAsync();

        var response = await Client.PutAsync($"/api/decks/{deck}/qa/settings", JsonBody(json));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Settings.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task PutSlide_WhenCallerCanPresent_ShouldSetTheSwitchAndReturnTheView()
    {
        var deck = await QaDeckAsync();

        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/qa/slides/3", Example().GetProperty("slideRequest"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example().GetProperty("live"), "qa-control.json live", ScreenShapes.QaControl);
        Settings.CallsFor(deck).ShouldBe(["SetSlide 3"]);
        Settings.SwitchOf(deck, 3).ShouldBe(new QaSlideSwitch(false, true));
    }

    // An unknown slide is IQaSettings' NotFoundException: 404, and no view is read.
    [Test]
    public async Task PutSlide_WhenSlideUnknown_ShouldAnswer404()
    {
        var deck = await QaDeckAsync();
        Settings.FailWith(new NotFoundException("Slide not found"));

        var response = await Client.PutAsync($"/api/decks/{deck}/qa/slides/9", JsonBody("""{"reactions":false}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Settings.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task PutSlide_WhenSlideIsZero_ShouldAnswer404AndChangeNothing()
    {
        var deck = await QaDeckAsync();

        var response = await Client.PutAsync($"/api/decks/{deck}/qa/slides/0", JsonBody("""{"reactions":false}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Settings.CallsFor(deck).ShouldBeEmpty();
    }

    // Sam's deck, with FakeQuestionModeration answering it with the live sample of qa-control.json.
    private async Task<string> QaDeckAsync(string? sharing = null)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        if (sharing != null)
        {
            await SharingApi.PutOkAsync(Client, deck, sharing);
        }
        Moderation.Set(deck, Live());
        return deck;
    }

    // A request with a body both settings routes accept, for a PUT.
    private static HttpRequestMessage RequestTo(string method, string path)
        => new(new HttpMethod(method), path) { Content = method == "PUT" ? JsonBody("""{"reactions":false}""") : null };

    private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

    // The "live" sample of qa-control.json.
    private static QaControlView Live() => new(
        212,
        true,
        ROUND,
        QaSettings.Default with { Review = true },
        new QaSlideView(5, true, true),
        [new QaSlideView(3, false, true)],
        new QaCounts(3, 1, 1, 1),
        2,
        [
            new QaQuestion(1, "How do you split a big change?", "Priya", "12 min", QuestionState.Open, false, false, 14),
            new QaQuestion(2, "What is the smallest slice you have shipped?", null, "9 min", QuestionState.Open, false, true, 9),
            new QaQuestion(3, "Do feature flags count as shipping?", "Sam K", "8 min", QuestionState.Answered, false, false, 6),
            new QaQuestion(4, "Who reviews the slices?", null, "2 min", QuestionState.Waiting, false, false, 1),
            new QaQuestion(5, "Why is this talk so long?", null, "1 min", QuestionState.Open, true, false, 2),
            new QaQuestion(6, "How do you handle database migrations?", null, "now", QuestionState.Open, false, false, 3),
        ],
        new ReactionCounts(41, 12, 7, 3, 28));

    // The "notLive" sample of qa-control.json.
    private static QaControlView NotLive() => new(198, false, null, QaSettings.Default, null, [], new QaCounts(0, 0, 0, 0), null, [], ReactionCounts.Zero);

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static JsonElement Example()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "qa-control.json")));
        return document.RootElement.Clone();
    }
}
