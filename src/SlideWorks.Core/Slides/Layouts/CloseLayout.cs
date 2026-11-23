using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class CloseLayout
{
    private const int CLOSE_HEADLINE_MAX_LINES = 3;

    // close h2: 112 px/1.02 in a 1400 px column, so a written line wraps after about 22 characters.
    private const int CLOSE_HEADLINE_MAX_SHOWN_LINES = 4;

    // The stack (headline, sub box, author) may be 846 px tall: from the content top at y 150 to y 996, above
    // the footer. close.module.css: headline 112 px/1.02; sub 36 px/1.35 in a 1200 px box (36 px padding either
    // side, 28 px above and below) under a 60 px margin; author 28 px/1.2 in 1640 px under a 70 px margin.
    private const double CLOSE_STACK_HEIGHT = 846;
    private const double CLOSE_SUB_WIDTH = 1128;

    public static LayoutDefinition Definition { get; } = new("close", "Closing slide (dark): a short headline of up to three written lines, with an optional sub and author.", LayoutSurface.Dark,
    [
        Req("headline", new TextSlot(80, lineBreaks: true), "Set at 112 px, about 22 characters a line: use \\n for a line break, at most 3 written and 4 shown lines; it shrinks to 60 px to keep 4 lines. An emphasised phrase sits within one written line: a phrase that spans a line break is refused"),
        Emphasis("headline", "sub"),
        Opt("sub", new TextSlot(110)),
        Opt("author", new TextSlot(60)),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline", "sub");
        var written = slots.GetProperty("headline").GetString()!.Split('\n');
        if (written.Length > CLOSE_HEADLINE_MAX_LINES)
        {
            validation.Fail($"{path}.headline", $"at most {CLOSE_HEADLINE_MAX_LINES} lines (got {written.Length})");
            return;
        }

        var shown = written.Sum(line => SlideFit.Lines(line, 1400, 112, validation.Widths.Head));
        if (shown > CLOSE_HEADLINE_MAX_SHOWN_LINES)
        {
            validation.Fail($"{path}.headline", $"too much text to fit: the headline wraps to about {shown} lines at the close slide's size, and at most {CLOSE_HEADLINE_MAX_SHOWN_LINES} fit");
        }

        var subLines = Lines(slots, "sub", CLOSE_SUB_WIDTH, 36, validation.Widths.Head);
        var authorLines = Lines(slots, "author", 1640, 28, validation.Widths.Body);
        var height = (shown * 112 * 1.02)
            + (subLines > 0 ? 60 + 56 + (subLines * 36 * 1.35) : 0)
            + (authorLines > 0 ? 70 + (authorLines * 28 * 1.2) : 0);
        if (height > CLOSE_STACK_HEIGHT)
        {
            validation.Fail($"{path}.{(subLines > 0 ? "sub" : "author")}", $"too much text to fit: the headline, sub and author take about {height:0} px, and at most {CLOSE_STACK_HEIGHT} px fit; shorten the sub or the author");
        }
    }

    // The lines a text slot takes, or 0 when it is not set.
    private static int Lines(JsonElement slots, string name, double width, double size, double em) =>
        slots.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? SlideFit.Lines(value.GetString()!, width, size, em)
            : 0;
}
