using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class BigNumberLayout
{
    public static LayoutDefinition Definition { get; } = new("big-number", "One number that carries the slide, with a caption, a detail and a source.", false,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("value", new TextSlot(6), "The number with a short unit, such as '11 wks' or '42%'; the layout sets a unit smaller than the number"),
        Req("caption", new TextSlot(120)),
        Opt("detail", new TextSlot(200)),
        Opt("source", new TextSlot(100), "Where the number comes from, without a 'Source:' prefix"),
    ]);
}
