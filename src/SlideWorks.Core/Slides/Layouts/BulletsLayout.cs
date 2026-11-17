using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class BulletsLayout
{
    public static LayoutDefinition Definition { get; } = new("bullets", "A headline over 1–8 points, each plain text or a bold lead plus text. Can reveal one point per step.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("items", new ListSlot(LeadOrText(200, 60, 160), 1, 8), "Each point is a string, or {lead, text} for a bold lead-in"),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("items"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        // ul.bl: 36 px/1.35 text, 52 px indent in a 1500 px list, 30 px between items.
        var items = slots.GetProperty("items").EnumerateArray().ToArray();
        var height = items.Sum(i => SlideFit.Lines(SlideFit.Words(i), 1448, 36, validation.Widths.Body) * 48.6) + ((items.Length - 1) * 30);
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            height += LayoutRules.CalloutHeight(callout, 1680, validation.Widths);
        }

        SlideFit.Check(height, $"{path}.items", validation);
    }
}
