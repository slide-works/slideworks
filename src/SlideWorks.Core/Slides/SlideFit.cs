using System.Globalization;
using System.Text.Json;

namespace SlideWorks.Core.Slides;

// Rough text fitting against the default theme's 1920×1080 slide (design/sw-1-foundation mockups).
// A line holds width / (font size × average glyph width) whole characters. The widths come from the
// validation's FitWidths, never from a constant here; the built-in ones are 0.56 em for the Inter body
// text, for the bold Plus Jakarta Sans headings and for table cells (rendered cells fill to about
// 0.55 em). Body text is measured at 0.56, not its 0.47 em average, because Title Case runs at
// 0.54 em (0.56 for a bold lead) and must fit too (SW-117). Lines dense with acronyms or all in
// capitals (0.62–0.65 em) can still overflow. Text wraps word by word, as the browser breaks it, and spaces count as whole
// characters. The estimates err on the side of wrapping early, so a slide that passes fits.
internal static class SlideFit
{
    // Content slides: the 1680×810 box under the 150 px top padding, less the kicker (42 px), a two-line
    // headline (2 × 69 px) and the body's 46 px top margin is 584 px. The body may run 36 px into the
    // 120 px bottom padding and still clear the footer, which starts at y = 1023.
    public const double BODY_HEIGHT = 620;

    // Greedy word wrap: a word that doesn't fit after the last one starts the next line, and a word
    // longer than a whole line breaks across lines.
    public static int Lines(string text, double width, double fontSize, double em)
    {
        var perLine = Math.Max(1, (int)(width / (fontSize * em)));
        var lines = 1;
        var used = 0;
        foreach (var length in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(TextSlot.Length))
        {
            if (used > 0 && used + 1 + length <= perLine)
            {
                used += 1 + length;
                continue;
            }

            if (used > 0)
            {
                lines++;
            }

            var extra = (length - 1) / perLine;
            lines += extra;
            used = length - (extra * perLine);
        }

        return lines;
    }

    // The words of a text-or-object item: "text", or the lead and text of {lead, text}, or a cell's text.
    public static string Words(JsonElement item) => item.ValueKind == JsonValueKind.String
        ? item.GetString()!
        : string.Join(" ", new[] { "lead", "text" }
            .Where(name => item.TryGetProperty(name, out var part) && part.ValueKind == JsonValueKind.String)
            .Select(name => item.GetProperty(name).GetString()));

    public static void Check(double height, string path, SlotValidation validation, double budget = BODY_HEIGHT)
    {
        if (height > budget)
        {
            validation.Fail(path, string.Create(CultureInfo.InvariantCulture,
                $"too much text to fit on the slide (about {Math.Round(height)} px of {budget} px); use fewer or shorter items"));
        }
    }
}
