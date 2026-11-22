using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class LevelsLayoutTests
{
    private const string MAIN = """{ "text": "A short paragraph." }""";

    [Test]
    public void Validate_WhenTheLadderIsTooTall_ShouldRefuse()
    {
        // 44 characters of two words take two lines of 30.8 px: a rung is 81.6 px, and seven with the gaps pass 620 px.
        var rung = $$"""{ "text": "{{new string('a', 20)}} {{new string('b', 23)}}" }""";
        var rungs = "[" + string.Join(", ", Enumerable.Repeat(rung, 7)) + "]";

        Validate(Slots(rungs, MAIN)).Errors.ShouldHaveSingleItem().ShouldStartWith("levels.levels.items: too much text to fit");
    }

    [Test]
    public void Validate_WhenTheLadderIsAtItsLimitsOfSevenFiftyCharacterRungsAndATitle_ShouldRefuseAsTooTall()
    {
        // 7 two-line rungs (81.6 px), 6 gaps, a 30-character title and the padding come to about 703 px: the layout refuses what its card cannot draw at the normal spacing.
        var rung = $$"""{ "text": "{{new string('a', 24)}} {{new string('b', 25)}}" }""";
        var rungs = "[" + string.Join(", ", Enumerable.Repeat(rung, 7)) + "]";

        Validate(Slots(rungs, MAIN, ladderTitle: new string('t', 30))).Errors.ShouldHaveSingleItem().ShouldStartWith("levels.levels.items: too much text to fit");
    }

    [Test]
    public void Validate_WhenSevenSingleLineRungsHaveATitle_ShouldAcceptAndFitTheCard()
    {
        // 7 rungs of 64 px, 6 gaps of 8 px, a 35 px title and 48 px of padding are about 579 px: inside the 620 px card.
        var rungs = "[" + string.Join(", ", Enumerable.Repeat("""{ "text": "Senior Engineer" }""", 7)) + "]";

        Validate(Slots(rungs, MAIN, ladderTitle: new string('t', 30))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAShortLadderHasATitle_ShouldAccept()
    {
        Validate(Slots("""[{ "text": "a" }, { "text": "b", "state": "current" }]""", MAIN, ladderTitle: "Career")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenMainAndTilesPassTheBody_ShouldRefuse()
    {
        // A 400-character paragraph is six lines, the four two-line tiles add 150 px and the callout 199 px.
        var main = $$"""{ "title": "{{new string('t', 20)}}", "text": "{{string.Join(' ', Enumerable.Repeat(new string('w', 9), 40))}}" }""";
        var tile = """{ "label": "Label", "value": "Twelve Weeks Per Quarter" }""";
        var facts = $"\"facts\": [{string.Join(", ", Enumerable.Repeat(tile, 4))}], ";
        var extra = facts + $"\"callout\": {{ \"tone\": \"accent\", \"text\": \"{new string('c', 160)}\" }}, ";

        Validate(Slots(TwoRungs, main, extra)).Errors.ShouldHaveSingleItem().ShouldStartWith("levels.main: too much text to fit");
    }

    [Test]
    public void Validate_WhenMainHasItemsAndText_ShouldRefuse()
    {
        Validate(Slots(TwoRungs, """{ "items": ["a"], "text": "b" }""")).Errors.ShouldContain(e => e.StartsWith("levels.main", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenMainHasAnIconAndNoTitle_ShouldRefuse()
    {
        Validate(Slots(TwoRungs, """{ "icon": "rocket", "text": "b" }""")).Errors.ShouldContain(e => e.StartsWith("levels.main.icon", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheLadderHasOneRung_ShouldRefuse()
    {
        Validate(Slots("""[{ "text": "a" }]""", MAIN)).Errors.ShouldContain(e => e.StartsWith("levels.levels", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenFiveTiles_ShouldRefuse()
    {
        var tile = """{ "label": "Label", "value": "Value" }""";

        Validate(Slots(TwoRungs, MAIN, $"\"facts\": [{string.Join(", ", Enumerable.Repeat(tile, 5))}], ")).Errors
            .ShouldContain(e => e.StartsWith("levels.facts", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate(Slots(TwoRungs, MAIN, "\"emphasis\": [{ \"text\": \"big\", \"tone\": \"accent\" }], ")).Errors
            .ShouldContain(e => e.StartsWith("levels.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheSideIsLeft_ShouldAccept()
    {
        Validate(Slots(TwoRungs, MAIN, "\"side\": \"left\", ")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenRead_ShouldBeZero()
    {
        Count(Slots(TwoRungs, MAIN)).ShouldBe(0);
    }

    [Test]
    public void Count_WhenCalloutRevealsLast_ShouldBeOne()
    {
        Count(Slots(TwoRungs, MAIN, "\"callout\": { \"tone\": \"accent\", \"text\": \"c\", \"reveal\": \"last\" }, ")).ShouldBe(1);
    }

    private static string TwoRungs => """[{ "text": "a" }, { "text": "b" }]""";

    private static string Slots(string rungs, string main, string extra = "", string? ladderTitle = null)
    {
        var title = ladderTitle is null ? "" : $"\"title\": \"{ladderTitle}\", ";
        return $$"""{ "headline": "Small steps", {{extra}} "levels": { {{title}}"items": {{rungs}} }, "main": {{main}} }""";
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(LevelsLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(LevelsLayout.Definition, document.RootElement);
    }
}
