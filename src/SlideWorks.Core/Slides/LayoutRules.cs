using System.Text.Json;

namespace SlideWorks.Core.Slides;

// Checks across fields that a per-field schema can't express: counts that must agree, and whether the
// text fits the slide (SlideFit; the pixel figures are the mockup CSS). They run only once the slots
// match the schema, so every field they read is there and has the right kind.
internal static class LayoutRules
{
    public const int CLOSE_HEADLINE_MAX_LINES = 3;

    // close h2: 112 px/1.02 in a 1400 px column, so a written line wraps after about 22 characters.
    public const int CLOSE_HEADLINE_MAX_SHOWN_LINES = 4;

    public static void Bullets(JsonElement slots, string path, SlotValidation validation)
    {
        // ul.bl: 36 px/1.35 text, 52 px indent in a 1500 px list, 30 px between items.
        var items = slots.GetProperty("items").EnumerateArray().ToArray();
        var height = items.Sum(i => SlideFit.Lines(SlideFit.Words(i), 1448, 36, SlideFit.BODY_EM) * 48.6) + ((items.Length - 1) * 30);
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            height += CalloutHeight(callout, 1680);
        }

        SlideFit.Check(height, $"{path}.items", validation);
    }

    public static void TwoColumn(JsonElement slots, string path, SlotValidation validation)
    {
        // .two: 1fr 1.05fr with a 64 px gap, so the narrower column is 788 px. Images scale to fit.
        foreach (var side in new[] { "left", "right" })
        {
            var column = slots.GetProperty(side);
            var height = 0.0;
            if (ObjectSlot.IsPresent(column, "title", out var title))
            {
                height += (SlideFit.Lines(title.GetString()!, 788, 34, SlideFit.HEAD_EM) * 40.8) + 16;
            }

            if (ObjectSlot.IsPresent(column, "items", out var items))
            {
                var list = items.EnumerateArray().ToArray();
                height += list.Sum(i => SlideFit.Lines(SlideFit.Words(i), 736, 30, SlideFit.BODY_EM) * 40.5) + ((list.Length - 1) * 30);
            }

            if (ObjectSlot.IsPresent(column, "text", out var text))
            {
                height += SlideFit.Lines(text.GetString()!, 788, 28, SlideFit.BODY_EM) * 39.2;
            }

            SlideFit.Check(height, $"{path}.{side}", validation);
        }
    }

    public static void Practice(JsonElement slots, string path, SlotValidation validation)
    {
        // .practice: 1.15fr 1fr with a 48 px gap (873 and 759 px). Each column has an 18 px header
        // (34 px with its margin); an .it card is 44 px of padding plus 25 px/1.4 text, 14 px apart.
        var doCards = slots.GetProperty("do").EnumerateArray()
            .Sum(i => 58 + (SlideFit.Lines(SlideFit.Words(i), 821, 25, SlideFit.BODY_EM) * 35));
        SlideFit.Check(34 + doCards, $"{path}.do", validation);

        var right = 0.0;
        foreach (var list in new[] { "comes_back", "avoid" }.Select(name => slots.GetProperty(name)).Where(l => l.GetArrayLength() > 0))
        {
            right += (right > 0 ? 26 : 0) + 34
                + list.EnumerateArray().Sum(i => 58 + (SlideFit.Lines(i.GetString()!, 699, 25, SlideFit.BODY_EM) * 35));
        }

        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            right += CalloutHeight(callout, 759) - 8;
        }

        SlideFit.Check(right, $"{path}.comes_back", validation);
    }

    public static void Compare(JsonElement slots, string path, SlotValidation validation)
    {
        // .panel: 820 px wide, 44/48 px padding, 10 px top border, an 18 px tag, a 44 px/1.1 title and
        // 27 px/1.4 points 16 px apart in a 694 px list.
        foreach (var side in new[] { "failure", "success" })
        {
            var panel = slots.GetProperty(side);
            var height = 160 + (SlideFit.Lines(panel.GetProperty("title").GetString()!, 724, 44, SlideFit.HEAD_EM) * 48.4);
            if (ObjectSlot.IsPresent(panel, "sub", out var sub))
            {
                height += 16 + (SlideFit.Lines(sub.GetString()!, 724, 27, SlideFit.BODY_EM) * 37.8);
            }

            var points = panel.GetProperty("points").EnumerateArray().ToArray();
            height += points.Sum(p => SlideFit.Lines(p.GetString()!, 694, 27, SlideFit.BODY_EM) * 37.8) + ((points.Length - 1) * 16);
            SlideFit.Check(height, $"{path}.{side}", validation);
        }
    }

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

        if (validation.ErrorCount > 0)
        {
            return;
        }

        // table.st: 1680 px across the columns (taken as an even split), 30 px cell padding each side,
        // 27 px/1.3 cells with 22 px top and bottom padding and a 1 px top border; the header is 17 px/1
        // uppercase with letter-spacing in the same padding.
        var cellWidth = (1680.0 / columns) - 60;
        var header = slots.GetProperty("columns").EnumerateArray().Max(c => SlideFit.Lines(c.GetString()!, cellWidth, 17, 0.75)) * 17;
        var body = rows.EnumerateArray().Sum(row => 45 + (row.EnumerateArray().Max(c => SlideFit.Lines(SlideFit.Words(c), cellWidth, 27, SlideFit.CELL_EM)) * 35.1));
        SlideFit.Check(44 + header + body, $"{path}.rows", validation);
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
        var written = slots.GetProperty("headline").GetString()!.Split('\n');
        if (written.Length > CLOSE_HEADLINE_MAX_LINES)
        {
            validation.Fail($"{path}.headline", $"at most {CLOSE_HEADLINE_MAX_LINES} lines (got {written.Length})");
            return;
        }

        var shown = written.Sum(line => SlideFit.Lines(line, 1400, 112, SlideFit.HEAD_EM));
        if (shown > CLOSE_HEADLINE_MAX_SHOWN_LINES)
        {
            validation.Fail($"{path}.headline", $"too long to fit: it wraps to about {shown} lines at the close slide's size, and at most {CLOSE_HEADLINE_MAX_SHOWN_LINES} fit");
        }
    }

    // .callout: 28 px/1.35 bold text in 28/36 px padding, plus the 22–30 px gap above it.
    private static double CalloutHeight(JsonElement callout, double width) =>
        30 + 56 + (SlideFit.Lines(callout.GetProperty("text").GetString()!, width - 72, 28, SlideFit.HEAD_EM) * 37.8);
}
