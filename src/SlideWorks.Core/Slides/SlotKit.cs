using System.Text.Json;

namespace SlideWorks.Core.Slides;

// The slot pieces more than one layout uses, so each layout file reads like the frozen table in the
// plan's section 4. The limits come from the mockup CSS.
public static class SlotKit
{
    // Raised from 40 and 80: real slide copy sat just over those. The headline shrinks on the client
    // to 2 lines, down to 48 px, and a kicker must stay on one row; HeadingRules refuses both when
    // they cannot, so 100 characters of lowercase prose are accepted and 100 capitals are not.
    public const int KICKER = 60;
    public const int HEADLINE = 100;

    public const string REVEAL = "'one-by-one' shows one item per step; 'all' (the default) shows them all at once";

    public const string TONE = "Colours from the deck's palette";
    public const string FILL = "'soft' (the default) tints it; 'solid' fills it with the tone's colour";
    public const string PANEL_FILL = "'soft' (the default) tints it; 'solid' fills it with the tone's colour; 'plain' leaves it on the slide's ground";
    public const string CALLOUT_REVEAL = "'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others";
    public const string EMPHASIS = "Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in";
    public const string PIN = "A short label in the card's corner, such as 'Hold on to this one'";

    public static SlotField Req(string name, ISlotType type, string? description = null) => new(name, type, true, description);

    public static SlotField Opt(string name, ISlotType type, string? description = null) => new(name, type, false, description);

    public static ObjectSlot Obj(params SlotField[] fields) => new(fields);

    public static EnumSlot Reveal() => new("all", "one-by-one");

    public static TextOrObjectSlot LeadOrText(int text, int leadInObject, int textInObject) =>
        new(new TextSlot(text), Obj(Req("lead", new TextSlot(leadInObject)), Req("text", new TextSlot(textInObject))));

    // A list item: a plain string, or an object with an optional icon and bold lead-in. Only its text is required.
    public static TextOrObjectSlot Point(int plainMax, int leadMax, int textMax) => new(
        new TextSlot(plainMax),
        Obj(Opt("icon", new IconSlot()), Opt("lead", new TextSlot(leadMax)), Req("text", new TextSlot(textMax))));

    // A column of two-column. With icons it gains an icon beside its title, and its items may carry one each.
    public static ObjectSlot Column(int titleMax = 50, bool icons = false) => new(
        [
            Opt("title", new TextSlot(titleMax)),
            .. icons ? [Opt("icon", new IconSlot(), "Drawn beside the title, so it needs one")] : Array.Empty<SlotField>(),
            Opt("items", new ListSlot(icons ? Point(120, 40, 120) : LeadOrText(120, 40, 120), 1, 6)),
            Opt("text", new TextSlot(400)),
            Opt("image", new ImageSlot()),
        ],
        exactlyOneOf: ["items", "text", "image"]);

    // A column's icon sits beside its title, so a column with an icon and no title is refused. The path is
    // the column's ('two-column.left'); the message names the title as the layout's slots do ('left.title').
    public static void ColumnIconNeedsTitle(JsonElement column, string path, SlotValidation validation)
    {
        if (column.ValueKind == JsonValueKind.Object
            && ObjectSlot.IsPresent(column, "icon", out _)
            && !ObjectSlot.IsPresent(column, "title", out _))
        {
            validation.Fail($"{path}.icon", $"needs {path[(path.LastIndexOf('.') + 1)..]}.title");
        }
    }

    // The optional emphasis field of a layout, for the text slots named (K10). The layout's rule runs
    // EmphasisRules.Check with the same names.
    public static SlotField Emphasis(params string[] targets) => Opt(EmphasisRules.SLOT, new EmphasisSlot(targets), EMPHASIS);

    // The optional pin of a card.
    public static SlotField Pin() => Opt("pin", new TextSlot(24), PIN);

    // Callout, card and compare-panel tones (L4). Table cell tones are a separate, unchanged set.
    public static EnumSlot Tone(string? defaultTone) => new("accent", "positive", "negative", "inverse")
    {
        Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["good"] = "positive",
            ["bad"] = "negative",
            ["info"] = "accent",
        },
        Default = defaultTone,
    };

    public static EnumSlot Fill() => new("soft", "solid") { Default = "soft" };

    // A panel's fill (K13): as Fill, and 'plain' for a panel with no ground of its own.
    public static EnumSlot PanelFill() => new("soft", "solid", "plain") { Default = "soft" };

    // A callout's place in the slide's steps (K9).
    public static EnumSlot CalloutReveal() => new("with-slide", "last") { Default = "with-slide" };

    public static TextOrObjectSlot TableCell(int max = 40) => new(
        new TextSlot(max),
        Obj(Req("text", new TextSlot(max)), Req("tone", new EnumSlot("good", "warn", "bad", "info"), "Draws the cell as a badge in this tone")));
}
