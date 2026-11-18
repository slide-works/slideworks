using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class FlowLayout
{
    // TODO: slots come with the layout.
    public static LayoutDefinition Definition { get; } = new("flow", "A headline over the steps of a flow, joined by arrows.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
    ]);
}
