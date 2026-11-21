using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Checks;

// K2a: src/contracts/screens/deck-checks.json is the shape both sides build against.
[TestFixture]
public class DeckChecksSampleTests
{
    [Test]
    public void Sample_WhenDeserialised_ShouldRoundTripToTheSameJson()
    {
        var text = File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "deck-checks.json"));

        var result = JsonSerializer.Deserialize<DeckChecksResult>(text, FindingJson.Options)!;

        result.Findings.Select(f => f.Rule).ShouldBe([FindingRule.Fit, FindingRule.Overflow, FindingRule.TextOnImage]);
        result.Error.ShouldBeNull();
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(result, FindingJson.Options), JsonNode.Parse(text)).ShouldBeTrue();
    }
}
