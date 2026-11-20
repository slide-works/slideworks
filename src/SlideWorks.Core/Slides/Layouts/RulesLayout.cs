using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class RulesLayout
{
    public static LayoutDefinition Definition { get; } = new("rules", "1–4 numbered rules, each a title and text. Can reveal one rule per step and bring the callout in last.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("rules", new ListSlot(Obj(Req("title", new TextSlot(60)), Req("text", new TextSlot(110))), 1, 4)),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("rules"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        if (validation.ErrorCount > 0)
        {
            return;
        }

        // .rules: rules 34 px apart, each a 170 px numeral column (150 px/0.85, so 127.5 px tall) beside a
        // 1510 px column of a 42 px/1.2 title and 26 px/1.4 text 10 px under it, centred on the numeral.
        var rules = slots.GetProperty("rules").EnumerateArray().ToArray();
        var height = rules.Sum(r => Math.Max(127.5,
            (SlideFit.Lines(r.GetProperty("title").GetString()!, 1510, 42, validation.Widths.Head) * 50.4) + 10
            + (SlideFit.Lines(r.GetProperty("text").GetString()!, 1510, 26, validation.Widths.Body) * 36.4))) + ((rules.Length - 1) * 34);
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            height += LayoutRules.CalloutHeight(callout, 1680, validation.Widths);
        }

        SlideFit.Check(height, $"{path}.rules", validation);
    }
}
