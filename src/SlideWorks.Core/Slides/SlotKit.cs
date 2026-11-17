namespace SlideWorks.Core.Slides;

// The slot pieces more than one layout uses, so each layout file reads like the frozen table in the
// plan's section 4. The limits come from the mockup CSS.
public static class SlotKit
{
    public const int KICKER = 40;
    public const int HEADLINE = 80;

    public const string REVEAL = "'one-by-one' shows one item per step; 'all' (the default) shows them all at once";

    public const string TONE = "Colours from the deck's palette";
    public const string FILL = "'soft' (the default) tints it; 'solid' fills it with the tone's colour";

    public static SlotField Req(string name, ISlotType type, string? description = null) => new(name, type, true, description);

    public static SlotField Opt(string name, ISlotType type, string? description = null) => new(name, type, false, description);

    public static ObjectSlot Obj(params SlotField[] fields) => new(fields);

    public static EnumSlot Reveal() => new("all", "one-by-one");

    public static TextOrObjectSlot LeadOrText(int text, int leadInObject, int textInObject) =>
        new(new TextSlot(text), Obj(Req("lead", new TextSlot(leadInObject)), Req("text", new TextSlot(textInObject))));

    public static ObjectSlot Column() => new(
        [
            Opt("title", new TextSlot(50)),
            Opt("items", new ListSlot(LeadOrText(120, 40, 120), 1, 6)),
            Opt("text", new TextSlot(400)),
            Opt("image", new ImageSlot()),
        ],
        exactlyOneOf: ["items", "text", "image"]);

    public static ObjectSlot ComparePanel(string defaultTone) => Obj(
        Req("label", new TextSlot(24), "The panel's tag, such as 'Failure mode'"),
        Req("title", new TextSlot(50)),
        Opt("sub", new TextSlot(100)),
        Req("points", new ListSlot(new TextSlot(90), 1, 4)),
        Opt("tone", Tone(defaultTone), $"{TONE}; defaults to '{defaultTone}'"),
        Opt("fill", Fill(), FILL));

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

    public static TextOrObjectSlot TableCell() => new(
        new TextSlot(40),
        Obj(Req("text", new TextSlot(40)), Req("tone", new EnumSlot("good", "warn", "bad", "info"), "Draws the cell as a badge in this tone")));
}
