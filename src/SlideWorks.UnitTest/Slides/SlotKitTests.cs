using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class SlotKitTests
{
    // The schemas the kit gave before it grew, built from the slot types and not from the kit.
    private static ObjectSlot LeadAndText(int lead, int text) =>
        new([new SlotField("lead", new TextSlot(lead), true), new SlotField("text", new TextSlot(text), true)]);

    [Test]
    public void Column_WhenCalledWithoutArguments_ShouldBeAsBefore()
    {
        var before = new ObjectSlot(
            [
                new SlotField("title", new TextSlot(50), false),
                new SlotField("items", new ListSlot(new TextOrObjectSlot(new TextSlot(120), LeadAndText(40, 120)), 1, 6), false),
                new SlotField("text", new TextSlot(400), false),
                new SlotField("image", new ImageSlot(), false),
            ],
            exactlyOneOf: ["items", "text", "image"]);

        Schema(SlotKit.Column()).ShouldBe(Schema(before));
        Schema(SlotKit.Column()).ShouldNotContain("icon");
        Errors(SlotKit.Column(), """{ "title": "t", "items": [{ "text": "only text" }] }""").ShouldBe(["left.items[0].lead: required"]);
    }

    [Test]
    public void LeadOrText_WhenCalledAsToday_ShouldBeAsBefore()
    {
        var before = new TextOrObjectSlot(new TextSlot(200), LeadAndText(60, 160));

        Schema(SlotKit.LeadOrText(200, 60, 160)).ShouldBe(Schema(before));
    }

    [Test]
    public void TableCell_WhenCalledWithoutArguments_ShouldBeAsBefore()
    {
        var before = new TextOrObjectSlot(
            new TextSlot(40),
            new ObjectSlot(
            [
                new SlotField("text", new TextSlot(40), true),
                new SlotField("tone", new EnumSlot("good", "warn", "bad", "info"), true, "Draws the cell as a badge in this tone"),
            ]));

        Schema(SlotKit.TableCell()).ShouldBe(Schema(before));
        SlotKit.TableCell(60).Text.MaxLength.ShouldBe(60);
        Errors(SlotKit.TableCell(60), $$"""{ "text": "{{new string('a', 61)}}", "tone": "good" }""").ShouldBe(["left.text: at most 60 characters (got 61)"]);
    }

    [Test]
    public void Fill_WhenCalled_ShouldBeAsBefore()
    {
        SlotKit.Fill().Values.ShouldBe(["soft", "solid"]);
        SlotKit.Fill().Default.ShouldBe("soft");
    }

    [Test]
    public void PanelFill_WhenCalled_ShouldAddPlain()
    {
        SlotKit.PanelFill().Values.ShouldBe(["soft", "solid", "plain"]);
        SlotKit.PanelFill().Default.ShouldBe("soft");
    }

    [Test]
    public void Point_WhenAnItemHasOnlyText_ShouldAccept()
    {
        var point = SlotKit.Point(200, 60, 160);

        Errors(point, """{ "text": "Only the text" }""").ShouldBeEmpty();
        Errors(point, "\"A plain string\"").ShouldBeEmpty();
        Errors(point, """{ "icon": "coins", "lead": "Cost", "text": "It is cheaper" }""").ShouldBeEmpty();
    }

    [Test]
    public void Point_WhenAnItemBreaksALimit_ShouldRefuse()
    {
        var point = SlotKit.Point(200, 60, 160);

        Errors(point, """{ "icon": "coins" }""").ShouldBe(["left.text: required"]);
        Errors(point, """{ "icon": "coin", "text": "t" }""").ShouldBe(["left.icon: unknown icon 'coin'; did you mean 'coins', 'code'?"]);
        Errors(point, $$"""{ "lead": "{{new string('a', 61)}}", "text": "t" }""").ShouldBe(["left.lead: at most 60 characters (got 61)"]);
        Errors(point, $$"""{ "text": "{{new string('a', 161)}}" }""").ShouldBe(["left.text: at most 160 characters (got 161)"]);
        Errors(point, $$""" "{{new string('a', 201)}}" """).ShouldBe(["left: at most 200 characters (got 201)"]);
    }

    [Test]
    public void Column_WhenBuiltWithIcons_ShouldTakeAnIconAndIconPoints()
    {
        var column = SlotKit.Column(60, icons: true);

        Errors(column, """{ "title": "t", "icon": "rocket", "items": ["plain", { "text": "only text" }, { "icon": "flag", "text": "t" }] }""").ShouldBeEmpty();
        Errors(column, $$"""{ "title": "{{new string('a', 61)}}", "text": "t" }""").ShouldBe(["left.title: at most 60 characters (got 61)"]);
        column.Fields.Select(f => f.Name).ShouldBe(["title", "icon", "items", "text", "image"]);
        column.ExactlyOneOf.ShouldBe(["items", "text", "image"]);
    }

    [Test]
    public void ColumnIconNeedsTitle_WhenTheColumnHasAnIconAndNoTitle_ShouldRefuse()
    {
        NeedsTitle("""{ "icon": "rocket", "text": "t" }""").ShouldBe(["two-column.left.icon: needs left.title"]);
        NeedsTitle("""{ "icon": "rocket", "title": null, "text": "t" }""").ShouldBe(["two-column.left.icon: needs left.title"]);
        NeedsTitle("""{ "icon": "rocket", "title": "t", "text": "t" }""").ShouldBeEmpty();
        NeedsTitle("""{ "text": "t" }""").ShouldBeEmpty();
        NeedsTitle("\"not a column\"").ShouldBeEmpty();
    }

    [Test]
    public void Pin_WhenCalled_ShouldBeAnOptionalTextOf24()
    {
        var pin = SlotKit.Pin();

        pin.Name.ShouldBe("pin");
        pin.Required.ShouldBeFalse();
        pin.Type.ShouldBeOfType<TextSlot>().MaxLength.ShouldBe(24);
        pin.Description.ShouldBe("A short label in the card's corner, such as 'Hold on to this one'");
    }

    [Test]
    public void Emphasis_WhenCalled_ShouldBeTheOptionalEmphasisField()
    {
        var emphasis = SlotKit.Emphasis("headline", "caption");

        emphasis.Name.ShouldBe("emphasis");
        emphasis.Required.ShouldBeFalse();
        emphasis.Type.ShouldBeOfType<EmphasisSlot>().Targets.ShouldBe(["headline", "caption"]);
    }

    [Test]
    public void Emphasis_WhenPlacedOnALayout_ShouldValidateAndPublishItsSchema()
    {
        var layout = new LayoutDefinition(
            "sample",
            "d",
            LayoutSurface.Light,
            [SlotKit.Req("headline", new TextSlot(80)), SlotKit.Emphasis("headline")],
            rules: (slots, path, validation) => EmphasisRules.Check(slots, path, validation, "headline"));
        using var good = JsonDocument.Parse("""{ "headline": "Small steps", "emphasis": [{ "text": "Small", "tone": "accent" }] }""");
        using var bad = JsonDocument.Parse("""{ "headline": "Small steps", "emphasis": [{ "text": "Big", "tone": "accent" }] }""");

        var accepted = new SlotValidation();
        var refused = new SlotValidation();

        layout.Validate(good.RootElement, accepted);
        layout.Validate(bad.RootElement, refused);

        accepted.Errors.ShouldBeEmpty();
        refused.Errors.ShouldBe(["sample.emphasis[0].text: 'Big' is not in headline"]);
        layout.ToJsonSchema()["properties"]!["emphasis"]!["maxItems"]!.GetValue<int>().ShouldBe(3);
    }

    private static string Schema(ISlotType type) => type.ToJsonSchema().ToJsonString();

    private static IReadOnlyList<string> Errors(ISlotType type, string json)
    {
        using var document = JsonDocument.Parse(json);
        var validation = new SlotValidation();
        type.Validate(document.RootElement, "left", validation);
        return validation.Errors;
    }

    private static IReadOnlyList<string> NeedsTitle(string column)
    {
        using var document = JsonDocument.Parse(column);
        var validation = new SlotValidation();
        SlotKit.ColumnIconNeedsTitle(document.RootElement, "two-column.left", validation);
        return validation.Errors;
    }
}
