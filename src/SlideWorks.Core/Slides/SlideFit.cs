using System.Globalization;
using System.Text.Json;

namespace SlideWorks.Core.Slides;

// Rough text fitting against the default theme's 1920×1080 slide (design/sw-1-foundation mockups).
// A line holds width / (font size × average glyph width) characters: about 0.5 em for the Inter body
// text and 0.56 em for the bold Plus Jakarta Sans headings. The estimates err on the side of wrapping
// early, so a slide that passes fits.
internal static class SlideFit
{
    // Content slides: the 1680×810 box under the 150 px top padding, less the kicker (42 px), a two-line
    // headline (2 × 69 px) and the body's 46 px top margin is 584 px. The body may run 36 px into the
    // 120 px bottom padding and still clear the footer, which starts at y = 1023.
    public const double BODY_HEIGHT = 620;

    public const double BODY_EM = 0.5;
    public const double HEAD_EM = 0.56;

    public static int Lines(string text, double width, double fontSize, double em) =>
        Math.Max(1, (int)Math.Ceiling(TextSlot.Length(text) * fontSize * em / width));

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
