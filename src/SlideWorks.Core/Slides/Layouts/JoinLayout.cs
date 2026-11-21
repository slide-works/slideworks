using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class JoinLayout
{
    public const string NAME = "join";

    public static LayoutDefinition Definition { get; } = new(NAME, "Join slide (dark): a QR code, the join code and the address, so the room can follow the talk on their phones. The code and the QR come from the live session, never from the slots.", LayoutSurface.Dark,
    [
        Opt("kicker", new TextSlot(KICKER), "Defaults to 'Join on your phone'"),
        Opt("count", new EnumSlot("shown", "hidden") { Default = "shown" }, "'shown' (the default) shows how many have joined; 'hidden' leaves the count off"),
    ]);
}
