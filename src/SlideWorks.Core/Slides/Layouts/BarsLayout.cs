using System.Globalization;
using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class BarsLayout
{
    // The geometry is the client's src/slides/diagrams/barsGeometry.ts, number for number (one source there):
    // the 400 px label column, 32 px gap and 1248 px track (TRACK); bar height by count 96 for 2-3, 80 for 4, 64 for 5
    // and 56 for 6; rows 28 apart, 20 for 5 or 6; a 38 px legend; a caption 34 px under the last bar at 24 px
    // (32.4 px lines); the label at 30 px (36 px lines) and the sub at 20 px (26 px lines, 4 px above);
    // the end label's value at 28 px x 0.62 em, its tag at 18 px x 0.85 em + 14, plus 30; a bar of at least 6 px;
    // a note at 22 px x 0.56 em, inside a bar with 40 px to spare, or 18 px after the bar or the outline.
    // Every em is per character: each character costs its GlyphWidths factor (a W 1.87, a digit 1.22, a
    // lowercase letter 1.0), the same table as the client's glyphWidths.ts, because a bar's label never wraps.
    private const double LABEL_COLUMN = 400;
    private const double TRACK = 1248;
    private const double LEGEND = 38;
    private const double CAPTION_GAP = 34;
    private const double CAPTION_LINE = 32.4;
    private const double CAPTION_FONT = 24;
    private const double BODY_WIDTH = 1680;
    private const double LABEL_FONT = 30;
    private const double LABEL_LINE = 36;
    private const double SUB_FONT = 20;
    private const double SUB_LINE = 26;
    private const double SUB_GAP = 4;
    private const double VALUE_FONT = 28;
    private const double VALUE_EM = 0.62;
    private const double TAG_FONT = 18;
    private const double TAG_EM = 0.85;
    private const double TAG_PAD = 14;
    private const double END_PAD = 30;
    private const double MIN_BAR = 6;
    private const double NOTE_FONT = 22;
    private const double NOTE_EM = 0.56;
    private const double NOTE_PAD = 40;
    private const double NOTE_GAP = 18;

    public static LayoutDefinition Definition { get; } = new("bars", "2–6 horizontal bars that compare named things. A bar can carry a note, a dashed outline for what it was, a tone and a tag. Can reveal one bar per step. For a quantity over time, use chart.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("bars", new ListSlot(Obj(
            Req("label", new TextSlot(30)),
            Opt("sub", new TextSlot(40), "A small line under the label"),
            Req("value", new NumberSlot(0, 1_000_000_000, false), "The bar's length; decimals are allowed"),
            Opt("was", new NumberSlot(0, 1_000_000_000, false), "A dashed outline at this length: what it was, or what was expected"),
            Opt("note", new TextSlot(50), "Written inside the bar, or after it when the bar is too short"),
            Opt("tone", Tone(null), "Draws the bar in this tone; defaults to 'accent'"),
            Opt("tag", new TextSlot(16), "A short marker after the bar's end, such as 'Bottleneck'")), 2, 6), "In the order to draw them"),
        Opt("unit", new TextSlot(8), "Written after each value"),
        Opt("values", new EnumSlot("shown", "hidden") { Default = "shown" }, "'hidden' leaves the numbers off, for lengths that are illustrative"),
        Opt("wasLabel", new TextSlot(24), "What the dashed outline means, such as 'Before'; shown as a small legend. Needs a bar with a was"),
        Opt("caption", new TextSlot(160)),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("bars"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var bars = slots.GetProperty("bars").EnumerateArray().ToArray();
        if (!bars.Any(b => Number(b, "value") > 0 || Number(b, "was") > 0))
        {
            validation.Fail($"{path}.bars", "needs at least one value or was above 0, or there is nothing to draw to scale");
            return;
        }

        var hasWasLabel = ObjectSlot.IsPresent(slots, "wasLabel", out _);
        if (hasWasLabel && !bars.Any(b => ObjectSlot.IsPresent(b, "was", out _)))
        {
            validation.Fail($"{path}.wasLabel", "needs a bar with a was to explain; give a bar a was, or drop the wasLabel");
            return;
        }

        var shown = !ObjectSlot.IsPresent(slots, "values", out var values) || !string.Equals(values.GetString(), "hidden", StringComparison.Ordinal);
        var unit = ObjectSlot.IsPresent(slots, "unit", out var unitText) ? unitText.GetString()! : "";
        var ends = bars.Select(b => EndWidth(b, unit, shown)).ToArray();
        var largest = bars.Max(b => Math.Max(Number(b, "value"), Number(b, "was")));
        var available = TRACK - ends.Max();
        for (var i = 0; i < bars.Length; i++)
        {
            if (!ObjectSlot.IsPresent(bars[i], "note", out var note))
            {
                continue;
            }

            var noteWidth = GlyphWidths.Cost(note.GetString()!) * NOTE_FONT * NOTE_EM;
            var inside = noteWidth + NOTE_PAD <= Length(Number(bars[i], "value"), largest, available);
            var mark = Length(Math.Max(Number(bars[i], "value"), Number(bars[i], "was")), largest, available);
            var after = mark + NOTE_GAP + noteWidth + ends[i] <= TRACK;
            if (!inside && !after)
            {
                validation.Fail($"{path}.bars[{i}].note", "too much text to fit in this bar; shorten it or leave it out");
                return;
            }
        }

        var height = bars.Sum(b => RowHeight(b, bars.Length, validation)) + (RowGap(bars.Length) * (bars.Length - 1));
        height += hasWasLabel ? LEGEND : 0;
        if (ObjectSlot.IsPresent(slots, "caption", out var caption))
        {
            height += CAPTION_GAP + (SlideFit.Lines(caption.GetString()!, BODY_WIDTH, CAPTION_FONT, validation.Widths.Body) * CAPTION_LINE);
        }

        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            height += LayoutRules.CalloutHeight(callout, BODY_WIDTH, validation.Widths);
        }

        SlideFit.Check(height, $"{path}.bars", validation);
    }

    // 0 when the bar has no such number.
    private static double Number(JsonElement bar, string name) =>
        ObjectSlot.IsPresent(bar, name, out var value) ? value.GetDouble() : 0;

    // A bar's length: its share of the track left for the bars, never under 6 px.
    private static double Length(double value, double largest, double available) =>
        Math.Max(MIN_BAR, value / largest * available);

    // The room an end label needs right of its bar: the value and unit at 28 px, the tag at 18 px, and 30 px.
    private static double EndWidth(JsonElement bar, string unit, bool shown)
    {
        var width = 0.0;
        if (shown)
        {
            var text = Number(bar, "value").ToString("#,##0.####################", CultureInfo.InvariantCulture) + (unit.Length > 0 ? " " + unit : "");
            width += GlyphWidths.Cost(text) * VALUE_FONT * VALUE_EM;
        }

        if (ObjectSlot.IsPresent(bar, "tag", out var tag))
        {
            width += (GlyphWidths.Cost(tag.GetString()!) * TAG_FONT * TAG_EM) + TAG_PAD;
        }

        return width > 0 ? width + END_PAD : 0;
    }

    private static double BarHeight(int count) => count switch
    {
        <= 3 => 96,
        4 => 80,
        5 => 64,
        _ => 56,
    };

    private static double RowGap(int count) => count >= 5 ? 20 : 28;

    private static double RowHeight(JsonElement bar, int count, SlotValidation validation)
    {
        var text = SlideFit.Lines(bar.GetProperty("label").GetString()!, LABEL_COLUMN, LABEL_FONT, validation.Widths.Body) * LABEL_LINE;
        if (ObjectSlot.IsPresent(bar, "sub", out var sub))
        {
            text += (SlideFit.Lines(sub.GetString()!, LABEL_COLUMN, SUB_FONT, validation.Widths.Body) * SUB_LINE) + SUB_GAP;
        }

        return Math.Max(BarHeight(count), text);
    }
}
