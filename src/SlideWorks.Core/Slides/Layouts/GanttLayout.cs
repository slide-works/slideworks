using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class GanttLayout
{
    public static LayoutDefinition Definition { get; } = new("gantt", "A timeline of 1–5 rows across 2–8 columns. Each bar has a status: done, slips, stopped or ship.", false,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("columns", new ListSlot(new TextSlot(16), 2, 8)),
        Req("rows", new ListSlot(Obj(
            Req("label", new TextSlot(30)),
            Req("start", new NumberSlot(1, 8, true), "The 1-based column the bar starts in"),
            Req("end", new NumberSlot(1, 8, true), "The 1-based column the bar ends in (inclusive)"),
            Req("status", new EnumSlot("done", "slips", "stopped", "ship"), "'ship' draws a ship marker instead of a bar"),
            Opt("note", new TextSlot(40), "Text on the bar; defaults to the status")), 1, 5)),
        Opt("caption", new TextSlot(140)),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        var columns = slots.GetProperty("columns").GetArrayLength();
        var index = 0;
        foreach (var row in slots.GetProperty("rows").EnumerateArray())
        {
            var start = row.GetProperty("start").GetDouble();
            var end = row.GetProperty("end").GetDouble();
            if (end < start)
            {
                validation.Fail($"{path}.rows[{index}].end", "must not be before start");
            }
            else if (end > columns)
            {
                validation.Fail($"{path}.rows[{index}].end", $"at most {columns} (the number of columns)");
            }

            index++;
        }
    }
}
