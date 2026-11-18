using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class TitleLayout
{
    public static LayoutDefinition Definition { get; } = new("title", "Opening slide (dark): the deck's title with a kicker, subtitle, tagline, byline and an optional stamp.", LayoutSurface.Dark,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("title", new TextSlot(34), "Set at 168 px: two lines at most, shrinking to 96 px to keep them"),
        Opt("subtitle", new TextSlot(100)),
        Opt("tagline", new TextSlot(90)),
        Opt("author", new TextSlot(60), "The presenter's name"),
        Opt("affiliation", new TextSlot(60), "The presenter's team or organisation"),
        Opt("stamp", new BlockSlot(InlineBlocks.Stamp)),
    ]);
}
