using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Render.Print;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// IPrintPollResults on the real rows: the newest opened round of each live poll slide in the session, with the counts the
// presenter's view holds. Rounds and answers come from PollRowSeed.
[TestFixture]
[CancelAfter(60000)]
public class PrintPollResultsTests : PollTestFixture
{
    [SetUp]
    public void NewMinute() => Clock.Advance(TimeSpan.FromMinutes(2));

    [Test]
    public async Task ReadAsync_WhenAClosedRoundHasAnswers_ShouldReturnItsCounts()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 0);
        await Rows.AnswerAsync(round.Id, "guest-b", 0);
        await Rows.AnswerAsync(round.Id, "guest-c", 2);
        await Rows.CloseAsync(round.Id, joinedAtClose: 3);

        var results = await ReadAsync(poll.DeckId, poll.SessionId);

        var result = results.ShouldHaveSingleItem();
        result.Slide.ShouldBe(poll.Slide);
        result.Phase.ShouldBe(PollPhase.Closed);
        result.Number.ShouldBe(1);
        result.Answered.ShouldBe(3);
        result.Counts.ShouldBe([2, 0, 1]);
        result.Pick.ShouldBe(PollPick.One);
        result.Max.ShouldBeNull();
        result.Answers.ShouldBe(PollAnswersForm.Choice);
    }

    [Test]
    public async Task ReadAsync_WhenTheSlideRanTwoRounds_ShouldReturnTheNewest()
    {
        var poll = await SeedPollAsync();
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(first.Id, "guest-a", 0);
        await Rows.CloseAsync(first.Id, joinedAtClose: 1);
        var second = await Rows.RoundAsync(poll.SlideId, poll.SessionId, number: 2);
        await Rows.AnswerAsync(second.Id, "guest-a", 1);
        await Rows.AnswerAsync(second.Id, "guest-b", 1);

        var result = (await ReadAsync(poll.DeckId, poll.SessionId)).ShouldHaveSingleItem();

        result.Number.ShouldBe(2);
        result.Phase.ShouldBe(PollPhase.Open);
        result.Answered.ShouldBe(2);
        result.Counts.ShouldBe([0, 2, 0]);
    }

    [Test]
    public async Task ReadAsync_WhenTheRoundIsInAnOlderSession_ShouldLeaveTheSlideOut()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 0);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();
        Clock.Advance(TimeSpan.FromMinutes(30));
        (await Client.PostAsync($"/api/decks/{poll.DeckId}/live", null)).EnsureSuccessStatusCode();
        var newest = await NewestSessionAsync(poll.DeckId);

        var results = await ReadAsync(poll.DeckId, newest);

        newest.ShouldNotBe(poll.SessionId);
        results.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenTheOnlyRoundsAreADraftAndADiscardedDraft_ShouldLeaveTheSlideOut()
    {
        var poll = await SeedPollAsync();
        var discarded = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);
        await Rows.CloseAsync(discarded.Id, joinedAtClose: 0);
        var draft = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft, number: 2);
        await Rows.AnswerAsync(draft.Id, "guest-a", 0);

        var results = await ReadAsync(poll.DeckId, poll.SessionId);

        results.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenTheRoundIsOpenOnAnUpToPoll_ShouldCarryLiveCountsAndTheMax()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, new PollSettings(PollPick.UpTo, 2, PollResults.Live, null, PollOpens.OnArrival, PollPrivacy.Private));
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 0, 1);
        await Rows.AnswerAsync(round.Id, "guest-b", 2);

        var result = (await ReadAsync(poll.DeckId, poll.SessionId)).ShouldHaveSingleItem();

        result.Phase.ShouldBe(PollPhase.Open);
        result.Answered.ShouldBe(2);
        result.Counts.ShouldBe([1, 1, 1]);
        result.Pick.ShouldBe(PollPick.UpTo);
        result.Max.ShouldBe(2);
    }

    [Test]
    public async Task ReadAsync_WhenTheAnswerWasNotShown_ShouldHaveNoCorrectOption()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Correct = 2 });
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 1);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1, answerShown: false);

        var result = (await ReadAsync(poll.DeckId, poll.SessionId)).ShouldHaveSingleItem();

        result.AnswerShown.ShouldBeFalse();
        result.Correct.ShouldBeNull();
    }

    [Test]
    public async Task ReadAsync_WhenTheAnswerWasShown_ShouldCarryTheCorrectOption()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Correct = 2 });
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 1);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1, answerShown: true);

        var result = (await ReadAsync(poll.DeckId, poll.SessionId)).ShouldHaveSingleItem();

        result.AnswerShown.ShouldBeTrue();
        result.Correct.ShouldBe(2);
    }

    [Test]
    public async Task ReadAsync_WhenOptionsWereRewordedAfterClosing_ShouldLeaveTheSlideOut()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 0);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        await SlideCalls.OkAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = "A fourth" });

        var results = await ReadAsync(poll.DeckId, poll.SessionId);

        results.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenTheFormIsChoice_ShouldHaveEmptyRepliesAndWords()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 0);

        var result = (await ReadAsync(poll.DeckId, poll.SessionId)).ShouldHaveSingleItem();

        result.Replies.ShouldBeEmpty();
        result.Words.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenTheSlideWasDeleted_ShouldLeaveItOut()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 0);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        await SlideCalls.OkAsync(agent, "delete_slide", new { deck = poll.DeckId, slide = poll.Slide });

        var results = await ReadAsync(poll.DeckId, poll.SessionId);

        results.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPrint_WhenThePollRanInTheLastTalk_ShouldCarryItsResults()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-a", 1);
        await Rows.AnswerAsync(round.Id, "guest-b", 1);
        await Rows.CloseAsync(round.Id, joinedAtClose: 2);
        var token = Factory.Services.GetRequiredService<IRenderTokens>().Issue(poll.DeckId, RenderPurpose.Print);
        using var browser = ClientFor(TestUser.Anonymous);
        browser.DefaultRequestHeaders.Add("X-Render-Token", token);

        var response = await browser.GetAsync($"/api/render/{poll.DeckId}/print");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, "deck-print.json", ScreenShapes.DeckPrint);
        var printed = view.GetProperty("polls").EnumerateArray().ShouldHaveSingleItem();
        printed.GetProperty("slide").GetInt32().ShouldBe(poll.Slide);
        printed.GetProperty("phase").GetString().ShouldBe("closed");
        printed.GetProperty("answered").GetInt32().ShouldBe(2);
        printed.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([0, 2, 0]);
        printed.GetProperty("replies").GetArrayLength().ShouldBe(0);
        printed.GetProperty("words").GetArrayLength().ShouldBe(0);
    }

    private async Task<IReadOnlyList<PrintPoll>> ReadAsync(string deckId, Guid sessionId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPrintPollResults>().ReadAsync(deckId, sessionId, CancellationToken.None);
    }

    private async Task<Guid> NewestSessionAsync(string deckId)
    {
        using var scope = Factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<PrintSessions>().NewestAsync(deckId, CancellationToken.None))!.Value;
    }
}
