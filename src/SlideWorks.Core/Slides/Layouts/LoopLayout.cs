using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

// A cycle: the last step leads back to the first. Drawn as one row of three, or two rows.
public static class LoopLayout
{
    public static LayoutDefinition Definition { get; } = new(
        "loop",
        "3–6 steps in a cycle: the last step leads back to the first.",
        LayoutSurface.Light,
        [
            Opt("kicker", new TextSlot(KICKER)),
            Req("headline", new TextSlot(HEADLINE)),
            Emphasis("headline"),
            Req("steps", new ListSlot(Obj(
                Req("label", new TextSlot(24)),
                Req("text", new TextSlot(80))), 3, 6)),
        ],
        rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var steps = slots.GetProperty("steps").EnumerateArray().ToArray();
        var columns = steps.Length <= 3 ? steps.Length : (steps.Length + 1) / 2;
        var width = (1680.0 - (28 * (columns - 1))) / columns;
        var parts = new List<FitPart>(steps.Length);
        var row = 0.0;
        var height = 0.0;
        var inRow = 0;
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            var card = 48
                + (SlideFit.Lines(step.GetProperty("label").GetString()!, width - 32, 16, validation.Widths.Head) * 22)
                + (SlideFit.Lines(step.GetProperty("text").GetString()!, width - 32, 20, validation.Widths.Body) * 28);
            parts.Add(new FitPart($"steps[{index}]", card));
            row = Math.Max(row, card);
            inRow++;
            if (inRow == columns || index == steps.Length - 1)
            {
                height += row + (height > 0 ? 28 : 0);
                row = 0;
                inRow = 0;
            }
        }

        SlideFit.Check(height, $"{path}.steps", validation, parts: parts, shares: ["steps"]);
    }
}