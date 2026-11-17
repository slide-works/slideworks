using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The presenter's poll routes (V4) over FakePolls: who may call them (D20: a person who can present the deck; 404 when the
// deck can't be viewed, 403 when it can be viewed but not presented), that a refused call reaches no seam, and the bodies.
// The app, and so FakePolls, is shared by the fixture: each test makes its own deck, and the refusal is reset after each.
[TestFixture]
public class PollRoutesTests : ApiTestFixture
{
    private const int SLIDE = 4;
    private const string ROUND = "0b7c6a52-1f0e-4f43-9d3a-5a1c2e8b7d10";

    protected override Action<IServiceCollection>? ConfigureTestServices => FakePolls.AddTo;

    private FakePolls Polls => Factory.Services.GetRequiredService<FakePolls>();

    [TearDown]
    public void AcceptAgain() => Polls.Reset();

    [Test]
    public async Task Get_WhenCallerCanPresent_ShouldReturnTheControlView()
    {
        var deck = await PollDeckAsync();

        var response = await Client.GetAsync($"/api/decks/{deck}/polls/{SLIDE}");
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example("poll-control.json").GetProperty("closed"), "poll-control.json closed", ScreenShapes.PollControl);
        body.GetProperty("seq").GetInt64().ShouldBe(140);
        body.GetProperty("round").GetString().ShouldBe(ROUND);
        body.GetProperty("phase").GetString().ShouldBe("closed");
        body.GetProperty("settings").GetProperty("pick").GetString().ShouldBe("upTo");
        body.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([12, 25, 14, 6]);
        Polls.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenSlideIsNotAPoll_ShouldReturn404()
    {
        var deck = await PollDeckAsync();

        var response = await Client.GetAsync($"/api/decks/{deck}/polls/{SLIDE + 1}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenSlideIsZero_ShouldReturn404()
    {
        var deck = await PollDeckAsync();

        var response = await Client.GetAsync($"/api/decks/{deck}/polls/0");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenCallerCannotPresent_ShouldReturn403()
    {
        var deck = await PollDeckAsync(sharing: "link");
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.GetAsync($"/api/decks/{deck}/polls/{SLIDE}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Get_WhenCallerCannotView_ShouldReturn404()
    {
        var deck = await PollDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.GetAsync($"/api/decks/{deck}/polls/{SLIDE}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestCase("open", "Open")]
    [TestCase("close", "Close")]
    [TestCase("reveal", "Reveal")]
    [TestCase("answer", "ShowAnswer")]
    [TestCase("round", "NewRound")]
    public async Task Post_WhenCallerCanPresent_ShouldCallTheActionOnceAndReturnItsView(string route, string action)
    {
        var deck = await PollDeckAsync();

        var response = await Client.PostAsync($"/api/decks/{deck}/polls/{SLIDE}/{route}", null);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(body, Example("poll-control.json").GetProperty("closed"), "poll-control.json closed", ScreenShapes.PollControl);
        body.GetProperty("round").GetString().ShouldBe(ROUND);
        Polls.CallsFor(deck).ShouldBe([$"{action} {SLIDE}"]);
    }

    // V4, D20: someone who can view the deck but not present it is refused before the seam is called.
    [TestCase("open")]
    [TestCase("close")]
    [TestCase("reveal")]
    [TestCase("answer")]
    [TestCase("round")]
    public async Task Post_WhenCallerCannotPresent_ShouldReturn403AndCallNothing(string route)
    {
        var deck = await PollDeckAsync(sharing: "link");
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsync($"/api/decks/{deck}/polls/{SLIDE}/{route}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Polls.CallsFor(deck).ShouldBeEmpty();
    }

    [TestCase("open")]
    [TestCase("close")]
    [TestCase("reveal")]
    [TestCase("answer")]
    [TestCase("round")]
    public async Task Post_WhenCallerCannotView_ShouldReturn404AndCallNothing(string route)
    {
        var deck = await PollDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsync($"/api/decks/{deck}/polls/{SLIDE}/{route}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Polls.CallsFor(deck).ShouldBeEmpty();
    }

    // One test per refusal code (V4): each reaches the wire as 409 { "error": code }.
    [TestCase(PollRefusals.NOT_LIVE)]
    [TestCase(PollRefusals.WRONG_PHASE)]
    [TestCase(PollRefusals.ANOTHER_POLL_OPEN)]
    [TestCase(PollRefusals.ALREADY_VISIBLE)]
    [TestCase(PollRefusals.NO_CORRECT)]
    public async Task Close_WhenServiceRefuses_ShouldReturn409WithTheCode(string code)
    {
        var deck = await PollDeckAsync();
        Polls.RefuseWith(code);

        var response = await Client.PostAsync($"/api/decks/{deck}/polls/{SLIDE}/close", null);
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example("poll-control.json").GetProperty("refusals")[0], "poll-control.json refusals");
        body.GetProperty("error").GetString().ShouldBe(code);
        Polls.CallsFor(deck).ShouldBeEmpty();
    }

    // Sam's deck, with FakePolls answering its poll slide with the closed sample of poll-control.json.
    private async Task<string> PollDeckAsync(string? sharing = null)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        if (sharing != null)
        {
            await SharingApi.PutOkAsync(Client, deck, sharing);
        }
        Polls.Set(deck, SLIDE, Closed());
        return deck;
    }

    private static PollControlView Closed() => new(
        140,
        SLIDE,
        ROUND,
        1,
        PollPhase.Closed,
        new PollSettings(PollPick.UpTo, 2, PollResults.AfterAnswer, 2, PollOpens.OnArrival, PollPrivacy.Private),
        "How big was your last merge request?",
        ["Under 50 lines", "50 to 200 lines", "200 to 1,000 lines", "Over 1,000 lines"],
        38,
        41,
        [12, 25, 14, 6],
        true,
        false,
        true,
        [new PollRoundSummary(1, 38, 41)]);

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
