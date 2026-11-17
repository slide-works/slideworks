using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class QaLayout
{
    public static LayoutDefinition Definition { get; } = new("qa", "Q&A slide: the room's questions, most upvoted first, while the deck is live, and one question at full size when the presenter shows it. The questions come from the live session, never from the slots; the Q&A settings are set with update_qa.", false,
    [
        Opt("title", new TextSlot(80), "Defaults to 'Questions from the room'"),
    ]);
}
