using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides;

// The 7 inline blocks (K2, D8, K15). Names and fields are frozen in the plan's section 4.
public static class InlineBlocks
{
    // A badge may wrap to two lines (K17).
    public const int BADGE = 36;

    public static readonly InlineBlock Callout = new(
        "callout",
        "A highlighted sentence with a tone (accent, positive, negative or inverse) and a soft or solid fill.",
        Obj(Req("tone", Tone(null), TONE), Opt("fill", Fill(), FILL), Req("text", new TextSlot(220)), Opt("reveal", CalloutReveal(), CALLOUT_REVEAL)));

    public static readonly InlineBlock Chip = new("chip", "A small rounded tag, such as a tool or a practice.", new TextSlot(24));

    public static readonly InlineBlock Badge = new("badge", "A short label at the top of a card, such as a role.", new TextSlot(BADGE));

    public static readonly InlineBlock Stamp = new("stamp", "A tilted stamp on the title slide, such as 'Draft'. Also used on compare panels.", new TextSlot(40));

    public static readonly InlineBlock Journey = new(
        "journey",
        "The parts of the deck as a timeline on divider slides. Rendered from the deck's parts; agents never set it.",
        null);

    public static readonly InlineBlock Levels = new(
        "levels",
        "A ladder of 2–7 levels, top to bottom, with any of them marked as current.",
        Obj(
            Opt("title", new TextSlot(30), "A small heading over the ladder"),
            Req(
                "items",
                new ListSlot(
                    Obj(
                        Req("text", new TextSlot(50)),
                        Opt("mark", new TextSlot(3), "In the rung's circle: a number or a letter. Left out, the circle is empty"),
                        Opt("state", new EnumSlot("plain", "current") { Default = "plain" }, "Any number of rungs can be current")),
                    2,
                    7),
                "Top rung first")));

    public static readonly InlineBlock Facts = new(
        "facts",
        "A row of 2–4 fact tiles, each a label and a value.",
        new ListSlot(
            Obj(
                Req("label", new TextSlot(20), "Small capitals"),
                Req("value", new TextSlot(28)),
                Opt("tone", Tone(null), "Colours the tile's top rule")),
            2,
            4));

    public static IReadOnlyList<InlineBlock> All { get; } = [Callout, Chip, Badge, Stamp, Journey, Levels, Facts];
}
