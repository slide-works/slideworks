using System.Globalization;
using System.Text.Json;

namespace SlideWorks.Core.Slides;

// Rough text fitting against the default theme's 1920×1080 slide (design/sw-1-foundation mockups).
// A line holds width / (font size × average glyph width) whole characters. The widths come from the
// validation's FitWidths, never from a constant here; the built-in ones are 0.56 em for the Inter body
// text, for the bold Plus Jakarta Sans headings and for table cells (rendered cells fill to about
// 0.55 em). Body text is measured at 0.56, not its 0.47 em average, because Title Case runs at
// 0.54 em (0.56 for a bold lead) and must fit too (SW-117). Wide characters (capitals, W, M, w, m,
// digits) cost more than one average character each (GlyphWidths), so all-capitals text wraps sooner;
// the line capacity itself is unchanged, which keeps lowercase prose where it was. Text wraps word by
// word, as the browser breaks it, and spaces count as whole characters. The estimates err on the side of wrapping early, so a slide that passes fits.
internal static class SlideFit
{
    // Content slides: the 1680×810 box under the 150 px top padding, less the kicker (42 px), a two-line
    // headline (2 × 69 px) and the body's 46 px top margin is 584 px. The body may run 36 px into the
    // 120 px bottom padding and still clear the footer, which starts at y = 1023.
    public const double BODY_HEIGHT = 620;

    // Greedy word wrap: a word that doesn't fit after the last one starts the next line, and a word
    // longer than a whole line breaks across lines. A line holds perLine average characters; a word
    // costs the sum of its characters' GlyphWidths factors (each at least 1), a space costs 1.
    public static int Lines(string text, double width, double fontSize, double em)
    {
        var perLine = Math.Max(1, (int)(width / (fontSize * em)));
        var lines = 1;
        var used = 0.0;
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var cost = GlyphWidths.Cost(word);
            if (used > 0 && used + 1 + cost <= perLine)
            {
                used += 1 + cost;
                continue;
            }

            if (used > 0)
            {
                lines++;
            }

            var extra = (int)Math.Ceiling(cost / perLine) - 1;
            lines += extra;
            used = cost - (extra * perLine);
        }

        return lines;
    }

    // The words of a text-or-object item: "text", or the lead and text of {lead, text}, or a cell's text.
    public static string Words(JsonElement item) => item.ValueKind == JsonValueKind.String
        ? item.GetString()!
        : string.Join(" ", new[] { "lead", "text" }
            .Where(name => item.TryGetProperty(name, out var part) && part.ValueKind == JsonValueKind.String)
            .Select(name => item.GetProperty(name).GetString()));

    // parts and shares are recorded even when the text fits, so a dry run can report the heights.
    // An overflow names each part's height and, when more than one slot draws in the region, every slot that shares it.
    public static void Check(
        double height,
        string path,
        SlotValidation validation,
        double budget = BODY_HEIGHT,
        IReadOnlyList<FitPart>? parts = null,
        IReadOnlyList<string>? shares = null)
    {
        var measured = parts ?? [];
        var shared = shares ?? [];
        validation.RecordFit(new FitRegion(path, height, budget, measured, shared));
        if (height > budget)
        {
            validation.Fail(path, string.Create(CultureInfo.InvariantCulture,
                $"too much text to fit on the slide (about {Math.Round(height)} px of {budget} px){Detail(measured, shared)}; use fewer or shorter items"));
        }
    }

    private static string Detail(IReadOnlyList<FitPart> parts, IReadOnlyList<string> shares)
    {
        var detail = "";
        if (parts.Count > 0)
        {
            detail += "; " + string.Join(", ", parts.Select(part => string.Create(CultureInfo.InvariantCulture, $"{part.Name} {Math.Round(part.Height)} px")));
        }

        if (shares.Count > 1)
        {
            detail += "; " + string.Join(" and ", shares) + " share this region";
        }

        if (parts.Count > 0)
        {
            var tip = parts.MaxBy(part => part.Height);
            detail += $"; {tip.Name} tips it over";
        }

        return detail;
    }
}
