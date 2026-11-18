using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// Privacy on the guest's question routes (Q4): another guest's hidden or waiting question, its text, its name and its votes
// are in none of a guest's raw bodies (the GET, a stale vote's 409, the ask's 201), and no body carries any guest's id. The
// question is given a vote count no visible question has, so its votes can be searched for. Rows come from QaRowSeed on
// QaSeed's live deck; the room is FakeAudienceRoom's. Every test seeds a deck of its own and starts a new minute.
[TestFixture]
[CancelAfter(60000)]
public class QuestionBodyPrivacyTests : QaTestFixture
{
    private const string VISIBLE = "How do you split a big change?";

    private const string SECRET = "Is the reorg happening next week?";

    private const string SECRET_NAME = "Avery Quinn";

    private const int SECRET_VOTES = 7;

    private FakeAudienceRoom Rooms => Factory.Services.GetRequiredService<FakeAudienceRoom>();

    protected override void ConfigureQaServices(IServiceCollection services) => FakeAudienceRoom.AddTo(services);

    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task Bodies_WhenAnotherGuestsQuestionIsHidden_ShouldNotContainItsTextNameOrVotes()
    {
        var bodies = await BodiesAsync(QuestionState.Open, hidden: true);

        bodies.Asker.ShouldContain(SECRET);
        bodies.Asker.ShouldContain($"\"votes\":{SECRET_VOTES}");
        bodies.Other[1].Body.ShouldContain(GuestQaErrors.STALE);
        foreach (var (route, body) in bodies.Other)
        {
            body.ShouldNotContain(SECRET, Case.Insensitive, route);
            body.ShouldNotContain(SECRET_NAME, Case.Insensitive, route);
            body.ShouldNotContain($"\"votes\":{SECRET_VOTES}", Case.Sensitive, route);
            body.ShouldNotContain("hidden", Case.Insensitive, route);
            body.ShouldNotContain(bodies.AskerId, Case.Insensitive, route);
        }
    }

    [Test]
    public async Task Bodies_WhenAnotherGuestsQuestionIsWaiting_ShouldNotContainItsTextNameOrVotes()
    {
        var bodies = await BodiesAsync(QuestionState.Waiting, hidden: false);

        bodies.Asker.ShouldContain(SECRET);
        bodies.Asker.ShouldContain($"\"votes\":{SECRET_VOTES}");
        bodies.Other[1].Body.ShouldContain(GuestQaErrors.STALE);
        foreach (var (route, body) in bodies.Other)
        {
            body.ShouldNotContain(SECRET, Case.Insensitive, route);
            body.ShouldNotContain(SECRET_NAME, Case.Insensitive, route);
            body.ShouldNotContain($"\"votes\":{SECRET_VOTES}", Case.Sensitive, route);
            body.ShouldNotContain("waiting", Case.Insensitive, route);
            body.ShouldNotContain(bodies.AskerId, Case.Insensitive, route);
        }
    }

    // A live deck with one visible question and the asker's secret one in this state, upvoted by SECRET_VOTES guests while
    // it was open; then the asker's GET, and another guest's raw GET, stale vote and ask bodies, by route.
    private async Task<(string Asker, string AskerId, IReadOnlyList<(string Route, string Body)> Other)> BodiesAsync(QuestionState state, bool hidden)
    {
        var qa = await SeedQaAsync();
        Rooms.Set(qa.DeckId, new QaRoom(qa.RoundId.ToString(), QaRoomQuestions.Open, true, true, 3));
        await using var asker = await JoinAsync(qa);
        await using var other = await JoinAsync(qa);
        await QaRowSeed.QuestionAsync(qa.RoundId, VISIBLE);
        await QaRowSeed.QuestionAsync(qa.RoundId, SECRET, state, hidden, guestId: asker.GuestId, name: SECRET_NAME);
        await QaRowSeed.VoteAsync(qa.RoundId, asker.GuestId, 2);
        for (var voter = 1; voter < SECRET_VOTES; voter++)
        {
            await QaRowSeed.VoteAsync(qa.RoundId, GuestCookie.NewId(), 2);
        }

        var askerView = await (await asker.GetQuestionsAsync(qa.Code)).Content.ReadAsStringAsync();
        var get = await (await other.GetQuestionsAsync(qa.Code)).Content.ReadAsStringAsync();
        var stale = await (await other.VoteAsync(qa.Code, new VoteRequest(qa.RoundId.ToString(), [1], 5))).Content.ReadAsStringAsync();
        var ask = await (await other.AskAsync(qa.Code, new AskRequest("What about tests?", null))).Content.ReadAsStringAsync();
        return (askerView, asker.GuestId, [("GET", get), ("PUT votes 409", stale), ("POST", ask)]);
    }

    private async Task<GuestClient> JoinAsync(SeededQa qa)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", qa.Code);
        return guest;
    }
}
