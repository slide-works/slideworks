using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// A guest's reaction tap (Q7) over FakeReactions: POST /api/join/{code}/reactions, each ReactionResult on the wire, the body
// limit, the sw_guest cookie and the join-code miss budget (GuestQa). The code is resolved through FakeAudienceSessions. The
// app, and so every fake, is shared by the fixture: each test uses a code of its own and a new window of the miss budget.
[TestFixture]
public class JoinReactionsRoutesTests : PollTestFixture
{
    private string _code = null!;

    private FakeReactions Reactions => Factory.Services.GetRequiredService<FakeReactions>();

    private FakeAudienceSessions Sessions => Factory.Services.GetRequiredService<FakeAudienceSessions>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakeAudienceSessions.AddTo(services);
        FakeReactions.AddTo(services);
    }

    [SetUp]
    public void NewCodeAndWindow()
    {
        _code = NewCode();
        Sessions.Set(_code, $"deck-{_code}", Guid.NewGuid().ToString(), AudiencePhase.Live);
        Clock.Advance(TimeSpan.FromMinutes(2));
    }

    [Test]
    public async Task React_WhenCounted_ShouldAnswer204()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.ReactAsync(_code, new ReactionRequest("clap"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        Reactions.TapsFor(_code).ShouldBe([(guest.GuestId, "clap")]);
    }

    // The emoji goes to the seam as the body's string, so an unknown or missing one is the seam's Invalid.
    [Test]
    public async Task React_WhenEmojiUnknown_ShouldAnswer400()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Reactions.SetTap(_code, new ReactionResult.Invalid(GuestQaErrors.UNKNOWN_EMOJI));

        var response = await guest.ReactAsync(_code, new ReactionRequest("rocket"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonShape.AssertMatches(body, Example().GetProperty("reaction").GetProperty("invalid"), "guest-questions.json reaction.invalid");
        body.GetProperty("error").GetString().ShouldBe("emoji: unknown");
        Reactions.TapsFor(_code).ShouldBe([(guest.GuestId, "rocket")]);
    }

    [TestCase("""{}""")]
    [TestCase("""{"emoji":null}""")]
    public async Task React_WhenEmojiIsLeftOut_ShouldPassNullToTheSeam(string json)
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Reactions.SetTap(_code, new ReactionResult.Invalid(GuestQaErrors.UNKNOWN_EMOJI));

        var response = await guest.ReactAsync(_code, JsonSerializer.Deserialize<JsonElement>(json));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Reactions.TapsFor(_code).ShouldBe([(guest.GuestId, null)]);
    }

    [Test]
    public async Task React_WhenReactionsOff_ShouldAnswer409ReactionsOff()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Reactions.SetTap(_code, new ReactionResult.Refused(GuestQaErrors.REACTIONS_OFF));

        var response = await guest.ReactAsync(_code, new ReactionRequest("heart"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonShape.AssertMatches(body, Example().GetProperty("reaction").GetProperty("reactionsOff"), "guest-questions.json reaction.reactionsOff");
        body.GetProperty("error").GetString().ShouldBe("reactions_off");
    }

    [Test]
    public async Task React_WhenLimited_ShouldAnswer429TooManyReactions()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Reactions.SetTap(_code, new ReactionResult.Limited());

        var response = await guest.ReactAsync(_code, new ReactionRequest("heart"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        JsonShape.AssertMatches(body, Example().GetProperty("reaction").GetProperty("tooManyReactions"), "guest-questions.json reaction.tooManyReactions");
        body.GetProperty("error").GetString().ShouldBe("too_many_reactions");
    }

    [Test]
    public async Task React_WhenSeamAnswersNoSession_ShouldAnswer404NoSession()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        Reactions.SetTap(_code, new ReactionResult.NoSession());

        var response = await guest.ReactAsync(_code, new ReactionRequest("heart"));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("error").GetString().ShouldBe(GuestViews.NO_SESSION);
    }

    [Test]
    public async Task React_WhenNoCookie_ShouldAnswer400NoGuestAndTapNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory, noCookie: true);

        var response = await guest.ReactAsync(_code, new ReactionRequest("heart"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BodyAsync(response)).GetProperty("error").GetString().ShouldBe(GuestPolls.NO_GUEST);
        Reactions.TapsFor(_code).ShouldBeEmpty();
    }

    // An unknown code is a miss, and the seam is never called for it: after the budget's worth, a live code is 429 too.
    [Test]
    public async Task React_WhenCodeUnknown_ShouldCountAMissAndTapNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var unknown = NewCode();
        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            (await guest.ReactAsync(unknown, new ReactionRequest("heart"))).StatusCode.ShouldBe(HttpStatusCode.NotFound, $"miss {i + 1}");
        }

        var refused = await guest.ReactAsync(_code, new ReactionRequest("heart"));

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await refused.Content.ReadAsStringAsync()).ShouldBe("""{"error":"too_many_attempts"}""");
        Reactions.TapsFor(unknown).ShouldBeEmpty();
        Reactions.TapsFor(_code).ShouldBeEmpty();
    }

    [Test]
    public async Task React_WhenBodyIsOverTheLimit_ShouldAnswer413AndTapNothing()
    {
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.ReactAsync(_code, new ReactionRequest(new string('x', JoinReactionsController.MAX_BYTES)));
        var body = await BodyAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        body.GetProperty("error").GetString().ShouldBe(BodyLimitAttribute.TooLarge(JoinReactionsController.MAX_BYTES));
        Reactions.TapsFor(_code).ShouldBeEmpty();
    }

    [Test]
    public async Task React_WhenBodyIsNotJson_ShouldAnswer400AndTapNothing()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/join/{_code}/reactions")
        {
            Content = new StringContent("emoji=clap", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Origin", TestAppFactory.PUBLIC_URL);
        request.Headers.Add("Cookie", $"{GuestCookie.NAME}={GuestCookie.NewId()}");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Reactions.TapsFor(_code).ShouldBeEmpty();
    }

    private static string NewCode() => Random.Shared.Next(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static JsonElement Example()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "guest-questions.json")));
        return document.RootElement.Clone();
    }
}
