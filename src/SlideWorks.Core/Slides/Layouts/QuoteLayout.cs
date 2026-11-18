using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class QuoteLayout
{
    public static LayoutDefinition Definition { get; } = new("quote", "A quotation with its attribution.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("quote", new TextSlot(280), "The words only; the layout adds the quotation marks"),
        Req("attribution", new TextSlot(80)),
    ]);
}
