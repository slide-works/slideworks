using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Checks;

// K9: the fake engine tells a render in a language from a primary render, by its recorded call and its findings.
[TestFixture]
public class FakeRenderEngineTests
{
    [Test]
    public async Task RenderSlideAsync_WhenALanguageIsGiven_ShouldRecordItInTheCall()
    {
        var engine = new FakeRenderEngine();
        var thai = new SlideFinding(2, FindingRule.Overflow, FindingSeverity.Error, FindingSource.Measured, "text", "Cut it.", "th");
        var primary = new SlideFinding(2, FindingRule.SmallText, FindingSeverity.Warning, FindingSource.Measured, "text", "Enlarge it.");
        engine.FindingsFor(2, "th", thai);
        engine.FindingsFor(2, primary);

        var inThai = await engine.RenderSlideAsync("dk_one", 2, "th", CancellationToken.None);
        var inPrimary = await engine.RenderSlideAsync("dk_one", 2, null, CancellationToken.None);

        engine.Calls.ShouldBe(["slide:dk_one:2:th", "slide:dk_one:2"]);
        inThai.Findings.ShouldBe([thai]);
        inPrimary.Findings.ShouldBe([primary]);
    }
}
