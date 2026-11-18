using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class CardsLayout
{
    public static LayoutDefinition Definition { get; } = new("cards", "1–3 cards, each with an optional badge, a title, text and chips. Can reveal one card per step.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("cards", new ListSlot(Obj(
            Opt("badge", new BlockSlot(InlineBlocks.Badge)),
            Req("title", new TextSlot(50)),
            Req("text", new TextSlot(140)),
            Req("chips", new ListSlot(new BlockSlot(InlineBlocks.Chip), 0, 4), "May be empty"),
            Opt("tone", Tone(null), TONE + "; left out, the card is plain"),
            Opt("fill", Fill(), FILL)), 1, 3)),
        Opt("reveal", Reveal(), REVEAL),
    ], steps: StepPlan.List("cards"));
}
