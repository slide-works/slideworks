using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// The headline and kicker rules every layout shares (HeadingRules, called from LayoutDefinition.Validate).
// The client shrinks a headline to two lines, down to 48 px: 1560 px holds 58 average characters a line
// there, and a capital A costs 1.39 of them, so one unbroken word of 41 As fills a line and 82 fill two.
[TestFixture]
public class HeadingRulesTests
{
    private const string PROSE = "every slip we had last year began as a big batch that nobody could ship, so we cut every batch down.";

    [Test]
    public void Validate_WhenAHundredLowercaseCharactersAreTheHeadline_ShouldAccept()
    {
        PROSE.Length.ShouldBe(100);
        Validate("bullets", Bullets(headline: PROSE)).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAHundredWidCapitalsAreTheHeadline_ShouldRefuseItAsTooMuchText()
    {
        Validate("bullets", Bullets(headline: new string('W', 100))).Errors
            .ShouldHaveSingleItem().ShouldBe("bullets.headline: headline: too much text to fit in 2 lines (it wraps to about 4 even at the smallest size); shorten it");
    }

    [Test]
    public void Validate_WhenTheHeadlineFillsExactlyTwoLinesAtTheFloor_ShouldAccept()
    {
        Validate("bullets", Bullets(headline: new string('A', 82))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheHeadlineIsOneCharacterOverTwoLinesAtTheFloor_ShouldRefuse()
    {
        Validate("bullets", Bullets(headline: new string('A', 83))).Errors
            .ShouldHaveSingleItem().ShouldContain("too much text to fit in 2 lines", Case.Sensitive);
    }

    [Test]
    public void Validate_WhenAQuoteHeadlineIsHundredWideCapitals_ShouldNotApplyTheSharedRule()
    {
        Validate("quote", """{ "headline": "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW", "quote": "q", "attribution": "a" }""")
            .Errors.ShouldNotContain(e => e.Contains("2 lines", StringComparison.Ordinal));
    }

    [Test]
    public void Validate_WhenAnImageRightHeadlineWrapsToThreeLines_ShouldAccept()
    {
        // 37 characters wrap to 3 lines in the 760 px column at 64 px: ImageLayout allows 3 there.
        Validate("image", Image("right", headline: "Make every merge small and reviewable")).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTheKickerIsSixtyWideCapitalsAcrossTheFullWidth_ShouldAccept()
    {
        Validate("bullets", Bullets(kicker: new string('W', 60))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAnImageRightKickerFillsTheColumn_ShouldAccept()
    {
        Validate("image", Image("right", kicker: new string('W', 27))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAnImageRightKickerIsOneLetterTooWide_ShouldRefuseItAsTooMuchText()
    {
        Validate("image", Image("right", kicker: new string('W', 28))).Errors
            .ShouldHaveSingleItem().ShouldBe("image.kicker: kicker: too much text to fit on one line (it is drawn in capitals with wide letter-spacing); shorten it");
    }

    [Test]
    public void Validate_WhenAnImageBelowKickerIsTheSameWidth_ShouldAcceptIt()
    {
        Validate("image", Image("below", kicker: new string('W', 28))).Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAJoinKickerFillsItsColumn_ShouldAccept()
    {
        Validate("join", $$"""{ "kicker": "{{new string('A', 23)}}" }""").Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenAJoinKickerIsOneLetterTooWide_ShouldRefuse()
    {
        Validate("join", $$"""{ "kicker": "{{new string('A', 24)}}" }""").Errors
            .ShouldHaveSingleItem().ShouldContain("kicker: too much text to fit on one line", Case.Sensitive);
    }

    [Test]
    public void Validate_WhenALowercaseKickerWouldWrapOnlyOnceUppercased_ShouldCountItAsCapitals()
    {
        // 24 lowercase letters are 24 average characters, under a 32 a line, but drawn as capitals they are not.
        Validate("join", $$"""{ "kicker": "{{new string('a', 24)}}" }""").Errors.ShouldHaveSingleItem();
    }

    private static string Bullets(string? headline = null, string? kicker = null) =>
        $$"""{ {{(kicker is null ? "" : $"\"kicker\": \"{kicker}\",")}} "headline": "{{headline ?? "Short"}}", "items": [{ "text": "t" }] }""";

    private static string Image(string position, string? headline = null, string? kicker = null) =>
        $$"""{ {{(kicker is null ? "" : $"\"kicker\": \"{kicker}\",")}} "headline": "{{headline ?? "Short"}}", "image": { "image": "img_sample0001", "alt": "A diagram" }, "position": "{{position}}" }""";

    private static SlideValidationResult Validate(string layout, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(layout, document.RootElement);
    }
}
