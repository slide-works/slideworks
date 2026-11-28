using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Translations;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K4: TranslationsSummary.ForDeckAsync counts lines through TranslationCoverage.
[TestFixture]
public class TranslationsSummaryTests : ApiTestFixture
{
    [Test]
    public async Task Summary_WhenOff_ShouldBeNull()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await using var scope = Factory.Services.CreateAsyncScope();

        var summary = await scope.ServiceProvider.GetRequiredService<TranslationsSummary>().ForDeckAsync(deck);

        summary.ShouldBeNull();
    }

    [Test]
    public async Task Summary_WhenOneLineStale_ShouldCountIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Hello");
        var seed = new TranslationRowSeed(Factory.Services);
        await seed.SettingsAsync(deck, true, "en", "th");
        await seed.LinesAsync(deck, 1, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/quote"] = ("สวัสดี", "Hi") },
        });
        await using var scope = Factory.Services.CreateAsyncScope();

        var summary = await scope.ServiceProvider.GetRequiredService<TranslationsSummary>().ForDeckAsync(deck);

        summary.ShouldNotBeNull();
        summary.Enabled.ShouldBeTrue();
        summary.Primary.ShouldBe("en");
        var thai = summary.Languages.ShouldHaveSingleItem();
        thai.Code.ShouldBe("th");
        thai.Stale.ShouldBe(1);
        thai.Current.ShouldBe(0);
        thai.Lines.ShouldBe(thai.Stale + thai.Missing);
    }
}
