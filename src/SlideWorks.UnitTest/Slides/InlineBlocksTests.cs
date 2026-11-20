using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class InlineBlocksTests
{
    [Test]
    public void Blocks_WhenListed_ShouldIncludeLevelsAndFacts()
    {
        LayoutCatalogue.Blocks.Select(b => b.Name).ShouldBe(["callout", "chip", "badge", "stamp", "journey", "levels", "facts"]);
        LayoutCatalogue.Blocks.Where(b => !b.SetByAgent).Select(b => b.Name).ShouldBe(["journey"]);
    }

    [Test]
    public void Validate_WhenTheBadgeIs37_ShouldRefuse()
    {
        Errors(InlineBlocks.Badge, $"\"{new string('a', 36)}\"").ShouldBeEmpty();
        Errors(InlineBlocks.Badge, $"\"{new string('a', 37)}\"").ShouldBe(["slot: at most 36 characters (got 37)"]);
    }

    [Test]
    public void Validate_WhenACalloutSetsReveal_ShouldAcceptWithSlideAndLast()
    {
        Errors(InlineBlocks.Callout, """{ "tone": "accent", "text": "t" }""").ShouldBeEmpty();
        Errors(InlineBlocks.Callout, """{ "tone": "accent", "text": "t", "reveal": "with-slide" }""").ShouldBeEmpty();
        Errors(InlineBlocks.Callout, """{ "tone": "accent", "text": "t", "reveal": "last" }""").ShouldBeEmpty();
        Errors(InlineBlocks.Callout, """{ "tone": "accent", "text": "t", "reveal": "first" }""")
            .ShouldBe(["slot.reveal: expected one of 'with-slide', 'last' (got 'first')"]);
    }

    // The step cases count a callout that reveals last; a slide like them must also be one the validator takes.
    [Test]
    public void Validate_WhenAStepCaseHasACalloutWithReveal_ShouldAccept()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("step-cases.json")));
        var withReveal = file.RootElement.EnumerateArray()
            .Where(c => c.GetProperty("slots").TryGetProperty("callout", out var callout) && callout.TryGetProperty("reveal", out _))
            .ToList();

        withReveal.Count.ShouldBe(3);
        foreach (var stepCase in withReveal)
        {
            SlideValidator.Validate(stepCase.GetProperty("layout").GetString()!, stepCase.GetProperty("slots")).Errors.ShouldBeEmpty();
        }
    }

    [Test]
    public void Validate_WhenLevelsIsWithinItsLimits_ShouldAccept()
    {
        Errors(InlineBlocks.Levels, """
            { "title": "Where teams are", "items": [{ "text": "Ad hoc" }, { "text": "Repeatable", "mark": "2", "state": "current" },
              { "text": "Measured", "mark": "3", "state": "plain" }] }
            """).ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenLevelsBreaksALimit_ShouldRefuse()
    {
        Errors(InlineBlocks.Levels, """{ "items": [{ "text": "one" }] }""").ShouldBe(["slot.items: at least 2 items"]);
        Errors(InlineBlocks.Levels, $$"""{ "items": [{{string.Join(",", Enumerable.Repeat("""{ "text": "t" }""", 8))}}] }""").ShouldBe(["slot.items: at most 7 items"]);
        Errors(InlineBlocks.Levels, """{ "title": "t" }""").ShouldBe(["slot.items: required"]);
        Errors(InlineBlocks.Levels, $$"""{ "title": "{{new string('a', 31)}}", "items": [{ "text": "{{new string('a', 51)}}", "mark": "1234", "state": "done" }, { "mark": "1" }] }""")
            .ShouldBe([
                "slot.title: at most 30 characters (got 31)",
                "slot.items[0].text: at most 50 characters (got 51)",
                "slot.items[0].mark: at most 3 characters (got 4)",
                "slot.items[0].state: expected one of 'plain', 'current' (got 'done')",
                "slot.items[1].text: required",
            ]);
    }

    [Test]
    public void Validate_WhenFactsIsWithinItsLimits_ShouldAccept()
    {
        Errors(InlineBlocks.Facts, """[{ "label": "Teams", "value": "12" }, { "label": "Lead time", "value": "3 days", "tone": "positive" }]""").ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenFactsBreaksALimit_ShouldRefuse()
    {
        Errors(InlineBlocks.Facts, """[{ "label": "Teams", "value": "12" }]""").ShouldBe(["slot: at least 2 items"]);
        Errors(InlineBlocks.Facts, $$"""[{{string.Join(",", Enumerable.Repeat("""{ "label": "l", "value": "v" }""", 5))}}]""").ShouldBe(["slot: at most 4 items"]);
        Errors(InlineBlocks.Facts, $$"""[{ "label": "{{new string('a', 21)}}", "value": "{{new string('a', 29)}}", "tone": "loud" }, { "label": "l" }]""")
            .ShouldBe([
                "slot[0].label: at most 20 characters (got 21)",
                "slot[0].value: at most 28 characters (got 29)",
                "slot[0].tone: expected one of 'accent', 'positive', 'negative', 'inverse' (got 'loud')",
                "slot[1].value: required",
            ]);
    }

    [Test]
    public void Stamp_WhenDescribed_ShouldSayItIsAlsoUsedOnComparePanels()
    {
        InlineBlocks.Stamp.Description.ShouldContain("compare panels");
        new BlockSlot(InlineBlocks.Stamp).Kind.ShouldBe("stamp");
    }

    private static IReadOnlyList<string> Errors(InlineBlock block, string json)
    {
        using var document = JsonDocument.Parse(json);
        var validation = new SlotValidation();
        new BlockSlot(block).Validate(document.RootElement, "slot", validation);
        return validation.Errors;
    }
}
