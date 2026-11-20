using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

// A headline over one YouTube video. The player shows a poster until play, then the video, and a link
// that opens the same video on YouTube.
public static class VideoLayout
{
    public static LayoutDefinition Definition { get; } = new(
        "video",
        "A headline over one YouTube video. The slide plays the video, and a link opens it on YouTube.",
        LayoutSurface.Light,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Emphasis("headline"),
            Req("url", new YouTubeSlot(), "A YouTube watch, youtu.be, embed, shorts or live link. The slide plays this video"),
            Opt("start", new NumberSlot(0, YouTubeVideo.MAX_START_SECONDS, true), "Seconds from the start. When set, it overrides a time in the link"),
            Opt("play", new EnumSlot("click", "auto") { Default = "click" }, "'click' (the default) plays when someone presses the video; 'auto' plays when the slide is shown"),
            Opt("caption", new TextSlot(140)),
        ],
        rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation) =>
        EmphasisRules.Check(slots, path, validation, "headline");
}
