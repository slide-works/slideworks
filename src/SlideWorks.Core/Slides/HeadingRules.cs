using System.Text.Json;

namespace SlideWorks.Core.Slides;

// The kicker and headline every content layout opens with, checked once for all of them from
// LayoutDefinition.Validate (keyed on the field names "headline" and "kicker"), not in each layout.
//
// Headline: SlideHeading shrinks it on the client from 64 px to HEADLINE_FLOOR to keep it to its line
// budget (2 lines; image with position "right" keeps 3 in its 760 px column). useFitText returns the
// floor even when nothing fits, so the server refuses a headline whose SlideFit.Lines at the floor,
// at the headline's width, exceeds the budget. At 48 px the 1560 px headline holds 58 average
// characters a line (1560 / (48 × 0.56)), so 100 characters of lowercase prose take 2 lines while
// 100 capital letters (1.39 average characters each) take 3 and are refused.
// Quote has its own headline fit (QuoteLayout) and close its own shrink (CloseLayout); image with
// position "right" is checked at full size by ImageLayout, which is stricter than the floor.
//
// Kicker: one row, uppercase, with letter-spacing. Every letter is counted as a capital, as the client
// uppercases it, and the spacing folds into the em (em + spacing, in em units) because SlideFit.Lines
// has no letter-spacing parameter.
internal static class HeadingRules
{
    public const double HEADLINE_START = 64;
    public const double HEADLINE_FLOOR = 48;
    public const double HEADLINE_WIDTH = 1560;
    public const int HEADLINE_LINES = 2;

    private const double KICKER_SIZE = 20;
    private const double KICKER_SPACING = 0.18;
    private const double KICKER_WIDTH = 1680;

    public static void Check(string layout, JsonElement slots, string path, SlotValidation validation)
    {
        if (Text(slots, "kicker") is { } kicker)
        {
            var (size, spacing, width) = Kicker(layout, slots);
            CheckKicker(kicker, path, width, size, spacing, validation);
        }

        if (Text(slots, "headline") is { } headline && layout is not ("quote" or "close") && !IsImageRight(layout, slots))
        {
            CheckHeadline(headline, path, validation);
        }
    }

    // The drawn size, letter-spacing (em) and width of a layout's kicker.
    public static (double Size, double Spacing, double Width) Kicker(string layout, JsonElement slots) => layout switch
    {
        // Join's text column: 1680 less the 780 px QR code and the 72 px gap, at 36 px with 0.14em.
        "join" => (36, 0.14, 828),
        // The title slide pads its content 40 px on the left.
        "title" => (KICKER_SIZE, KICKER_SPACING, KICKER_WIDTH - 40),
        "image" when IsImageRight(layout, slots) => (KICKER_SIZE, KICKER_SPACING, 760),
        _ => (KICKER_SIZE, KICKER_SPACING, KICKER_WIDTH),
    };

    public static void CheckHeadline(string text, string path, SlotValidation validation)
    {
        var lines = SlideFit.Lines(text, HEADLINE_WIDTH, HEADLINE_FLOOR, validation.Widths.Head);
        if (lines > HEADLINE_LINES)
        {
            validation.Fail($"{path}.headline", $"headline: too much text to fit in {HEADLINE_LINES} lines (it wraps to about {lines} even at the smallest size); shorten it");
        }
    }

    public static void CheckKicker(string text, string path, double width, double size, double spacing, SlotValidation validation)
    {
        var lines = SlideFit.Lines(text.ToUpperInvariant(), width, size, validation.Widths.Body + spacing);
        if (lines > 1)
        {
            validation.Fail($"{path}.kicker", "kicker: too much text to fit on one line (it is drawn in capitals with wide letter-spacing); shorten it");
        }
    }

    private static bool IsImageRight(string layout, JsonElement slots) =>
        layout == "image" && Text(slots, "position") == "right";

    private static string? Text(JsonElement slots, string name) =>
        slots.ValueKind == JsonValueKind.Object && slots.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
