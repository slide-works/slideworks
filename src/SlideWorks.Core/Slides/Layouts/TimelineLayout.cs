using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

// Events down the slide, each a short when and a line of what happens.
public static class TimelineLayout
{
    public static LayoutDefinition Definition { get; } = new(
        "timeline",
        "2–6 events down the slide, each a short when and what happens then.",
        LayoutSurface.Light,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Emphasis("headline"),
            Req("events", new ListSlot(Obj(
                Req("when", new TextSlot(24)),
                Req("text", new TextSlot(140))), 2, 6)),
        ],
        rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var events = slots.GetProperty("events").EnumerateArray().ToArray();
        var parts = new List<FitPart>(events.Length);
        var height = 0.0;
        for (var index = 0; index < events.Length; index++)
        {
            var item = events[index];
            var itemHeight = 28
                + (SlideFit.Lines(item.GetProperty("when").GetString()!, 240, 18, validation.Widths.Head) * 24)
                + (SlideFit.Lines(item.GetProperty("text").GetString()!, 1400, 24, validation.Widths.Body) * 32);
            parts.Add(new FitPart($"events[{index}]", itemHeight));
            height += itemHeight;
        }

        SlideFit.Check(height, $"{path}.events", validation, parts: parts, shares: ["events"]);
    }
}
