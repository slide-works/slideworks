using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class PracticeLayout
{
    public static LayoutDefinition Definition { get; } = new("practice", "A practice: what to do, what comes back and what to avoid, with an optional callout that can arrive on the last step. density compact fits a fuller practice. Light or dark.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Opt("density", new EnumSlot("regular", "compact") { Default = "regular" }, "'compact' uses smaller type so a full practice fits; 'regular' is the default"),
        Req("do", new ListSlot(Obj(Opt("lead", new TextSlot(50), "Optional bold lead-in"), Req("text", new TextSlot(120))), 1, 5)),
        Opt("comes_back", new ListSlot(new TextSlot(100), 0, 4), "Optional. What the team gets back"),
        Req("avoid", new ListSlot(new TextSlot(100), 0, 4), "What to avoid; may be empty"),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], rules: Rule);

    private static readonly string[] RightShares = ["comes_back", "avoid", "callout"];

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var compact = slots.TryGetProperty("density", out var density) && density.ValueKind == JsonValueKind.String && density.GetString() == "compact";

        // .practice: 1.15fr 1fr with a 48 px gap (873 and 759 px). Regular: an 18 px header (34 px
        // with its margin) and an .it card of 44 px padding plus 25 px/1.4 text, 14 px apart.
        // Compact: a 22 px header and a card of 16 px padding plus 16 px/1.25 text, 6 px apart.
        var doItems = slots.GetProperty("do").EnumerateArray().ToArray();
        var doParts = new List<FitPart>(doItems.Length);
        var doHeight = compact ? 22.0 : 34;
        for (var index = 0; index < doItems.Length; index++)
        {
            var height = Item(SlideFit.Words(doItems[index]), 821, compact, validation.Widths);
            doParts.Add(new FitPart($"do[{index}]", height));
            doHeight += height;
        }

        SlideFit.Check(doHeight, $"{path}.do", validation, parts: doParts, shares: ["do"]);

        var right = 0.0;
        var rightParts = new List<FitPart>();
        foreach (var name in new[] { "comes_back", "avoid" })
        {
            if (!ObjectSlot.IsPresent(slots, name, out var list) || list.GetArrayLength() == 0)
            {
                continue;
            }

            var header = compact ? 22.0 : 34;
            var gap = 0.0;
            if (right > 0)
            {
                gap = compact ? 12 : 26;
            }
            right += gap;
            var items = list.EnumerateArray().ToArray();
            var block = header;
            for (var index = 0; index < items.Length; index++)
            {
                var height = Item(items[index].GetString()!, 699, compact, validation.Widths);
                rightParts.Add(new FitPart($"{name}[{index}]", height));
                block += height;
            }

            right += block;
        }

        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            var height = LayoutRules.CalloutHeight(callout, 759, validation.Widths) - (compact ? 18 : 8);
            rightParts.Add(new FitPart("callout", height));
            right += height;
        }

        SlideFit.Check(right, $"{path}.comes_back", validation, parts: rightParts, shares: RightShares);
    }

    // A card's height: regular is 58 px of padding and gap plus 35 px a line; compact is 22 plus 20.
    private static double Item(string text, double width, bool compact, FitWidths widths)
    {
        var lines = SlideFit.Lines(text, width, compact ? 16 : 25, widths.Body);
        return compact ? 22 + (lines * 20) : 58 + (lines * 35);
    }
}
