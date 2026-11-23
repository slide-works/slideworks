using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.UnitTest.Slides.Layouts;

[TestFixture]
public class QuoteLayoutTests
{
    [Test]
    public void Validate_WhenStatHasNoCaption_ShouldRefuse()
    {
        Validate("""{ "quote": "Ship it", "attribution": "Sam", "stat": { "value": "40%" } }""").Errors.ShouldBe(["quote.stat.caption: required"]);
    }

    [Test]
    public void Validate_WhenStatValueHasAUnit_ShouldAccept()
    {
        Validate("""{ "quote": "Ship it", "attribution": "Sam", "stat": { "value": "40% faster", "caption": "lead time" } }""").Errors.ShouldBeEmpty();
    }

    // The value breaks anywhere at 72 px with line-height 1 in the 360 px column, so a value that wraps to three
    // lines takes two more lines (144 px) of the quote's budget than one that fits on one.
    [Test]
    public void Validate_WhenStatValueWraps_ShouldCountEachLineInTheHeight()
    {
        var oneLine = QuoteBudget("40%");
        var threeLines = QuoteBudget(new string('W', 12));

        (oneLine - threeLines).ShouldBe(144, 0.5);
    }

    [Test]
    public void Validate_WhenAttributionIs81_ShouldRefuse()
    {
        var attribution = new string('a', 81);
        Validate($$"""{ "quote": "Ship it", "attribution": "{{attribution}}" }""").Errors.ShouldHaveSingleItem().ShouldContain("at most 80 characters");
    }

    [Test]
    public void Validate_WhenRoleIs160_ShouldAccept()
    {
        var role = string.Join(' ', Enumerable.Repeat("Head of search", 12))[..160];
        Validate($$"""{ "quote": "Ship it", "attribution": "Sam", "role": "{{role}}" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheFullQuoteIsOverTheHeight_ShouldRefuse()
    {
        var quote = string.Join(' ', Enumerable.Repeat("small steps", 30))[..280];
        var headline = string.Join(' ', Enumerable.Repeat("Small steps", 10))[..80];
        var caption = string.Join(' ', Enumerable.Repeat("weeks of waiting", 10))[..90];
        var followUp = string.Join(' ', Enumerable.Repeat("and then we learn", 12))[..160];
        var source = string.Join(' ', Enumerable.Repeat("the retro notes", 20))[..200];
        var errors = Validate($$"""
            { "kicker": "The retro", "headline": "{{headline}}", "quote": "{{quote}}", "attribution": "Sam", "role": "Head of search",
              "stat": { "value": "11 wks", "caption": "{{caption}}" }, "follow_up": "{{followUp}}", "source": "{{source}}" }
            """).Errors;

        errors.ShouldHaveSingleItem().ShouldStartWith("quote.quote: too much text to fit on the slide (about ");
    }

    [Test]
    public void Validate_WhenOnlyTodaysSlotsAreSet_ShouldSkipTheFitRule()
    {
        var quote = string.Join(' ', Enumerable.Repeat("small steps", 30))[..280];
        Validate($$"""{ "kicker": "The retro", "quote": "{{quote}}", "attribution": "Sam" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Count_WhenStatAndFollowUp_ShouldBeTwo()
    {
        Count("""{ "quote": "q", "attribution": "a", "stat": { "value": "1", "caption": "c" }, "follow_up": "f", "reveal": "one-by-one" }""").ShouldBe(2);
    }

    [Test]
    public void Count_WhenOnlyFollowUp_ShouldBeOne()
    {
        Count("""{ "quote": "q", "attribution": "a", "follow_up": "f", "reveal": "one-by-one" }""").ShouldBe(1);
    }

    [Test]
    public void Count_WhenRevealIsAll_ShouldBeZero()
    {
        Count("""{ "quote": "q", "attribution": "a", "stat": { "value": "1", "caption": "c" }, "follow_up": "f", "reveal": "all" }""").ShouldBe(0);
    }

    [Test]
    public void Validate_WhenTheEmphasisPhraseIsInTheQuote_ShouldAccept()
    {
        Validate("""{ "quote": "We ship small", "attribution": "Sam", "emphasis": [{ "text": "small", "tone": "accent" }] }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenEmphasisNamesAHeadlineThatIsNotSet_ShouldRefuse()
    {
        Validate("""{ "quote": "We ship small", "attribution": "Sam", "emphasis": [{ "text": "small", "tone": "accent", "in": "headline" }] }""").Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void Definition_WhenRead_ShouldAllowEitherSurface()
    {
        QuoteLayout.Definition.Surface.ShouldBe(LayoutSurface.Either);
    }

    private static SlideValidationResult Validate(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(QuoteLayout.Definition, document.RootElement, FitWidths.Default, "light");
    }

    private static int Count(string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideSteps.Count(QuoteLayout.Definition, document.RootElement);
    }

    private static double QuoteBudget(string value) =>
        Validate($$"""{ "quote": "Ship it", "attribution": "Sam", "stat": { "value": "{{value}}", "caption": "weeks" } }""")
            .Regions.Single(region => region.Path == "quote.quote").Budget;
}
