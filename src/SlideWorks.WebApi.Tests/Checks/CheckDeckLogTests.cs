using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Checks;

// D10: one Information line per check_deck call is the Measure; compare a deck's first and last call.
[TestFixture]
public class CheckDeckLogTests : ApiTestFixture
{
    private CapturingLoggerProvider _logs = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeStoredSlideChecks.Register(services);
        _logs = CapturingLoggerProvider.Replace<DeckChecks>(services);
    };

    [SetUp]
    public void ResetFakes()
    {
        Factory.Services.GetRequiredService<FakeRenderEngine>().Reset();
        Factory.Services.GetRequiredService<FakeStoredSlideChecks>().Reset();
    }

    [Test]
    public async Task CheckDeck_WhenCalled_ShouldLogOneLineWithCounts()
    {
        var engine = Factory.Services.GetRequiredService<FakeRenderEngine>();
        Factory.Services.GetRequiredService<FakeStoredSlideChecks>().FindingsFor(1, new SlideFinding(1, FindingRule.Fit, FindingSeverity.Error, FindingSource.Stored, "items", "Shorten it."));
        engine.FindingsFor(1, new SlideFinding(1, FindingRule.Overflow, FindingSeverity.Error, FindingSource.Measured, "text", "Cut it."));
        engine.FindingsFor(2, new SlideFinding(2, FindingRule.LowContrast, FindingSeverity.Warning, FindingSource.Measured, "text", "Darken it."), new SlideFinding(2, FindingRule.TextOnImage, FindingSeverity.Note, FindingSource.Measured, "text", "Look."));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Logged");
        await DeckSeed.AddQuoteAsync(agent, deck, "One.");
        await DeckSeed.AddQuoteAsync(agent, deck, "Two.");

        var (isError, json) = await agent.CallToolAsync("check_deck", new { deck });

        isError.ShouldBeFalse(json.GetRawText());
        var lines = _logs.Lines.Where(l => l.Contains($"Checked deck {deck}:")).ToList();
        lines.Count.ShouldBe(1);
        lines[0].ShouldContain($"Checked deck {deck}: 2 slides, 2 errors, 1 warnings, 1 notes, measured True; by rule ");
        lines[0].ShouldContain("overflow=1");
        lines[0].ShouldContain("fit=1");
        engine.Calls.ShouldNotBeEmpty();
        foreach (var call in engine.Calls)
        {
            lines[0].ShouldNotContain(call);
        }
    }

    [Test]
    public async Task CheckDeck_WhenRenderingStops_ShouldLogMeasuredFalse()
    {
        Factory.Services.GetRequiredService<FakeRenderEngine>().ThrowUnavailable();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Stopped");
        await DeckSeed.AddQuoteAsync(agent, deck, "One.");

        await agent.CallToolAsync("check_deck", new { deck });

        _logs.Lines.Single(l => l.Contains($"Checked deck {deck}:")).ShouldContain("measured False");
    }
}
