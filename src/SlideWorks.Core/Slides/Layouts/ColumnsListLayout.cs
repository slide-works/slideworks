using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

// A checklist that is several columns of bullets, for a workshop slide that will not fit one list.
public static class ColumnsListLayout
{
    public static LayoutDefinition Definition { get; } = new(
        "columns-list",
        "3–5 columns of bullets under one headline, for a checklist that will not fit a single list.",
        LayoutSurface.Light,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Emphasis("headline"),
            Req("columns", new ListSlot(Obj(
                Req("title", new TextSlot(40)),
                Req("items", new ListSlot(new TextSlot(80), 1, 6))), 3, 5)),
        ],
        rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var columns = slots.GetProperty("columns").EnumerateArray().ToArray();
        var width = (1680.0 - (32 * (columns.Length - 1))) / columns.Length;
        var tallest = 0.0;
        var parts = new List<FitPart>(columns.Length);
        for (var index = 0; index < columns.Length; index++)
        {
            var column = columns[index];
            var height = SlideFit.Lines(column.GetProperty("title").GetString()!, width, 20, validation.Widths.Head) * 26;
            height += column.GetProperty("items").EnumerateArray()
                .Sum(item => 8 + (SlideFit.Lines(item.GetString()!, width - 16, 18, validation.Widths.Body) * 26));
            parts.Add(new FitPart($"columns[{index}]", height));
            tallest = Math.Max(tallest, height);
        }

        SlideFit.Check(tallest, $"{path}.columns", validation, parts: parts, shares: ["columns"]);
    }
}
