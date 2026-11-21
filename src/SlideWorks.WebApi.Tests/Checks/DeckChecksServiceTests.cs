using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Checks;

// DeckChecks and the engine's data-checks parsing, below the MCP tool.
[TestFixture]
public class DeckChecksServiceTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeStoredSlideChecks.Register;

    [Test]
    public async Task CheckAsync_WhenMeasureIsFalse_ShouldGiveTheStoredChecksOnly()
    {
        var engine = Factory.Services.GetRequiredService<FakeRenderEngine>();
        var stored = Factory.Services.GetRequiredService<FakeStoredSlideChecks>();
        engine.Reset();
        stored.Reset();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Checks");
        await DeckSeed.AddQuoteAsync(agent, deck, "Quote.");
        stored.FindingsFor(1, new SlideFinding(1, FindingRule.Fit, FindingSeverity.Error, FindingSource.Stored, "text", "Change it."));

        // The tool always measures, so the service is reached directly; the access stub ignores the actor.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var result = await new DeckChecks(db, null!, new AllowEdit(), engine, stored, NullLogger<DeckChecks>.Instance).CheckAsync(deck, null, false);

        result.Measured.ShouldBeFalse();
        result.Error.ShouldBeNull();
        result.Findings.Count.ShouldBe(1);
        engine.Calls.ShouldNotContain($"slide:{deck}:1");
    }

    [Test]
    public void RenderingFailedException_ShouldKeepItsReasonApartFromThePrefix()
    {
        var error = new RenderingFailedException("no slide frame");

        error.Reason.ShouldBe("no slide frame");
        error.Message.ShouldBe("Rendering failed: no slide frame");
    }

    [TestCase("""[null]""")]
    [TestCase("""[{"slide":1,"rule":"overflow","severity":"error","source":"measured","where":null,"message":"m"}]""")]
    [TestCase("""[{"slide":1,"rule":"overflow","severity":"error","source":"measured","where":"w","message":null}]""")]
    [TestCase("""null""")]
    [TestCase("""not json""")]
    public void ParseChecks_WhenTheTextIsUnreadable_ShouldFailTheRender(string json)
    {
        var error = Should.Throw<RenderingFailedException>(() => PlaywrightRenderEngine.ParseChecks(json));

        error.Reason.ShouldBe("checks unreadable");
    }

    [Test]
    public void ParseChecks_WhenTheAttributeIsMissing_ShouldGiveNone()
    {
        PlaywrightRenderEngine.ParseChecks(null).ShouldBeEmpty();
    }

    private sealed class AllowEdit : IDeckAccess
    {
        public Task<bool> CanViewAsync(string deckId, ICurrentActor actor) => Task.FromResult(true);

        public Task<bool> CanEditAsync(string deckId, ICurrentActor actor) => Task.FromResult(true);

        public Task<bool> CanPresentAsync(string deckId, ICurrentActor actor) => Task.FromResult(true);
    }
}
