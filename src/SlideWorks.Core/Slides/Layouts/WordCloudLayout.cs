using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class WordCloudLayout
{
    public static LayoutDefinition Definition { get; } = new("word-cloud", "Word cloud slide: a question the room answers on their phones with up to 3 words each, while the deck is live. Words that several people send grow bigger. The words come from the live session, never from the slots.", LayoutSurface.Light,
    [
        Req("question", new TextSlot(120)),
    ]);
}
