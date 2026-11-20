using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class BulletsLayout
{
    public static LayoutDefinition Definition { get; } = new("bullets", "A headline over 1–8 points, each plain text or an optional icon, bold lead and text. Can reveal one point per step and bring the callout in last.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("items", new ListSlot(Point(200, 80, 160), 1, 8), "Each point is a string, or {icon?, lead?, text} for an icon and a bold lead-in"),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("items"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        // ul.bl: 36 px/1.35 text, 52 px indent (68 px when any point has an icon) in a 1500 px list, 30 px between items.
        var items = slots.GetProperty("items").EnumerateArray().ToArray();
        var width = items.Any(HasIcon) ? 1432 : 1448;
        var height = items.Sum(i => SlideFit.Lines(SlideFit.Words(i), width, 36, validation.Widths.Body) * 48.6) + ((items.Length - 1) * 30);
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            height += LayoutRules.CalloutHeight(callout, 1680, validation.Widths);
        }

        SlideFit.Check(height, $"{path}.items", validation);
    }

    private static bool HasIcon(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("icon", out var icon) && icon.ValueKind == JsonValueKind.String;
}
