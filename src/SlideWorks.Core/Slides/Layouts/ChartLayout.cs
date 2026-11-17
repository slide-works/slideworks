using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class ChartLayout
{
    public static LayoutDefinition Definition { get; } = new("chart", "A line or bar chart of 1–3 series, with an optional marker line and caption.", false,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("kind", new EnumSlot("line", "bar")),
        Req("xLabels", new ListSlot(new TextSlot(12), 2, 12)),
        Req("series", new ListSlot(Obj(
            Req("name", new TextSlot(30)),
            Req("values", new ListSlot(new NumberSlot(-1_000_000_000, 1_000_000_000, false), 2, 12), "One number per xLabel")), 1, 3)),
        Opt("yUnit", new TextSlot(8), "A short unit for the y axis, such as 'wk' or '%'"),
        Opt("caption", new TextSlot(160)),
        Opt("marker", Obj(
            Req("x", new NumberSlot(1, 12, false), "1 is the first xLabel; 3.5 sits between the third and the fourth"),
            Req("label", new TextSlot(20))), "A dashed vertical line with a label"),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
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
    }
}
