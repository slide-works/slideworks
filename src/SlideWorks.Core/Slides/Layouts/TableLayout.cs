using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class TableLayout
{
    public static LayoutDefinition Definition { get; } = new("table", "A table of 2–6 columns and 1–6 rows. A cell can carry a tone, and one row can be highlighted. The rows can arrive one per step, or a two-column table can strike each left cell as its answer arrives.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("columns", new ListSlot(new TextSlot(30), 2, 6)),
        Req("rows", new ListSlot(new ListSlot(TableCell(80), 2, 6), 1, 6), "Each row has one cell per column"),
        Opt("highlightRow", new NumberSlot(1, 6, true), "The 1-based number of the row to highlight"),
        Opt("reveal", new EnumSlot("all", "one-by-one", "strike-and-answer"), "'one-by-one' shows one row per step; 'strike-and-answer' (two columns) shows every left cell from the start and, one row per step, strikes it and shows its answer; 'all' (the default) shows everything at once"),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("rows"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var columns = slots.GetProperty("columns").GetArrayLength();
        if (slots.TryGetProperty("reveal", out var reveal) && reveal.ValueKind == JsonValueKind.String && reveal.GetString() == "strike-and-answer" && columns != 2)
        {
            validation.Fail($"{path}.reveal", $"'strike-and-answer' needs exactly 2 columns (got {columns})");
        }

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
        var callout = ObjectSlot.IsPresent(slots, "callout", out var block) ? LayoutRules.CalloutHeight(block, 1680, validation.Widths) : 0;
        SlideFit.Check(44 + header + body + callout, $"{path}.rows", validation);
    }
}
