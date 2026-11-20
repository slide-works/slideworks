using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class TwoColumnLayout
{
    public static LayoutDefinition Definition { get; } = new("two-column", "A headline over two columns. Each column holds a list of points, a paragraph or an image. Light or dark.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("left", Column(60, icons: true), "Exactly one of items, text or image; an icon needs a title"),
        Req("right", Column(60, icons: true), "Exactly one of items, text or image; an icon needs a title"),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.Fixed(1, "the right column"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        // .two: 1fr 1.05fr with a 64 px gap, so the narrower column is 788 px. Images scale to fit.
        var callout = ObjectSlot.IsPresent(slots, "callout", out var block) ? LayoutRules.CalloutHeight(block, 1680, validation.Widths) : 0;
        foreach (var side in new[] { "left", "right" })
        {
            var column = slots.GetProperty(side);
            ColumnIconNeedsTitle(column, $"{path}.{side}", validation);
            var parts = new List<FitPart>();
            var shares = new List<string> { side };
            var height = 0.0;
            if (callout > 0)
            {
                parts.Add(new FitPart("callout", callout));
                shares.Add("callout");
                height += callout;
            }

            if (ObjectSlot.IsPresent(column, "title", out var title))
            {
                var titleHeight = (SlideFit.Lines(title.GetString()!, 788, 34, validation.Widths.Head) * 40.8) + 16;
                parts.Add(new FitPart($"{side}.title", titleHeight));
                height += titleHeight;
            }

            if (ObjectSlot.IsPresent(column, "items", out var items))
            {
                var list = items.EnumerateArray().ToArray();
                for (var index = 0; index < list.Length; index++)
                {
                    var itemHeight = (SlideFit.Lines(SlideFit.Words(list[index]), 736, 30, validation.Widths.Body) * 40.5) + (index > 0 ? 30 : 0);
                    parts.Add(new FitPart($"{side}.items[{index}]", itemHeight));
                    height += itemHeight;
                }
            }

            if (ObjectSlot.IsPresent(column, "text", out var text))
            {
                var textHeight = SlideFit.Lines(text.GetString()!, 788, 28, validation.Widths.Body) * 39.2;
                parts.Add(new FitPart($"{side}.text", textHeight));
                height += textHeight;
            }

            SlideFit.Check(height, $"{path}.{side}", validation, parts: parts, shares: shares);
        }
    }
}
