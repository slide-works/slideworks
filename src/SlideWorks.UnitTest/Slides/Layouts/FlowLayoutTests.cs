using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class FlowLayoutTests
{
    private const string PLAIN = """{ "title": "a" }""";
    private const string GROUP = """{ "title": "b", "items": ["x", "y"] }""";

    [Test]
    public void Validate_WhenTwoGroupNodes_ShouldRefuse()
    {
        Validate(Slots($"[{GROUP}, {PLAIN}, {GROUP}]")).Errors.ShouldHaveSingleItem()
            .ShouldBe("flow.nodes[2].items: only one node can hold items");
    }

    [Test]
    public void Validate_WhenAGroupNodeSitsAmongFour_ShouldRefuse()
    {
        Validate(Slots($"[{PLAIN}, {GROUP}, {PLAIN}, {PLAIN}]")).Errors.ShouldHaveSingleItem()
            .ShouldBe("flow.nodes: at most 3 nodes when one holds items");
    }

    [Test]
    public void Validate_WhenAGroupNodeSitsAmongThree_ShouldAccept()
    {
        Validate(Slots($"[{PLAIN}, {GROUP}, {PLAIN}]")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenANodeIsTooTall_ShouldRefuse()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 24));
        var node = $$"""{ "title": "a", "text": "{{text}}" }""";

        var caption = string.Join(" ", Enumerable.Repeat("word", 28));
        var extra = $$"""
            "caption": "{{caption}}", "chips": ["a"], "callout": { "tone": "accent", "text": "{{new string('c', 159)}}" },
            """;

        Validate(Slots($"[{node}, {node}, {node}, {node}]", extra)).Errors.ShouldHaveSingleItem().ShouldStartWith("flow.nodes: too much text to fit");
    }

    [Test]
    public void Validate_WhenOneNode_ShouldRefuse()
    {
        Validate(Slots($"[{PLAIN}]")).Errors.ShouldContain(e => e.StartsWith("flow.nodes", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenAGroupHasOneItem_ShouldRefuse()
    {
        Validate(Slots("""[{ "title": "a" }, { "title": "b", "items": ["x"] }]""")).Errors
            .ShouldContain(e => e.StartsWith("flow.nodes[1].items", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate(Slots($"[{PLAIN}, {PLAIN}]", "\"emphasis\": [{ \"text\": \"big\", \"tone\": \"accent\" }], ")).Errors
            .ShouldContain(e => e.StartsWith("flow.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenOneByOne_ShouldBeNodes()
    {
        Count(Slots($"[{PLAIN}, {PLAIN}, {PLAIN}]", "\"reveal\": \"one-by-one\", ")).ShouldBe(3);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        Count(Slots($"[{PLAIN}, {PLAIN}]")).ShouldBe(0);
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBeNodesPlusOne()
    {
        Count(Slots($"[{PLAIN}, {PLAIN}]", "\"reveal\": \"one-by-one\", \"callout\": { \"tone\": \"accent\", \"text\": \"c\", \"reveal\": \"last\" }, ")).ShouldBe(3);
    }

    private static string Slots(string nodes, string extra = "") => $$"""{ "headline": "Small steps", {{extra}} "nodes": {{nodes}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(FlowLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(FlowLayout.Definition, document.RootElement);
    }
}
