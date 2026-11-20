using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class EmphasisRulesTests
{
    private static readonly string[] _targets = ["headline", "caption", "quote.text"];

    [Test]
    public void Check_WhenEachPhraseOccursOnce_ShouldAccept()
    {
        Check("""
            { "headline": "Small steps beat big plans", "caption": "Every week, not every quarter",
              "emphasis": [{ "text": "Small steps", "tone": "accent" }, { "text": "big plans", "tone": "negative" },
                           { "text": "Every week", "tone": "positive", "in": "caption" }] }
            """).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenThereIsNoEmphasis_ShouldAccept()
    {
        Check("""{ "headline": "Small steps beat big plans" }""").ShouldBeEmpty();
        Check("""{ "headline": "Small steps beat big plans", "emphasis": null }""").ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenThePhraseOccursTwice_ShouldRefuse()
    {
        Check("""{ "headline": "Step by step by step", "emphasis": [{ "text": "by step", "tone": "accent" }] }""")
            .ShouldBe(["big-number.emphasis[0].text: 'by step' occurs 2 times in headline; emphasis needs a phrase that occurs once"]);
        Check("""{ "headline": "aaa", "emphasis": [{ "text": "aa", "tone": "accent", "in": "headline" }] }""")
            .ShouldBe(["big-number.emphasis[0].text: 'aa' occurs 2 times in headline; emphasis needs a phrase that occurs once"]);
    }

    [Test]
    public void Check_WhenThePhraseIsNotInTheText_ShouldRefuse()
    {
        Check("""{ "headline": "Small steps", "caption": "c", "emphasis": [{ "text": "small steps", "tone": "accent", "in": "headline" }] }""")
            .ShouldBe(["big-number.emphasis[0].text: 'small steps' is not in headline"]);
        Check("""{ "headline": "Small steps", "caption": "c", "emphasis": [{ "text": "big", "tone": "accent" }] }""")
            .ShouldBe(["big-number.emphasis[0].text: 'big' is not in headline or caption"]);
        Check("""{ "emphasis": [{ "text": "big", "tone": "accent" }] }""")
            .ShouldBe(["big-number.emphasis[0].text: 'big' is not in headline or caption or quote.text"]);
    }

    [Test]
    public void Check_WhenInNamesATargetThatIsNotSet_ShouldRefuse()
    {
        Check("""{ "headline": "Small steps", "emphasis": [{ "text": "Small", "tone": "accent", "in": "caption" }] }""")
            .ShouldBe(["big-number.emphasis[0].in: caption is not set"]);
        Check("""{ "headline": "Small steps", "caption": null, "emphasis": [{ "text": "Small", "tone": "accent", "in": "caption" }] }""")
            .ShouldBe(["big-number.emphasis[0].in: caption is not set"]);
        Check("""{ "headline": "Small steps", "quote": { "by": "x" }, "emphasis": [{ "text": "Small", "tone": "accent", "in": "quote.text" }] }""")
            .ShouldBe(["big-number.emphasis[0].in: quote.text is not set"]);
    }

    [Test]
    public void Check_WhenTwoTargetsHoldThePhraseAndInIsLeftOut_ShouldRefuse()
    {
        const string slots = """{ "headline": "Small steps", "caption": "Small wins", "emphasis": [{ "text": "Small", "tone": "accent"IN }] }""";

        Check(slots.Replace("IN", "", StringComparison.Ordinal))
            .ShouldBe(["big-number.emphasis[0].text: 'Small' is in headline and caption; say which with 'in'"]);
        Check(slots.Replace("IN", """, "in": "caption" """, StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenInNamesADottedTarget_ShouldLookInsideTheObject()
    {
        Check("""{ "quote": { "text": "Make it small" }, "emphasis": [{ "text": "small", "tone": "accent", "in": "quote.text" }] }""").ShouldBeEmpty();
        Check("""{ "quote": { "text": "Make it small" }, "emphasis": [{ "text": "small", "tone": "accent" }] }""").ShouldBeEmpty();
    }

    [Test]
    public void Check_WhenEntriesOverlap_ShouldRefuse()
    {
        Check("""
            { "headline": "Small steps beat big plans",
              "emphasis": [{ "text": "Small steps", "tone": "accent" }, { "text": "steps beat", "tone": "positive" }] }
            """).ShouldBe(["big-number.emphasis[1].text: 'steps beat' overlaps emphasis[0]"]);
        Check("""
            { "headline": "Small steps beat big plans",
              "emphasis": [{ "text": "steps", "tone": "accent" }, { "text": "Small steps beat", "tone": "positive" }, { "text": "big", "tone": "accent" }] }
            """).ShouldBe(["big-number.emphasis[1].text: 'Small steps beat' overlaps emphasis[0]"]);
    }

    [Test]
    public void Check_WhenTheSamePhraseIsInTwoTexts_ShouldNotCallThatAnOverlap()
    {
        Check("""
            { "headline": "Small steps", "caption": "Small wins",
              "emphasis": [{ "text": "Small", "tone": "accent", "in": "headline" }, { "text": "Small", "tone": "accent", "in": "caption" }] }
            """).ShouldBeEmpty();
    }

    [TestCase("""{ "headline": "h", "emphasis": "h" }""")]
    [TestCase("""{ "headline": "h", "emphasis": ["h", 7, null, {}] }""")]
    [TestCase("""{ "headline": "h", "emphasis": [{ "text": 7 }, { "text": "" }, { "text": "h", "in": 7 }, { "text": "h", "in": "kicker" }] }""")]
    [TestCase("""{ "headline": 7, "caption": ["h"], "emphasis": [{ "text": "h", "in": 7 }] }""")]
    [TestCase("[]")]
    public void Check_WhenTheShapeIsWrong_ShouldLeaveItToTheSchema(string slots)
    {
        Should.NotThrow(() => Check(slots));
    }

    [Test]
    public void Check_WhenAPhraseIsLongOrHoldsControlCharacters_ShouldEchoItSafely()
    {
        var errors = Check($$"""{ "headline": "h", "emphasis": [{ "text": "a\nb{{new string('x', 500)}}", "tone": "accent" }] }""");

        errors.Count.ShouldBe(1);
        errors[0].ShouldStartWith("big-number.emphasis[0].text: 'a\\nbxxx");
        errors[0].Length.ShouldBeLessThan(120);
    }

    [Test]
    public void Validate_WhenTheEmphasisIsWellFormed_ShouldAccept()
    {
        Validate("""[{ "text": "Small steps", "tone": "accent" }, { "text": "big", "tone": "negative", "in": "caption" }]""").ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheEmphasisBreaksItsLimits_ShouldRefuse()
    {
        Validate("[]").ShouldBe(["big-number.emphasis: at least 1 item"]);
        Validate("""[{ "text": "a", "tone": "accent" }, { "text": "b", "tone": "accent" }, { "text": "c", "tone": "accent" }, { "text": "d", "tone": "accent" }]""")
            .ShouldBe(["big-number.emphasis: at most 3 items"]);
        Validate($$"""[{ "text": "{{new string('a', 61)}}", "tone": "accent" }]""").ShouldBe(["big-number.emphasis[0].text: at most 60 characters (got 61)"]);
        Validate("""[{ "text": "a", "tone": "inverse" }]""")
            .ShouldBe(["big-number.emphasis[0].tone: expected one of 'accent', 'positive', 'negative' (got 'inverse')"]);
        Validate("""[{ "text": "a", "tone": "accent", "in": "kicker" }]""")
            .ShouldBe(["big-number.emphasis[0].in: expected one of 'headline', 'caption', 'quote.text' (got 'kicker')"]);
        Validate("""[{ "text": "a" }]""").ShouldBe(["big-number.emphasis[0].tone: required"]);
    }

    [Test]
    public void Constructor_WhenNoTargetIsGiven_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => new EmphasisSlot());
    }

    private static IReadOnlyList<string> Check(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        var validation = new SlotValidation();
        EmphasisRules.Check(document.RootElement, "big-number", validation, _targets);
        return validation.Errors;
    }

    private static IReadOnlyList<string> Validate(string emphasis)
    {
        using var document = JsonDocument.Parse(emphasis);
        var validation = new SlotValidation();
        new EmphasisSlot(_targets).Validate(document.RootElement, "big-number.emphasis", validation);
        return validation.Errors;
    }
}
