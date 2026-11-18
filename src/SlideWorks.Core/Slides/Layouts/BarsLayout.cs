using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class BarsLayout
{
    // TODO: slots come with the layout.
    public static LayoutDefinition Definition { get; } = new("bars", "A headline over labelled horizontal bars.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
    ]);
}
