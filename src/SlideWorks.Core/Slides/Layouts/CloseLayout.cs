using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class CloseLayout
{
    private const int CLOSE_HEADLINE_MAX_LINES = 3;

    // close h2: 112 px/1.02 in a 1400 px column, so a written line wraps after about 22 characters.
    private const int CLOSE_HEADLINE_MAX_SHOWN_LINES = 4;

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
    }
}
