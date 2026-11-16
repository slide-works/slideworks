using System.Text.Json;

namespace SlideWorks.Core.Slides;

// Checks across fields that a per-field schema can't express. They run only once the slots match
// the schema, so every field they read is there and has the right kind.
internal static class LayoutRules
{
    public const int CLOSE_HEADLINE_MAX_LINES = 3;

    public static void Table(JsonElement slots, string path, SlotValidation validation)
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
    }

    public static void Chart(JsonElement slots, string path, SlotValidation validation)
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

    public static void Gantt(JsonElement slots, string path, SlotValidation validation)
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

    public static void Close(JsonElement slots, string path, SlotValidation validation)
    {
        var lines = slots.GetProperty("headline").GetString()!.Split('\n').Length;
        if (lines > CLOSE_HEADLINE_MAX_LINES)
        {
            validation.Fail($"{path}.headline", $"at most {CLOSE_HEADLINE_MAX_LINES} lines (got {lines})");
        }
    }
}
