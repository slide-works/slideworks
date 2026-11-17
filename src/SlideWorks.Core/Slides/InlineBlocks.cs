using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides;

// The 5 inline blocks (K2, D8). Names and fields are frozen in the plan's section 4.
public static class InlineBlocks
{
    public static readonly InlineBlock Callout = new(
        "callout",
        "A highlighted sentence with a tone (accent, positive, negative or inverse) and a soft or solid fill.",
        Obj(Req("tone", Tone(null), TONE), Opt("fill", Fill(), FILL), Req("text", new TextSlot(160))));

    public static readonly InlineBlock Chip = new("chip", "A small rounded tag, such as a tool or a practice.", new TextSlot(24));

    public static readonly InlineBlock Badge = new("badge", "A short label at the top of a card, such as a role.", new TextSlot(20));

    public static readonly InlineBlock Stamp = new("stamp", "A tilted stamp on the title slide, such as 'Draft'.", new TextSlot(16));

    public static readonly InlineBlock Journey = new(
        "journey",
        "The parts of the deck as a timeline on divider slides. Rendered from the deck's parts; agents never set it.",
        null);

    public static IReadOnlyList<InlineBlock> All { get; } = [Callout, Chip, Badge, Stamp, Journey];
}
