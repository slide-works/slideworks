using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Render.Print;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// IPrintQaResults over rows written by QaRowSeed: the newest Q&A round of the session, counted questions only (open or
// answered), the top five by votes with ties to the lower number, and the print route carrying them.
[TestFixture]
[CancelAfter(60000)]
public class PrintQaResultsTests : QaTestFixture
{
    [Test]
    public async Task ReadAsync_WhenTheLastTalkHadQuestions_ShouldReturnTheTopFiveByVotes()
    {
        var qa = await SeedQaAsync();
        for (var number = 1; number <= 7; number++)
        {
            await QaRowSeed.QuestionAsync(qa.RoundId, $"Question {number}");
        }
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-a", 7, 6, 5, 4, 3, 2);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-b", 7, 6, 5, 4, 3);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-c", 7, 6, 5, 4);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-d", 7, 6, 5);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-e", 7, 6);

        var result = await ReadAsync(qa.DeckId, qa.SessionId);

        result.ShouldNotBeNull();
        result.Phase.ShouldBe("open");
        result.Total.ShouldBe(7);
        result.Top.Select(q => (q.Number, q.Text, q.Votes)).ToList().ShouldBe(
            [(6, "Question 6", 5), (7, "Question 7", 5), (5, "Question 5", 4), (4, "Question 4", 3), (3, "Question 3", 2)]);
    }

    [Test]
    public async Task ReadAsync_WhenVotesTie_ShouldPutTheOlderQuestionFirst()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "First");
        await QaRowSeed.QuestionAsync(qa.RoundId, "Second");
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-a", 2);
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-b", 1);

        var result = await ReadAsync(qa.DeckId, qa.SessionId);

        result.ShouldNotBeNull();
        result.Top.Select(q => q.Number).ToList().ShouldBe([1, 2]);
    }

    [Test]
    public async Task ReadAsync_WhenAQuestionWasAnswered_ShouldStillList()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Answered already", QuestionState.Answered);

        var result = await ReadAsync(qa.DeckId, qa.SessionId);

        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Top.Select(q => q.Text).ToList().ShouldBe(["Answered already"]);
    }

    [Test]
    public async Task ReadAsync_WhenAQuestionWasHidden_ShouldLeaveItOutOfTopAndTotal()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Hidden one", hidden: true);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Shown one");

        var result = await ReadAsync(qa.DeckId, qa.SessionId);

        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Top.Select(q => q.Text).ToList().ShouldBe(["Shown one"]);
    }

    [Test]
    public async Task ReadAsync_WhenAQuestionIsWaiting_ShouldLeaveItOutOfTopAndTotal()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Waiting one", QuestionState.Waiting);
        await QaRowSeed.QuestionAsync(qa.RoundId, "Open one");

        var result = await ReadAsync(qa.DeckId, qa.SessionId);

        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Top.Select(q => q.Text).ToList().ShouldBe(["Open one"]);
    }

    [Test]
    public async Task ReadAsync_WhenTheRoundWasClosed_ShouldSayClosed()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Asked");
        await Rows.CloseAsync(qa.RoundId, joinedAtClose: 1);

        var result = await ReadAsync(qa.DeckId, qa.SessionId);

        result.ShouldNotBeNull();
        result.Phase.ShouldBe("closed");
        result.Total.ShouldBe(1);
    }

    [Test]
    public async Task ReadAsync_WhenTheSessionHadNoQaRound_ShouldBeNull()
    {
        var qa = await SeedQaAsync();

        (await ReadAsync(qa.DeckId, Guid.NewGuid())).ShouldBeNull();
    }

    [Test]
    public async Task GetPrint_WhenTheLastTalkHadQuestions_ShouldCarryThem()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "Why so fast?", name: "Alex");
        await QaRowSeed.VoteAsync(qa.RoundId, "guest-a", 1);
        using var browser = ClientFor(TestUser.Anonymous);
        browser.DefaultRequestHeaders.Add("X-Render-Token", Factory.Services.GetRequiredService<IRenderTokens>().Issue(qa.DeckId, RenderPurpose.Print));

        var response = await browser.GetAsync($"/api/render/{qa.DeckId}/print");

        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        var printed = view.GetProperty("qa");
        printed.GetProperty("total").GetInt32().ShouldBe(1);
        printed.GetProperty("top")[0].GetProperty("text").GetString().ShouldBe("Why so fast?");
        printed.GetProperty("top")[0].GetProperty("votes").GetInt32().ShouldBe(1);
        printed.GetRawText().ShouldNotContain("Alex");
        printed.GetRawText().ShouldNotContain("guest");
    }

    private async Task<PrintQa?> ReadAsync(string deckId, Guid sessionId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPrintQaResults>().ReadAsync(deckId, sessionId, CancellationToken.None);
    }
}
