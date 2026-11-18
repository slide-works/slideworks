using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class StackLayout
{
    // TODO: slots come with the layout.
    public static LayoutDefinition Definition { get; } = new("stack", "A headline over layers stacked one on another.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
    ]);
}
