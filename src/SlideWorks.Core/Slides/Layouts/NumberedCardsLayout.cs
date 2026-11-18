using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class NumberedCardsLayout
{
    public static LayoutDefinition Definition { get; } = new("numbered-cards", "2–4 numbered cards, each a title and text. Can reveal one card per step.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("cards", new ListSlot(Obj(Req("title", new TextSlot(40)), Req("text", new TextSlot(140))), 2, 4)),
        Opt("reveal", Reveal(), REVEAL),
    ], steps: StepPlan.List("cards"));
}
