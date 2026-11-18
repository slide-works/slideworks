using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class CompareLayout
{
    private const double STAMP_ROW = 78;
    private const string LINE_NOTE = "One large line, such as a quoted sentence. Write the quotation marks yourself";

    public static LayoutDefinition Definition { get; } = new("compare", "Two panels side by side: the failure mode and the success mode. A panel holds points, label-and-value rows or one large line, with an optional stamp, footer and icon. A callout can sit under the panels.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline", "failure.line", "success.line"),
        Req("failure", Panel("negative")),
        Req("success", Panel("positive")),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.Fixed(1, "the success panel").Then(StepPlan.WhenAny(["failure.stamp", "success.stamp"], 1, "the stamps")), rules: Rule);

    private static ObjectSlot Panel(string defaultTone) => new(
    [
        Req("label", new TextSlot(24), "The panel's tag, such as 'Failure mode'"),
        Req("title", new TextSlot(50)),
        Opt("sub", new TextSlot(100)),
        Opt("points", new ListSlot(new TextSlot(90), 1, 4)),
        Opt("rows", new ListSlot(Obj(Req("label", new TextSlot(16)), Req("value", new TextSlot(40))), 1, 4)),
        Opt("line", new TextSlot(100), LINE_NOTE),
        Opt("stamp", new BlockSlot(InlineBlocks.Stamp)),
        Opt("footer", new TextSlot(90)),
        Opt("tone", Tone(defaultTone), $"{TONE}; defaults to '{defaultTone}'"),
        Opt("fill", PanelFill(), PANEL_FILL),
        Opt("icon", new IconSlot()),
    ], ["points", "rows", "line"]);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline", "failure.line", "success.line");
        if (validation.ErrorCount > 0)
        {
            return;
        }

        // .panel: 820 px wide, 44/48 px padding, 10 px top border, an 18 px tag, a 44 px/1.1 title.
        var callout = ObjectSlot.IsPresent(slots, "callout", out var block) ? LayoutRules.CalloutHeight(block, 1680, validation.Widths) : 0;
        var budget = SlideFit.BODY_HEIGHT - callout;
        foreach (var side in new[] { "failure", "success" })
        {
            var panel = slots.GetProperty(side);
            var height = 160 + (SlideFit.Lines(panel.GetProperty("title").GetString()!, 724, 44, validation.Widths.Head) * 48.4);
            if (ObjectSlot.IsPresent(panel, "sub", out var sub))
            {
                height += 16 + (SlideFit.Lines(sub.GetString()!, 724, 27, validation.Widths.Body) * 37.8);
            }

            height += Body(panel, validation.Widths);
            if (ObjectSlot.IsPresent(panel, "stamp", out _))
            {
                height += STAMP_ROW;
            }

            if (ObjectSlot.IsPresent(panel, "footer", out var footer))
            {
                height += 35 + (SlideFit.Lines(footer.GetString()!, 724, 24, validation.Widths.Body) * 33.6);
            }

            if (height > budget)
            {
                SlideFit.Check(height, $"{path}.{side}", validation, budget);
            }
        }
    }

    private static double Body(JsonElement panel, FitWidths widths)
    {
        if (ObjectSlot.IsPresent(panel, "rows", out var rows))
        {
            return rows.EnumerateArray().Sum(r => 25 + (SlideFit.Lines(r.GetProperty("value").GetString()!, 524, 32, widths.Body) * 38.4));
        }

        if (ObjectSlot.IsPresent(panel, "line", out var line))
        {
            return SlideFit.Lines(line.GetString()!, 724, 40, widths.Body) * 50;
        }

        var points = panel.GetProperty("points").EnumerateArray().ToArray();
        return points.Sum(p => SlideFit.Lines(p.GetString()!, 694, 27, widths.Body) * 37.8) + ((points.Length - 1) * 16);
    }
}
