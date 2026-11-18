using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// update_qa over the real IQaSettings: what lands in the database and in the deck's activity (Q10, Q6), which QaMcpTests,
// over FakeQaSettings, can't see. Each test seeds a live deck of its own.
[TestFixture]
[CancelAfter(60000)]
public class QaMcpActivityTests : QaTestFixture
{
    [Test]
    public async Task UpdateQa_WhenAnAgentChangesASetting_ShouldStoreItAndLogItUnderTheToolName()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, review = true, questions = "closed" });

        (await QaRowSeedSettingsAsync(qa.DeckId)).ShouldBe(QaSettings.Default with { Review = true, Questions = QaQuestions.Closed });
        var entry = (await DeckSeed.ActivityAsync(Client, qa.DeckId))[0];
        entry.GetProperty("tool").GetString().ShouldBe("update_qa");
        entry.GetProperty("summary").GetString().ShouldBe("closed questions, turned review on");
    }

    [Test]
    public async Task UpdateQa_WhenTheChangeAltersNothing_ShouldLogNothing()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var entries = (await DeckSeed.ActivityAsync(Client, qa.DeckId)).Count;

        var result = await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, review = false });

        result.GetProperty("settings").GetProperty("review").GetBoolean().ShouldBeFalse();
        (await DeckSeed.ActivityAsync(Client, qa.DeckId)).Count.ShouldBe(entries);
    }

    [Test]
    public async Task UpdateQa_WhenASlideSwitchIsSetForSomethingTheDeckHasOff_ShouldStoreItAndLogIt()
    {
        var qa = await SeedQaAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, reactions = false });

        await SlideCalls.OkAsync(agent, "update_qa", new { deck = qa.DeckId, slide = qa.QuoteSlide, reactions = false });

        var entry = (await DeckSeed.ActivityAsync(Client, qa.DeckId))[0];
        entry.GetProperty("tool").GetString().ShouldBe("update_qa");
        entry.GetProperty("summary").GetString().ShouldBe($"turned reactions off on slide {qa.QuoteSlide}");
    }

    private async Task<QaSettings> QaRowSeedSettingsAsync(string deckId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<QaRows>().SettingsAsync(deckId);
    }
}
