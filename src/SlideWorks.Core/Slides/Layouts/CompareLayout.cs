using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class CompareLayout
{
    public static LayoutDefinition Definition { get; } = new("compare", "Two panels side by side: the failure mode and the success mode.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("failure", ComparePanel("negative")),
        Req("success", ComparePanel("positive")),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        // .panel: 820 px wide, 44/48 px padding, 10 px top border, an 18 px tag, a 44 px/1.1 title and
        // 27 px/1.4 points 16 px apart in a 694 px list.
        foreach (var side in new[] { "failure", "success" })
        {
            var panel = slots.GetProperty(side);
            var height = 160 + (SlideFit.Lines(panel.GetProperty("title").GetString()!, 724, 44, validation.Widths.Head) * 48.4);
            if (ObjectSlot.IsPresent(panel, "sub", out var sub))
            {
                height += 16 + (SlideFit.Lines(sub.GetString()!, 724, 27, validation.Widths.Body) * 37.8);
            }

            var points = panel.GetProperty("points").EnumerateArray().ToArray();
            height += points.Sum(p => SlideFit.Lines(p.GetString()!, 694, 27, validation.Widths.Body) * 37.8) + ((points.Length - 1) * 16);
            SlideFit.Check(height, $"{path}.{side}", validation);
        }
    }
}
