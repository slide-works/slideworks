using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class SlideValidatorTests
{
    private static IEnumerable<TestCaseData> SampleSlides() =>
        Enumerable.Range(1, 16).Select(n => new TestCaseData(n).SetArgDisplayNames(n.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [TestCaseSource(nameof(SampleSlides))]
    public void Validate_WhenSampleDeckSlide_ShouldPass(int number)
    {
        var slide = SampleDeck.Slide(number);

        var result = SlideValidator.Validate(slide.GetProperty("layout").GetString()!, slide.GetProperty("slots"));

        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenSampleDeckImageSlide_ShouldReturnItsImageId()
    {
        var slide = SampleDeck.Slide(10);

        var result = SlideValidator.Validate("two-column", slide.GetProperty("slots"));

        result.ImageIds.ShouldBe(["img_sample0001"]);
    }

    [Test]
    public void Validate_WhenRequiredSlotMissing_ShouldNameThePath()
    {
        var result = Validate("practice", """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }, { "lead": "c", "text": "d" }, { "text": "e" }],
              "comes_back": [], "avoid": [] }
            """);

        result.Error.ShouldBe("practice.do[2].lead: required");
    }

    // An escaped lone surrogate (half an emoji) in a field name, slot text or an enum value can't be read as a .NET
    // string. It must come back as a validation error, not an exception (a 500 through the write path).
    [TestCase("""{ "quote": "Ship it \ud83d", "attribution": "Sam" }""")]
    [TestCase("""{ "quote": "Ship it.", "attribution": "Sam", "\udc00": "x" }""")]
    [TestCase("""{ "quote": "Ship it.", "attribution": "Sam", "kicker": ["\ud83d"] }""")]
    public void Validate_WhenTextHasLoneSurrogate_ShouldSayItIsNotValidUtf16(string slots)
    {
        var result = Validate("quote", slots);

        result.Error.ShouldBe("slots: text is not valid UTF-16");
    }

    [Test]
    public void Validate_WhenEnumValueHasLoneSurrogate_ShouldSayItIsNotValidUtf16()
    {
        var result = Validate("bullets", """{ "headline": "Ship", "items": ["a"], "reveal": "\ud83dall" }""");

        result.Error.ShouldBe("slots: text is not valid UTF-16");
    }

    [Test]
    public void Validate_WhenRequiredTopLevelSlotMissing_ShouldNameIt()
    {
        var result = Validate("quote", """{ "quote": "Ship it." }""");

        result.Error.ShouldBe("quote.attribution: required");
    }

    [Test]
    public void Validate_WhenUnknownField_ShouldNameIt()
    {
        var result = Validate("practice", """
            { "headline": "Slice it", "do": [{ "lead": "a", "text": "b" }], "comes_back": [], "avoid": [], "foo": 1 }
            """);

        result.Error.ShouldBe("practice.foo: unknown field");
    }

    [Test]
    public void Validate_WhenTooManyItems_ShouldNameTheLimit()
    {
        var items = string.Join(", ", Enumerable.Range(1, 9).Select(i => $"\"Point {i}\""));

        var result = Validate("bullets", $$"""{ "headline": "Nine points", "items": [{{items}}] }""");

        result.Error.ShouldBe("bullets.items: at most 8 items");
    }

    [Test]
    public void Validate_WhenTooFewItems_ShouldNameTheLimit()
    {
        var result = Validate("numbered-cards", """{ "headline": "One card", "cards": [{ "title": "a", "text": "b" }] }""");

        result.Error.ShouldBe("numbered-cards.cards: at least 2 items");
    }

    [Test]
    public void Validate_WhenTooLong_ShouldNameTheLimit()
    {
        var result = Validate("big-number", """{ "headline": "Lead time", "value": "eleven weeks", "caption": "Median" }""");

        result.Error.ShouldBe("big-number.value: at most 6 characters (got 12)");
    }

    [Test]
    public void Validate_WhenUnknownLayout_ShouldListKnownOnes()
    {
        var result = Validate("foo", "{}");

        result.Error.ShouldBe("layout: unknown layout 'foo' (known: title, divider, bullets, two-column, practice, compare, "
            + "big-number, quote, image, table, chart, gantt, numbered-cards, rules, cards, close)");
    }

    [TestCase("""{ "headline": 5, "items": ["a"] }""", "bullets.headline: expected text")]
    [TestCase("""{ "headline": "h", "items": "a" }""", "bullets.items: expected a list")]
    [TestCase("""{ "headline": "h", "items": [true] }""", "bullets.items[0]: expected text or an object")]
    [TestCase("""{ "headline": "h", "items": ["a"], "reveal": "some" }""", "bullets.reveal: expected one of 'all', 'one-by-one' (got 'some')")]
    [TestCase("""{ "headline": "h", "items": ["a"], "callout": "careful" }""", "bullets.callout: expected an object")]
    public void Validate_WhenWrongKind_ShouldNameExpectedKind(string slots, string error)
    {
        Validate("bullets", slots).Error.ShouldBe(error);
    }

    [Test]
    public void Validate_WhenSlotsNotAnObject_ShouldSayExpectedObject()
    {
        Validate("quote", "[]").Error.ShouldBe("quote: expected an object");
    }

    [Test]
    public void Validate_WhenOptionalSlotIsNull_ShouldTreatItAsAbsent()
    {
        Validate("quote", """{ "kicker": null, "quote": "Ship it.", "attribution": "Sam" }""").IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenTextIsEmpty_ShouldSayNotEmpty()
    {
        Validate("quote", """{ "quote": " ", "attribution": "Sam" }""").Error.ShouldBe("quote.quote: must not be empty");
    }

    [Test]
    public void Validate_WhenLineBreakOutsideCloseHeadline_ShouldRefuseIt()
    {
        Validate("quote", """{ "quote": "one\ntwo", "attribution": "Sam" }""").Error.ShouldBe("quote.quote: line breaks are not allowed here");
    }

    [Test]
    public void Validate_WhenCloseHeadlineHasFourLines_ShouldNameTheLimit()
    {
        Validate("close", """{ "headline": "a\nb\nc\nd" }""").Error.ShouldBe("close.headline: at most 3 lines (got 4)");
    }

    [Test]
    public void Validate_WhenTwoColumnSideHasTextAndImage_ShouldRequireExactlyOne()
    {
        var result = Validate("two-column", """
            { "headline": "h", "left": { "text": "a" }, "right": { "text": "b", "image": { "image": "img_1", "alt": "x" } } }
            """);

        result.Error.ShouldBe("two-column.right: exactly one of items, text, image");
    }

    [Test]
    public void Validate_WhenImageIdHasSlash_ShouldRefuseIt()
    {
        var result = Validate("image", """{ "headline": "h", "image": { "image": "../etc", "alt": "x" } }""");

        result.Error.ShouldBe("image.image.image: not an image id (use the id upload_image returned)");
        result.ImageIds.ShouldBeEmpty();
    }

    [Test]
    public void Validate_WhenTableRowHasTooFewCells_ShouldNameTheRow()
    {
        var result = Validate("table", """
            { "headline": "h", "columns": ["a", "b", "c"], "rows": [["1", "2", "3"], ["1", "2"]] }
            """);

        result.Error.ShouldBe("table.rows[1]: 2 cells, but there are 3 columns");
    }

    [Test]
    public void Validate_WhenChartSeriesHasTooFewValues_ShouldNameTheSeries()
    {
        var result = Validate("chart", """
            { "headline": "h", "kind": "line", "xLabels": ["a", "b", "c"], "series": [{ "name": "s", "values": [1, 2] }] }
            """);

        result.Error.ShouldBe("chart.series[0].values: 2 values, but there are 3 xLabels");
    }

    [Test]
    public void Validate_WhenGanttBarEndsPastLastColumn_ShouldNameTheRow()
    {
        var result = Validate("gantt", """
            { "headline": "h", "columns": ["a", "b"], "rows": [{ "label": "x", "start": 1, "end": 3, "status": "done" }] }
            """);

        result.Error.ShouldBe("gantt.rows[0].end: at most 2 (the number of columns)");
    }

    [Test]
    public void Validate_WhenGanttStartIsFractional_ShouldExpectWholeNumber()
    {
        var result = Validate("gantt", """
            { "headline": "h", "columns": ["a", "b"], "rows": [{ "label": "x", "start": 1.5, "end": 2, "status": "done" }] }
            """);

        result.Error.ShouldBe("gantt.rows[0].start: expected a whole number");
    }

    [Test]
    public void Validate_WhenSeveralErrors_ShouldReportEach()
    {
        var result = Validate("quote", """{ "foo": 1 }""");

        result.Errors.ShouldBe(["quote.foo: unknown field", "quote.quote: required", "quote.attribution: required"]);
    }

    [Test]
    public void Validate_WhenListIsFarOverItsMaximum_ShouldNotCheckItsItems()
    {
        var items = string.Join(", ", Enumerable.Repeat("1", 200_000));

        var result = Validate("bullets", $$"""{ "headline": "h", "items": [{{items}}] }""");

        result.Errors.ShouldBe(["bullets.items: at most 8 items"]);
    }

    [Test]
    public void Validate_WhenManyErrors_ShouldKeepTheFirstTwentyAndCountTheRest()
    {
        var fields = string.Join(", ", Enumerable.Range(1, 500).Select(i => $"\"f{i}\": 1"));

        var result = Validate("quote", $$"""{ {{fields}}, "quote": "q", "attribution": "a" }""");

        result.Errors.Count.ShouldBe(21);
        result.Errors[0].ShouldBe("quote.f1: unknown field");
        result.Errors[^1].ShouldBe("…and 480 more");
    }

    [Test]
    public void Validate_WhenFieldIsRepeated_ShouldNameTheDuplicate()
    {
        var result = Validate("quote", """{ "quote": 5, "quote": "ok", "attribution": "a" }""");

        result.Error.ShouldBe("quote.quote: duplicate field");
    }

    [TestCase("a\\u0000b", "quote.quote: control characters are not allowed")]
    [TestCase("one\\u2028two", "quote.quote: line breaks are not allowed here")]
    [TestCase("\\u200B\\u200B ", "quote.quote: must not be empty")]
    public void Validate_WhenTextHasHiddenCharacters_ShouldRefuseIt(string jsonQuote, string error)
    {
        Validate("quote", $$"""{ "quote": "{{jsonQuote}}", "attribution": "a" }""").Error.ShouldBe(error);
    }

    [Test]
    public void Validate_WhenCombiningMarksPadOneCharacter_ShouldCapTheRawLength()
    {
        var padded = "a" + string.Concat(Enumerable.Repeat("\\u0301", 5_000));

        var result = Validate("quote", $$"""{ "quote": "q", "attribution": "{{padded}}" }""");

        result.Error.ShouldStartWith("quote.attribution: at most 80 characters");
    }

    [Test]
    public void Validate_WhenBulletsWrapPastTheSlide_ShouldSayTheyDoNotFit()
    {
        var items = string.Join(", ", Enumerable.Repeat($"\"{new string('x', 199)}\"", 8));

        var result = Validate("bullets", $$"""{ "headline": "h", "items": [{{items}}] }""");

        result.Error.ShouldStartWith("bullets.items: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenEightOneLineBullets_ShouldFit()
    {
        var items = string.Join(", ", Enumerable.Repeat($"\"{new string('x', 60)}\"", 8));

        Validate("bullets", $$"""{ "headline": "h", "items": [{{items}}] }""").IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenTableCellsWrapPastTheSlide_ShouldSayTheyDoNotFit()
    {
        var cell = $"\"{new string('x', 40)}\"";
        var row = "[" + string.Join(", ", Enumerable.Repeat(cell, 6)) + "]";
        var rows = string.Join(", ", Enumerable.Repeat(row, 6));

        var result = Validate("table", $$"""{ "headline": "h", "columns": ["a", "b", "c", "d", "e", "f"], "rows": [{{rows}}] }""");

        result.Error.ShouldStartWith("table.rows: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenTableCellsWrapInNarrowColumns_ShouldSayTheyDoNotFit()
    {
        // 16 characters is about 240 px of 27 px text, so each cell wraps in a sixth of the table.
        var row = """["the feedback arr", "change every day", "the feedback arr", "change every day", "the feedback arr", "change every day"]""";
        var rows = string.Join(", ", Enumerable.Repeat(row, 6));

        var result = Validate("table", $$"""{ "headline": "h", "columns": ["a", "b", "c", "d", "e", "f"], "rows": [{{rows}}] }""");

        result.Error.ShouldStartWith("table.rows: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenTableCellsBreakBeforeALongWord_ShouldSayTheyDoNotFit()
    {
        // 24 characters fill a quarter of the table, but the line can only break before the long word.
        var row = """["documentation infrastruc", "documentation infrastruc", "documentation infrastruc", "documentation infrastruc"]""";
        var rows = string.Join(", ", Enumerable.Repeat(row, 6));

        var result = Validate("table", $$"""{ "headline": "h", "columns": ["a", "b", "c", "d"], "rows": [{{rows}}] }""");

        result.Error.ShouldStartWith("table.rows: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenWordsWrapToMoreLinesThanTheirLength_ShouldCountTheWrappedLines()
    {
        // 28 characters would pack into 2 lines of a sixth of the table, but no two of the words share a line.
        var cell = "\"shipments deadlines releases\"";
        var row = "[" + string.Join(", ", Enumerable.Repeat(cell, 6)) + "]";
        var rows = string.Join(", ", Enumerable.Repeat(row, 4));

        var result = Validate("table", $$"""{ "headline": "h", "columns": ["a", "b", "c", "d", "e", "f"], "rows": [{{rows}}] }""");

        result.Error.ShouldStartWith("table.rows: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenTableCellsAreShortPhrases_ShouldFit()
    {
        var row = """["Under a day", "Minutes", "Flip a flag", "Same week", { "text": "Great", "tone": "info" }]""";
        var rows = string.Join(", ", Enumerable.Repeat(row, 6));

        var result = Validate("table", $$"""
            { "headline": "h", "columns": ["Slice size", "Review time", "Rollback", "Customer feedback", "Verdict"], "rows": [{{rows}}] }
            """);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenPracticeDoCardsWrapPastTheSlide_ShouldSayTheyDoNotFit()
    {
        var item = $$"""{ "lead": "{{new string('x', 50)}}", "text": "{{new string('y', 120)}}" }""";
        var doItems = string.Join(", ", Enumerable.Repeat(item, 5));

        var result = Validate("practice", $$"""{ "headline": "h", "do": [{{doItems}}], "comes_back": [], "avoid": [] }""");

        result.Error.ShouldStartWith("practice.do: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenComparePanelWrapsPastTheSlide_ShouldNameThePanel()
    {
        var points = string.Join(", ", Enumerable.Repeat($"\"{new string('x', 90)}\"", 4));
        var panel = $$"""{ "label": "l", "title": "{{new string('t', 50)}}", "sub": "{{new string('s', 100)}}", "points": [{{points}}] }""";

        var result = Validate("compare", $$"""{ "headline": "h", "failure": {{panel}}, "success": { "label": "l", "title": "t", "points": ["p"] } }""");

        result.Error.ShouldStartWith("compare.failure: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenTwoColumnItemsWrapPastTheSlide_ShouldNameTheColumn()
    {
        var items = string.Join(", ", Enumerable.Repeat($"\"{new string('x', 120)}\"", 6));

        var result = Validate("two-column", $$"""{ "headline": "h", "left": { "items": [{{items}}] }, "right": { "text": "t" } }""");

        result.Error.ShouldStartWith("two-column.left: too much text to fit on the slide");
    }

    [Test]
    public void Validate_WhenRulesOfWideLettersWrapPastTheSlide_ShouldSayTheyDoNotFit()
    {
        // 108 characters of text wrap to two lines at 0.56 em, so four such rules come to about 635 px.
        const string TITLE = "Many Women Who Wander Wide Meadows Mow Moss With Me When Warm";
        const string TEXT = "Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays When Many Women Wander Wide Meadows Mow";
        var rule = $$"""{ "title": "{{TITLE[..60]}}", "text": "{{TEXT}}" }""";
        var rules = string.Join(", ", Enumerable.Repeat(rule, 4));

        var result = Validate("rules", $$"""{ "headline": "h", "rules": [{{rules}}] }""");

        result.Error.ShouldStartWith("rules.rules: too much text to fit on the slide (about 635 px of 620 px)");
    }

    [Test]
    public void Validate_WhenFourRulesOfOneLineProse_ShouldFit()
    {
        // A 60-character title and 100 characters of text each take one line, so every rule is the
        // numeral's 127.5 px: 4 × 127.5 + 3 × 34 = 612 px.
        const string TITLE = "The team ships a small slice of work to customers every week";
        const string TEXT = "Feedback arrives in days rather than quarters, so the risk shows up early while it is cheap to change";
        var rule = $$"""{ "title": "{{TITLE}}", "text": "{{TEXT}}" }""";
        var rules = string.Join(", ", Enumerable.Repeat(rule, 4));

        Validate("rules", $$"""{ "headline": "h", "rules": [{{rules}}] }""").IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validate_WhenCloseHeadlineWrapsToFiveLines_ShouldSayItDoesNotFit()
    {
        var result = Validate("close", $$"""{ "headline": "{{new string('x', 50)}}\n{{new string('y', 25)}}" }""");

        result.Error.ShouldBe("close.headline: too long to fit: it wraps to about 5 lines at the close slide's size, and at most 4 fit");
    }

    [Test]
    public void Validate_WhenUnknownFieldNameIsHuge_ShouldEchoAShortEscapedName()
    {
        var name = "line\\none" + new string('k', 1_000_000);

        var result = Validate("quote", $$"""{ "quote": "q", "attribution": "a", "{{name}}": 1 }""");

        result.Error.ShouldBe("quote.line\\none" + new string('k', 32) + "…: unknown field");
    }

    [Test]
    public void Validate_WhenEnumValueIsHuge_ShouldEchoAShortEscapedValue()
    {
        var value = "\\u0000" + new string('v', 1_000_000);

        var result = Validate("bullets", $$"""{ "headline": "h", "items": ["i"], "callout": { "text": "t", "tone": "{{value}}" } }""");

        result.Error.ShouldBe("bullets.callout.tone: expected one of 'accent', 'positive', 'negative', 'inverse' (got '\\u0000" + new string('v', 39) + "…')");
    }

    [Test]
    public void Validate_WhenUnknownLayoutIsHuge_ShouldEchoAShortName()
    {
        var result = SlideValidator.Validate(new string('x', 1_000_000), default);

        result.Error.ShouldStartWith("layout: unknown layout '" + new string('x', 40) + "…' (known: title,");
    }

    [TestCase("accent", null)]
    [TestCase("positive", "soft")]
    [TestCase("negative", "solid")]
    [TestCase("inverse", "solid")]
    [TestCase("good", null)]
    [TestCase("bad", "soft")]
    [TestCase("info", "solid")]
    public void Validate_WhenCalloutToneAliasesAndFill_ShouldPass(string tone, string? fill)
    {
        var fillField = fill is null ? "" : $$""", "fill": "{{fill}}" """;

        var result = Validate("bullets", $$"""{ "headline": "h", "items": ["i"], "callout": { "text": "t", "tone": "{{tone}}"{{fillField}} } }""");

        result.Errors.ShouldBeEmpty();
    }

    [TestCase("good", "positive")]
    [TestCase("bad", "negative")]
    [TestCase("info", "accent")]
    [TestCase("accent", "accent")]
    [TestCase("inverse", "inverse")]
    public void CalloutToneAliasesAndFill_WhenAlias_ShouldResolveToTheNewTone(string stored, string resolved)
    {
        var tone = (EnumSlot)((ObjectSlot)LayoutCatalogue.Callout.Type!).Fields.Single(f => f.Name == "tone").Type;

        tone.Resolve(stored).ShouldBe(resolved);
    }

    [Test]
    public void CalloutToneAliasesAndFill_WhenFillUnknown_ShouldNameTheChoices()
    {
        var result = Validate("practice", """
            { "headline": "h", "do": [{ "lead": "a", "text": "b" }], "comes_back": [], "avoid": [],
              "callout": { "text": "t", "tone": "good", "fill": "outline" } }
            """);

        result.Error.ShouldBe("practice.callout.fill: expected one of 'soft', 'solid' (got 'outline')");
    }

    [Test]
    public void CalloutToneAliasesAndFill_WhenToneMissing_ShouldStillBeRequired()
    {
        var result = Validate("bullets", """{ "headline": "h", "items": ["i"], "callout": { "text": "t", "fill": "solid" } }""");

        result.Error.ShouldBe("bullets.callout.tone: required");
    }

    [Test]
    public void Validate_WhenCardsAndCompareToneAndFill_ShouldPass()
    {
        var cards = Validate("cards", """
            { "headline": "h", "cards": [
              { "title": "a", "text": "b", "chips": [] },
              { "title": "c", "text": "d", "chips": [], "tone": "negative", "fill": "solid" },
              { "title": "e", "text": "f", "chips": [], "tone": "good" } ] }
            """);
        var compare = Validate("compare", """
            { "headline": "h",
              "failure": { "label": "l", "title": "t", "points": ["p"], "tone": "inverse", "fill": "solid" },
              "success": { "label": "l", "title": "t", "points": ["p"] } }
            """);

        cards.Errors.ShouldBeEmpty();
        compare.Errors.ShouldBeEmpty();
    }

    [TestCase("cards", """{ "headline": "h", "cards": [{ "title": "a", "text": "b", "chips": [], "tone": "warn" }] }""", "cards.cards[0].tone")]
    [TestCase("compare", """{ "headline": "h", "failure": { "label": "l", "title": "t", "points": ["p"] }, "success": { "label": "l", "title": "t", "points": ["p"], "tone": "warn" } }""", "compare.success.tone")]
    public void CardsAndCompareToneAndFill_WhenToneUnknown_ShouldGiveTheUsualEnumError(string layout, string slots, string path)
    {
        var result = Validate(layout, slots);

        result.Error.ShouldBe($"{path}: expected one of 'accent', 'positive', 'negative', 'inverse' (got 'warn')");
    }

    [Test]
    public void CardsAndCompareToneAndFill_WhenComparePanelToneLeftOut_ShouldDefaultToNegativeAndPositiveSoft()
    {
        var compare = LayoutCatalogue.Find("compare")!;
        EnumSlot Slot(string panel, string field) =>
            (EnumSlot)((ObjectSlot)compare.Slots.Fields.Single(s => s.Name == panel).Type).Fields.Single(f => f.Name == field).Type;

        Slot("failure", "tone").Default.ShouldBe("negative");
        Slot("success", "tone").Default.ShouldBe("positive");
        Slot("failure", "fill").Default.ShouldBe("soft");
        Slot("success", "fill").Default.ShouldBe("soft");
    }

    private static SlideValidationResult Validate(string layout, string slots)
    {
        using var document = JsonDocument.Parse(slots);
        return SlideValidator.Validate(layout, document.RootElement);
    }
}
