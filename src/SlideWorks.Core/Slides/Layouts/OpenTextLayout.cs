using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class OpenTextLayout
{
    public static LayoutDefinition Definition { get; } = new("open-text", "Open-text slide: a question the room answers on their phones with one short answer each, shown as a wall while the deck is live. The answers come from the live session, never from the slots.", LayoutSurface.Light,
    [
        Req("question", new TextSlot(120)),
    ]);
}
