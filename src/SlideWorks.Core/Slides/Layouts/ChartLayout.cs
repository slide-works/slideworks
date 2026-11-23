using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class ChartLayout
{
    // The client's ChartLayout.tsx and .module.css: the body is 620 px tall, an aside is 480 px wide with 28 px
    // padding in its callout (408 px of text), a 28 px gap between the legend and the caption callout. The
    // legend's names are 22 px/1.25 (27.5 px lines) in 440 px (40 px of swatch), 12 px apart. The caption
    // callout is 28 px/1.35 bold (37.8 px lines) in 56 px of padding; a callout of its own takes the room it
    // needs from the 620 px, and the plot shrinks.
    private const double BODY_HEIGHT = 620;
    private const double BODY_WIDTH = 1680;
    private const double ASIDE_TEXT = 408;
    private const double ASIDE_GAP = 28;
    private const double CALLOUT_PAD = 56;
    private const double CALLOUT_FONT = 28;
    private const double CALLOUT_LINE = 37.8;
    private const double LEGEND_TEXT = 440;
    private const double LEGEND_FONT = 22;
    private const double LEGEND_LINE = 27.5;
    private const double LEGEND_GAP = 12;

    public static LayoutDefinition Definition { get; } = new("chart", "A line or bar chart of 1–3 series, with an optional marker line and caption.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("kind", new EnumSlot("line", "bar")),
        Req("xLabels", new ListSlot(new TextSlot(12), 2, 12)),
        Req("series", new ListSlot(Obj(
            Req("name", new TextSlot(30)),
            Req("values", new ListSlot(new NumberSlot(-1_000_000_000, 1_000_000_000, false), 2, 12), "One number per xLabel")), 1, 3)),
        Opt("yUnit", new TextSlot(8), "A short unit for the y axis, such as 'wk' or '%'"),
        Opt("caption", new TextSlot(160)),
        Opt("callout", new BlockSlot(InlineBlocks.Callout), "Under the chart; the plot shrinks to make room"),
        Opt("marker", Obj(
            Req("x", new NumberSlot(1, 12, false), "1 is the first xLabel; 3.5 sits between the third and the fourth"),
            Req("label", new TextSlot(20))), "A dashed vertical line with a label"),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var labels = slots.GetProperty("xLabels").GetArrayLength();
        var index = 0;
        foreach (var series in slots.GetProperty("series").EnumerateArray())
        {
            var values = series.GetProperty("values").GetArrayLength();
            if (values != labels)
            {
                validation.Fail($"{path}.series[{index}].values", $"{values} values, but there are {labels} xLabels");
            }

            index++;
        }

        if (ObjectSlot.IsPresent(slots, "marker", out var marker) && marker.GetProperty("x").GetDouble() > labels)
        {
            validation.Fail($"{path}.marker.x", $"at most {labels} (the number of xLabels)");
        }

        CaptionRule(slots, path, validation);
    }

    // The caption sits in the aside under the legend; it is refused when that column is taller than the body.
    // A separate rule and not SlideFit.Check, which would make the chart fit-controlled.
    private static void CaptionRule(JsonElement slots, string path, SlotValidation validation)
    {
        if (!ObjectSlot.IsPresent(slots, "caption", out var caption))
        {
            return;
        }

        var series = slots.GetProperty("series").EnumerateArray().ToArray();
        var height = CALLOUT_PAD + (SlideFit.Lines(caption.GetString()!, ASIDE_TEXT, CALLOUT_FONT, validation.Widths.Head) * CALLOUT_LINE);
        if (series.Length > 1)
        {
            height += ASIDE_GAP + (LEGEND_GAP * (series.Length - 1));
            height += series.Sum(s => SlideFit.Lines(s.GetProperty("name").GetString()!, LEGEND_TEXT, LEGEND_FONT, validation.Widths.Body) * LEGEND_LINE);
        }

        var room = BODY_HEIGHT;
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            room -= LayoutRules.CalloutHeight(callout, BODY_WIDTH, validation.Widths);
        }

        if (height > room)
        {
            validation.Fail($"{path}.caption", "too much text to fit beside the chart; shorten the caption");
        }
    }
}
