using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Checks;

// SW-3 K6: check_deck gives the stored findings and (unless measure is false) the findings each slide's render measures,
// sorted by slide, severity, rule. The deck's editors only.
[TestFixture]
public class CheckDeckToolTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeStoredSlideChecks.Register;

    private FakeRenderEngine Engine => Factory.Services.GetRequiredService<FakeRenderEngine>();

    private FakeStoredSlideChecks Stored => Factory.Services.GetRequiredService<FakeStoredSlideChecks>();

    [SetUp]
    public void ResetFakes()
    {
        Engine.Reset();
        Stored.Reset();
    }

    [Test]
    public async Task CheckDeck_WhenBothSourcesReportFindings_ShouldReturnThemSorted()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Stored.FindingsFor(2, Finding(2, FindingRule.GenericAlt, FindingSource.Stored));
        Engine.FindingsFor(2, Finding(2, FindingRule.Overflow, FindingSource.Measured), Finding(2, FindingRule.TextOnImage, FindingSource.Measured));
        Engine.FindingsFor(1, Finding(1, FindingRule.SmallText, FindingSource.Measured));

        var (isError, json) = await agent.CallToolAsync("check_deck", new { deck });

        isError.ShouldBeFalse(json.GetRawText());
        json.GetProperty("measured").GetBoolean().ShouldBeTrue();
        json.GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("slides").EnumerateArray().Select(n => n.GetInt32()).ShouldBe([1, 2, 3]);
        json.GetProperty("findings").EnumerateArray().Select(f => $"{f.GetProperty("slide").GetInt32()} {f.GetProperty("rule").GetString()}")
            .ShouldBe(["1 smallText", "2 overflow", "2 genericAlt", "2 textOnImage"]);
    }

    [Test]
    public async Task CheckDeck_WhenTheCallerCannotEdit_ShouldRefuseAsNotFound()
    {
        await using var owner = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await ThreeSlideDeckAsync(owner);
        await using var other = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await other.CallToolAsync("check_deck", new { deck });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("Deck not found");
        Engine.Calls.ShouldNotContain(c => c.Contains(deck));
    }

    [Test]
    public async Task CheckDeck_WhenASlideNumberIsOutOfRange_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        var (isError, json) = await agent.CallToolAsync("check_deck", new { deck, slides = new[] { 2, 40 } });

        isError.ShouldBeTrue();
        json.GetString().ShouldBe("slides: no slide 40");
    }

    [Test]
    public async Task CheckDeck_WhenTheRendererBecomesUnavailableAtSlide3_ShouldKeepStoredAndEarlierMeasuredFindings()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent, 4);
        Stored.FindingsFor(4, Finding(4, FindingRule.Fit, FindingSource.Stored));
        Engine.FindingsFor(1, Finding(1, FindingRule.Overflow, FindingSource.Measured));
        Engine.FailSlide(3, new RendererUnavailableException("Card rendering isn't available"));

        var (isError, json) = await agent.CallToolAsync("check_deck", new { deck });

        isError.ShouldBeFalse(json.GetRawText());
        json.GetProperty("measured").GetBoolean().ShouldBeFalse();
        json.GetProperty("error").GetString().ShouldBe("Rendering stopped at slide 3, so slides 3 and 4 have only the stored checks.");
        json.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("slide").GetInt32()).ShouldBe([1, 4]);
        Engine.Calls.ShouldNotContain($"slide:{deck}:4");
    }

    [Test]
    public async Task CheckDeck_WhenTheRendererStopsOnAListedSlide_ShouldNameTheUnmeasuredSlidesInOrder()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent, 6);
        Engine.FailSlide(5, new RendererUnavailableException("Card rendering isn't available"));

        var (_, json) = await agent.CallToolAsync("check_deck", new { deck, slides = new[] { 5, 2 } });

        json.GetProperty("error").GetString().ShouldBe("Rendering stopped at slide 5, so slides 5 and 2 have only the stored checks.");
        Engine.Calls.ShouldNotContain($"slide:{deck}:2");
    }

    [Test]
    public async Task CheckDeck_WhenTheRenderTimesOut_ShouldStopMeasuring()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Engine.FindingsFor(1, Finding(1, FindingRule.Overflow, FindingSource.Measured));
        Engine.FailSlide(2, new TimeoutException("Rendering timed out"));

        var (_, json) = await agent.CallToolAsync("check_deck", new { deck });

        json.GetProperty("measured").GetBoolean().ShouldBeFalse();
        json.GetProperty("error").GetString().ShouldBe("Rendering stopped at slide 2, so slides 2 and 3 have only the stored checks.");
        json.GetProperty("findings").GetArrayLength().ShouldBe(1);
        Engine.Calls.ShouldNotContain($"slide:{deck}:3");
    }

    [Test]
    public async Task CheckDeck_WhenOneSlideFailsToRender_ShouldReportRenderFailedAndMeasureTheRest()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Engine.FailSlide(2, new RenderingFailedException("image not loaded"));
        Engine.FindingsFor(3, Finding(3, FindingRule.SmallText, FindingSource.Measured));

        var (_, json) = await agent.CallToolAsync("check_deck", new { deck });

        json.GetProperty("measured").GetBoolean().ShouldBeTrue();
        json.GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
        var findings = json.GetProperty("findings").EnumerateArray().ToList();
        findings.Select(f => f.GetProperty("rule").GetString()).ShouldBe(["renderFailed", "smallText"]);
        findings[0].GetProperty("slide").GetInt32().ShouldBe(2);
        findings[0].GetProperty("severity").GetString().ShouldBe("error");
        findings[0].GetProperty("where").GetString().ShouldBe("slide");
        findings[0].GetProperty("message").GetString().ShouldBe("This slide didn't render: image not loaded. Fix what the reason names, then check again.");
    }

    [Test]
    public async Task CheckDeck_OnTheRawWire_ShouldReturnAnObjectWithCamelCaseEnums()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Engine.FindingsFor(1, Finding(1, FindingRule.ImageLowResolution, FindingSource.Measured));

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "check_deck", $$"""{"deck":"{{deck}}"}""");

        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["deck", "slides", "languages", "findings", "measured", "error"]);
        var finding = structured.GetProperty("findings")[0];
        finding.GetProperty("rule").GetString().ShouldBe("imageLowResolution");
        finding.GetProperty("severity").GetString().ShouldBe("warning");
        finding.GetProperty("source").GetString().ShouldBe("measured");
        finding.GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task CheckDeck_WhenLanguageIsGivenOnTheRawWire_ShouldAnswerAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);

        // The deck's primary language (en by default), which check_deck always accepts.
        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "check_deck", $$"""{"deck":"{{deck}}","language":"en"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["deck", "slides", "languages", "findings", "measured", "error"]);
        structured.GetProperty("languages").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Test]
    public async Task CheckDeck_WhenTranslationsAreOff_ShouldLeaveEveryFindingLanguageNull()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await ThreeSlideDeckAsync(agent);
        Stored.FindingsFor(2, Finding(2, FindingRule.GenericAlt, FindingSource.Stored));
        Engine.FindingsFor(1, Finding(1, FindingRule.SmallText, FindingSource.Measured));
        Engine.FailSlide(3, new RenderingFailedException("image not loaded"));

        var (isError, json) = await agent.CallToolAsync("check_deck", new { deck });

        isError.ShouldBeFalse(json.GetRawText());
        json.GetProperty("languages").GetArrayLength().ShouldBe(0);
        var findings = json.GetProperty("findings").EnumerateArray().ToList();
        findings.Select(f => f.GetProperty("rule").GetString()).ShouldBe(["smallText", "genericAlt", "renderFailed"]);
        findings.Select(f => f.GetProperty("language").ValueKind).ShouldAllBe(kind => kind == JsonValueKind.Null);
    }

    private static SlideFinding Finding(int slide, FindingRule rule, FindingSource source)
        => new(slide, rule, SlideFinding.SeverityOf(rule), source, "text", "Change it.");

    private static async Task<string> ThreeSlideDeckAsync(McpTestClient agent, int slides = 3)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent, "Checks");
        for (var i = 0; i < slides; i++)
        {
            await DeckSeed.AddQuoteAsync(agent, deck, $"Quote {i}.");
        }

        return deck;
    }
}
