using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class ImageLayout
{
    public static LayoutDefinition Definition { get; } = new("image", "A headline over one large image, with optional points beside it and a caption. The image can go without its card, sit on the right at full height, and the points can arrive one per step.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("image", new ImageSlot()),
        Opt("frame", new EnumSlot("card", "none") { Default = "card" }, "'card' (the default) puts the image on a white card with a shadow; 'none' draws it as it is, for a transparent image"),
        Opt("position", new EnumSlot("below", "right") { Default = "below" }, "'below' (the default) puts the image under the headline; 'right' puts it beside the headline at full height"),
        Opt("points", new ListSlot(Point(80, 30, 80), 1, 4), "Each point is a string, or {icon?, lead?, text} for an icon and a bold lead-in"),
        Opt("caption", new TextSlot(140)),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("points"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var right = slots.TryGetProperty("position", out var position) && position.ValueKind == JsonValueKind.String && position.GetString() == "right";
        var points = ObjectSlot.IsPresent(slots, "points", out var list) ? list.EnumerateArray().ToArray() : [];
        var calloutWidth = right ? 760 : 1680;
        var callout = ObjectSlot.IsPresent(slots, "callout", out var block) ? LayoutRules.CalloutHeight(block, calloutWidth, validation.Widths) : 0;
        if (!right)
        {
            // The column of points beside the image: 26 px/1.35 in 636 px, 22 px apart; the callout takes its height under both.
            SlideFit.Check(PointsHeight(points, 636, validation), $"{path}.points", validation, SlideFit.BODY_HEIGHT - callout);
            return;
        }

        // Left column of 760 px: a 42 px kicker, the headline at 64 px/1.08, the points 46 px under it, the callout at the foot.
        var headline = SlideFit.Lines(slots.GetProperty("headline").GetString()!, 760, 64, validation.Widths.Head);
        if (headline > 3)
        {
            validation.Fail($"{path}.headline", $"too long beside the image: it wraps to about {headline} lines in the left column, and at most 3 fit");
            return;
        }

        var height = 42 + (headline * 69.1) + callout;
        if (points.Length > 0)
        {
            height += 46 + PointsHeight(points, 716, validation);
        }

        SlideFit.Check(height, $"{path}.points", validation, 846);
    }

    private static double PointsHeight(JsonElement[] points, double width, SlotValidation validation) =>
        points.Length == 0
            ? 0
            : points.Sum(p => SlideFit.Lines(SlideFit.Words(p), width, 26, validation.Widths.Body) * 35.1) + ((points.Length - 1) * 22);
}
