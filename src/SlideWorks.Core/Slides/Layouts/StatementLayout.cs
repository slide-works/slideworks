using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class StatementLayout
{
    // TODO: slots come with the layout.
    public static LayoutDefinition Definition { get; } = new("statement", "One sentence set large on a light or dark slide, with no tracker.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("statement", new TextSlot(120)),
    ], tracker: false);
}
