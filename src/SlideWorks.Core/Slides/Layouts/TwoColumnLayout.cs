using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class TwoColumnLayout
{
    public static LayoutDefinition Definition { get; } = new("two-column", "A headline over two columns. Each column holds a list of points, a paragraph or an image.", false,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("left", Column(), "Exactly one of items, text or image"),
        Req("right", Column(), "Exactly one of items, text or image"),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        // .two: 1fr 1.05fr with a 64 px gap, so the narrower column is 788 px. Images scale to fit.
        foreach (var side in new[] { "left", "right" })
        {
            var column = slots.GetProperty(side);
            var height = 0.0;
            if (ObjectSlot.IsPresent(column, "title", out var title))
            {
                height += (SlideFit.Lines(title.GetString()!, 788, 34, validation.Widths.Head) * 40.8) + 16;
            }

            if (ObjectSlot.IsPresent(column, "items", out var items))
            {
                var list = items.EnumerateArray().ToArray();
                height += list.Sum(i => SlideFit.Lines(SlideFit.Words(i), 736, 30, validation.Widths.Body) * 40.5) + ((list.Length - 1) * 30);
            }

            if (ObjectSlot.IsPresent(column, "text", out var text))
            {
                height += SlideFit.Lines(text.GetString()!, 788, 28, validation.Widths.Body) * 39.2;
            }

            SlideFit.Check(height, $"{path}.{side}", validation);
        }
    }
}
