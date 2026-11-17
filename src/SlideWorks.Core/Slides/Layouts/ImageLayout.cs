using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class ImageLayout
{
    public static LayoutDefinition Definition { get; } = new("image", "A headline over one large image, with optional points beside it and a caption.", false,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("image", new ImageSlot()),
        Opt("points", new ListSlot(new TextSlot(80), 1, 4)),
        Opt("caption", new TextSlot(140)),
    ]);
}
