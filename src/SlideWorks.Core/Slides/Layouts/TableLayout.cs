using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class TableLayout
{
    public static LayoutDefinition Definition { get; } = new("table", "A table of 2–6 columns and 1–6 rows. A cell can carry a tone, and one row can be highlighted.", false,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("columns", new ListSlot(new TextSlot(30), 2, 6)),
        Req("rows", new ListSlot(new ListSlot(TableCell(), 2, 6), 1, 6), "Each row has one cell per column"),
        Opt("highlightRow", new NumberSlot(1, 6, true), "The 1-based number of the row to highlight"),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        var columns = slots.GetProperty("columns").GetArrayLength();
        var rows = slots.GetProperty("rows");
        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var cells = row.GetArrayLength();
            if (cells != columns)
            {
                validation.Fail($"{path}.rows[{index}]", $"{cells} cells, but there are {columns} columns");
            }

            index++;
        }

        if (ObjectSlot.IsPresent(slots, "highlightRow", out var highlight) && highlight.GetDouble() > rows.GetArrayLength())
        {
            validation.Fail($"{path}.highlightRow", $"at most {rows.GetArrayLength()} (the number of rows)");
        }

        if (validation.ErrorCount > 0)
        {
            return;
        }

        // table.st: 1680 px across the columns (taken as an even split), 30 px cell padding each side,
        // 27 px/1.3 cells with 22 px top and bottom padding and a 1 px top border; the header is 17 px/1
        // uppercase with letter-spacing in the same padding.
        var cellWidth = (1680.0 / columns) - 60;
        var header = slots.GetProperty("columns").EnumerateArray().Max(c => SlideFit.Lines(c.GetString()!, cellWidth, 17, 0.75)) * 17;
        var body = rows.EnumerateArray().Sum(row => 45 + (row.EnumerateArray().Max(c => SlideFit.Lines(SlideFit.Words(c), cellWidth, 27, validation.Widths.Cell)) * 35.1));
        SlideFit.Check(44 + header + body, $"{path}.rows", validation);
    }
}
