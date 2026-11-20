using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class DividerLayout
{
    public static LayoutDefinition Definition { get; } = new("divider", "Section divider (dark) that opens a part. Its number and the journey come from the deck's parts and the slide's part. When the template lists deep dives on dividers (workshop does), the divider lists that part's deep dives.", LayoutSurface.Dark,
    [
        Req("title", new TextSlot(44), "Set at 104 px: two lines at most, shrinking to 64 px to keep them"),
        Opt("sub", new TextSlot(110)),
    ]);
}
