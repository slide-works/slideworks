using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class LinksLayout
{
    // TODO: slots come with the layout.
    public static LayoutDefinition Definition { get; } = new("links", "A headline over a short list of links.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
    ]);
}
