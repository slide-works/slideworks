using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class CardsLayoutTests
{
    private const string CARD = """{ "title": "t", "text": "x", "chips": [] }""";

    [Test]
    public void Validate_WhenFourCards_ShouldAccept()
    {
        Validate(Cards(CARD, 4)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenFiveCards_ShouldRefuse()
    {
        Validate(Cards(CARD, 5)).Errors.ShouldContain("cards.cards: at most 4 items");
    }

    [Test]
    public void Validate_WhenTheTallestCardIsOverTheHeight_ShouldRefuse()
    {
        // Four regular cards in the 2×2: 56 + 78.2 (two title lines) + 12 + 100.8 (three text lines) + a 60 px
        // row of chips + a 48 px badge is 355 px, over the 296 px of half the body less the gap.
        var title = new string('a', 40) + " " + new string('b', 9);
        var text = $"{new string('a', 50)} {new string('b', 50)} {new string('c', 30)}";
        var card = $$"""{ "badge": "b", "title": "{{title}}", "text": "{{text}}", "chips": ["c"] }""";

        Validate(Cards(card, 4)).Errors.ShouldHaveSingleItem()
            .ShouldBe("cards.cards[0]: too much text to fit on the slide (about 355 px of 296 px); use fewer or shorter items");
    }

    [Test]
    public void Validate_WhenACalloutTakesTheHeight_ShouldRefuse()
    {
        // The card is 68 + 48 (badge) + 78 (icon) + 3 * 39.1 (title) + 12 + 33.6 (text) + 204 (four chips, a
        // row each) = 560.9 px: it fits the 620 px body, but not the 496.2 px a one-line callout leaves.
        var title = $"{new string('a', 23)} {new string('b', 23)} c";
        var card = $$"""{ "badge": "b", "icon": "coins", "title": "{{title}}", "text": "x", "chips": ["aaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccccc", "dddddddddddddddddddddddd"] }""";
        const string CALLOUT = """, "callout": { "tone": "accent", "text": "c" }""";

        Validate(Cards(card, 3, "")).Errors.ShouldBeEmpty();
        Validate(Cards(card, 3, CALLOUT)).Errors.ShouldHaveSingleItem()
            .ShouldStartWith("cards.cards[0]: too much text to fit on the slide (about 561 px of 496.2 px)");
    }

    [Test]
    public void Validate_WhenPinIs25_ShouldRefuse()
    {
        Validate(Cards($$"""{ "title": "t", "text": "x", "chips": [], "pin": "{{new string('p', 25)}}" }""", 1)).Errors
            .ShouldContain(e => e.StartsWith("cards.cards[0].pin", StringComparison.Ordinal) && e.Contains("24", StringComparison.Ordinal));
        Validate(Cards($$"""{ "title": "t", "text": "x", "chips": [], "pin": "{{new string('p', 24)}}" }""", 1)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenFillIsPlain_ShouldAccept()
    {
        Validate(Cards("""{ "title": "t", "text": "x", "chips": [], "tone": "accent", "fill": "plain" }""", 2)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheIconIsUnknown_ShouldRefuse()
    {
        Validate(Cards("""{ "icon": "zzzzzzzzzz", "title": "t", "text": "x", "chips": [] }""", 1)).Errors
            .ShouldContain(e => e.StartsWith("cards.cards[0].icon: unknown icon 'zzzzzzzzzz'", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsNotInTheHeadline_ShouldRefuse()
    {
        Validate($$"""{ "headline": "Small steps", "cards": [{{CARD}}], "emphasis": [{ "text": "big", "tone": "accent" }] }""").Errors
            .ShouldContain(e => e.StartsWith("cards.emphasis[0].text", StringComparison.Ordinal));
    }

    [Test]
    public void Count_WhenFourCardsOneByOne_ShouldBeFour()
    {
        Count(Cards(CARD, 4, """, "reveal": "one-by-one" """)).ShouldBe(4);
    }

    [Test]
    public void Count_WhenACalloutRevealsLast_ShouldAddOne()
    {
        Count(Cards(CARD, 3, """, "reveal": "one-by-one", "callout": { "tone": "accent", "text": "c", "reveal": "last" } """)).ShouldBe(4);
    }

    private static string Cards(string card, int count, string extra = "") =>
        $$"""{ "headline": "h", "cards": [{{string.Join(", ", Enumerable.Repeat(card, count))}}]{{extra}} }""";

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(CardsLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(CardsLayout.Definition, document.RootElement);
    }
}
