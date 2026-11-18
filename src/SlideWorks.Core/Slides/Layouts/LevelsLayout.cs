using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class LevelsLayout
{
    // TODO: slots come with the layout.
    public static LayoutDefinition Definition { get; } = new("levels", "A headline over levels that rise step by step.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
    ]);
}
