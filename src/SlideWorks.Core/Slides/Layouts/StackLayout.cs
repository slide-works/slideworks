using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class StackLayout
{
    public static LayoutDefinition Definition { get; } = new("stack", "A stack of 2–8 labelled segments that make up one whole, with optional values, a highlighted segment and points beside it. Can reveal one segment per step, from the bottom up.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("segments", new ListSlot(Obj(
            Req("label", new TextSlot(40)),
            Opt("value", new NumberSlot(0, 1_000_000_000, false), "A number above 0; a segment's height follows it. Give every segment a value or none"),
            Opt("note", new TextSlot(40)),
            Opt("tone", Tone(null), "Highlights the segment in this tone"),
            Opt("tag", new TextSlot(12), "A small tag after the label")), 2, 8), "Listed top to bottom"),
        Opt("unit", new TextSlot(8), "The unit of the values; needs values"),
        Opt("total", new TextSlot(30), "A line under the stack"),
        Opt("caption", new TextSlot(60)),
        Opt("points", new ListSlot(LeadOrText(120, 8, 90), 1, 4), "Each point is a string, or {lead, text} for a bold lead-in; they stand beside the stack"),
        Opt("side", new EnumSlot("right", "left") { Default = "right" }, "'right' (the default) puts the stack on the right of the points; 'left' puts it on the left"),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.ListReversed("segments"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var segments = slots.GetProperty("segments").EnumerateArray().ToArray();
        var valued = segments.Count(s => ObjectSlot.IsPresent(s, "value", out _));
        for (var i = 0; i < segments.Length; i++)
        {
            if (ObjectSlot.IsPresent(segments[i], "value", out var value))
            {
                if (value.GetDouble() <= 0)
                {
                    validation.Fail($"{path}.segments[{i}].value", "must be above 0");
                    return;
                }
            }
            else if (valued > 0)
            {
                validation.Fail($"{path}.segments[{i}].value", "required, because other segments have a value");
                return;
            }
        }

        if (valued == 0 && ObjectSlot.IsPresent(slots, "unit", out _))
        {
            validation.Fail($"{path}.unit", "not allowed without values: give every segment a value, or drop the unit");
            return;
        }

        // The stack is 500 px: each row holds its label (and the tag's room) in a 492 px cell at 26 px/1.3, a note
        // line of 26 px, and 10 px of padding, at least 44 px; the rows are 4 px apart.
        var rows = segments.Sum(s => RowHeight(s, validation)) + (4 * (segments.Length - 1));
        SlideFit.Check(rows, $"{path}.segments", validation, 500);

        // The points and callout in the 800 px text column, to the height of the body.
        var points = ObjectSlot.IsPresent(slots, "points", out var list) ? list.EnumerateArray().ToArray() : [];
        var side = points.Sum(p => PointHeight(p, validation)) + (30 * Math.Max(0, points.Length - 1));
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            side += LayoutRules.CalloutHeight(callout, 800, validation.Widths);
        }

        SlideFit.Check(side, $"{path}.points", validation, 620);
    }

    private static double RowHeight(JsonElement segment, SlotValidation validation)
    {
        var label = segment.GetProperty("label").GetString()!;
        if (ObjectSlot.IsPresent(segment, "tag", out var tag))
        {
            label += " " + new string('x', TextSlot.Length(tag.GetString()!) + 3);
        }

        var note = ObjectSlot.IsPresent(segment, "note", out _) ? 26 : 0;
        return Math.Max(44, (SlideFit.Lines(label, 492, 26, validation.Widths.Body) * 33.8) + note + 10);
    }

    private static double PointHeight(JsonElement point, SlotValidation validation) =>
        point.ValueKind == JsonValueKind.String
            ? SlideFit.Lines(point.GetString()!, 800, 30, validation.Widths.Body) * 40.5
            : Math.Max(76, SlideFit.Lines(point.GetProperty("text").GetString()!, 546, 30, validation.Widths.Body) * 40.5);
}
