using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class NumberedCardsLayoutTests
{
    [Test]
    public void Validate_WhenACardIsOverTheHeightInFourColumns_ShouldRefuse()
    {
        // 14-letter words never share a 26-character line, so nine of them take nine lines; a pin and a plain fill make the card 622 px.
        var title = string.Join(' ', Enumerable.Repeat("bbbbbbbbbb", 3));
        var text = string.Join(' ', Enumerable.Repeat("cccccccccccccc", 9));
        var card = $$"""{ "title": "{{title}}", "text": "{{text}}", "pin": "Hold this", "fill": "plain" }""";
        var plain = $$"""{ "title": "{{title}}", "text": "{{text}}", "pin": "Hold this" }""";

        Validate(Slots($"{plain}, {plain}, {plain}, {plain}")).Errors.ShouldBeEmpty();
        Validate(Slots($"{card}, {card}, {card}, {card}")).Errors
            .ShouldContain(e => e.StartsWith("numbered-cards.cards[0]", StringComparison.Ordinal) && e.Contains("too much text to fit", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenThreeShortCards_ShouldAccept()
    {
        var card = """{ "title": "Who notices?", "text": "Name the customer." }""";

        Validate(Slots($"{card}, {card}, {card}")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenACalloutTakesTheHeight_ShouldRefuse()
    {
        var card = $$"""{ "title": "Responsibility Accountability", "text": "{{string.Join(' ', Enumerable.Repeat("Internationalisation Telecommunications", 3))}}", "pin": "Hold on to this one" }""";
        var cards = $"{card}, {card}, {card}";
        var callout = $$"""{ "tone": "info", "text": "{{string.Join(' ', Enumerable.Repeat("ab", 40))}}" }""";

        Validate(Slots(cards)).Errors.ShouldBeEmpty();
        Validate(Slots(cards, $$""", "callout": {{callout}}""")).Errors.ShouldContain(e => e.StartsWith("numbered-cards.cards[0]", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenPinIs24_ShouldAccept()
    {
        Validate(Slots(Two($$""", "pin": "{{new string('p', 24)}}" """, string.Empty))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenPinIs25_ShouldRefuse()
    {
        Validate(Slots(Two($$""", "pin": "{{new string('p', 25)}}" """, string.Empty))).Errors
            .ShouldContain(e => e.StartsWith("numbered-cards.cards[0].pin", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenFillIsPlain_ShouldAccept()
    {
        Validate(Slots(Two(""", "tone": "accent", "fill": "plain" """, string.Empty))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenFillIsUnknown_ShouldRefuse()
    {
        Validate(Slots(Two(""", "fill": "glow" """, string.Empty))).Errors
            .ShouldContain(e => e.StartsWith("numbered-cards.cards[0].fill", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenOneByOne_ShouldBeCards()
    {
        Count(Slots(Three(), reveal: "one-by-one")).ShouldBe(3);
    }

    [Test]
    public void Count_WhenOneByOneAndCalloutRevealsLast_ShouldBeCardsPlusOne()
    {
        var callout = """, "callout": { "tone": "info", "text": "c", "reveal": "last" }""";

        Count(Slots(Two(string.Empty, string.Empty), callout, "one-by-one")).ShouldBe(3);
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        var slots = $$"""{ "headline": "Small steps", "cards": [{{CardWith(string.Empty)}}, {{CardWith(string.Empty)}}], "emphasis": [{ "text": "big", "tone": "accent" }] }""";

        Validate(slots).Errors.ShouldContain(e => e.StartsWith("numbered-cards.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Definition_WhenRead_ShouldAllowOnlyLightSurface()
    {
        NumberedCardsLayout.Definition.Surface.ShouldBe(LayoutSurface.Light);
    }

    private static string CardWith(string extra) => $$"""{ "title": "Title", "text": "Text"{{extra}} }""";

    private static string Slots(string cards, string extra = "", string? reveal = null) =>
        $$"""{ "headline": "h", "cards": [{{cards}}]{{(reveal is null ? string.Empty : $", \"reveal\": \"{reveal}\"")}}{{extra}} }""";

    private static string Two(string first, string second) => $"{CardWith(first)}, {CardWith(second)}";

    private static string Three() => $"{CardWith(string.Empty)}, {CardWith(string.Empty)}, {CardWith(string.Empty)}";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(NumberedCardsLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(NumberedCardsLayout.Definition, document.RootElement);
    }
}
