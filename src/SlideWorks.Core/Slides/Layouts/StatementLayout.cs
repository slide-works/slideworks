using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class StatementLayout
{
    private const int STATEMENT_MAX = 70;
    private const int SUB_MAX = 100;
    private const int STATEMENT_MAX_LINES = 3;

    // The statement is set at 210 px and shrinks to 110 px; the shown-line check uses the floor.
    private const int STATEMENT_FLOOR_SIZE = 110;
    private const int STATEMENT_WIDTH = 1680;
    private const int SUB_SIZE = 56;
    private const int SUB_MAX_LINES = 2;

    public static LayoutDefinition Definition { get; } = new("statement", "One big sentence that carries the slide, light or dark, with an optional sub line. Can reveal the sub line on a step.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("statement", new TextSlot(STATEMENT_MAX, lineBreaks: true), "Set at 210 px: use \\n for a line break, at most 3 written and 3 shown lines; it shrinks to 110 px to keep them"),
        Emphasis("statement", "sub"),
        Opt("sub", new TextSlot(SUB_MAX), "One or two lines under the sentence"),
        Opt("reveal", Reveal(), "'one-by-one' shows the sub line on a step; 'all' (the default) shows it with the slide"),
    ], steps: StepPlan.When("sub", 1, "the sub line"), tracker: false, rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "statement", "sub");
        var written = slots.GetProperty("statement").GetString()!.Split('\n');
        if (written.Length > STATEMENT_MAX_LINES)
        {
            validation.Fail($"{path}.statement", $"at most {STATEMENT_MAX_LINES} lines (got {written.Length})");
        }
        else
        {
            var shown = written.Sum(line => SlideFit.Lines(line, STATEMENT_WIDTH, STATEMENT_FLOOR_SIZE, validation.Widths.Head));
            if (shown > STATEMENT_MAX_LINES)
            {
                validation.Fail($"{path}.statement", $"too long to fit: it wraps to about {shown} lines at the statement's smallest size, and at most {STATEMENT_MAX_LINES} fit");
            }
        }

        if (slots.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.String)
        {
            var lines = SlideFit.Lines(sub.GetString()!, STATEMENT_WIDTH, SUB_SIZE, validation.Widths.Head);
            if (lines > SUB_MAX_LINES)
            {
                validation.Fail($"{path}.sub", $"too long to fit: it wraps to about {lines} lines, and at most {SUB_MAX_LINES} fit");
            }
        }
    }
}
