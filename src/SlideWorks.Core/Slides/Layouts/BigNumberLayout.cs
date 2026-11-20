using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class BigNumberLayout
{
    private const int VALUE_MAX = 10;
    private const int NUMBER_PART_MAX = 6;

    public static LayoutDefinition Definition { get; } = new("big-number", "One number that carries the slide, with a caption, a detail and a source. The detail can arrive on a step, and a callout can sit under the number.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline", "caption"),
        Req("value", new TextSlot(VALUE_MAX), "The number with a short unit, such as '11 wks' or '42%'; the layout sets a unit smaller than the number. The number part, up to and including its last digit, is at most 6 characters"),
        Req("caption", new TextSlot(120)),
        Opt("detail", new TextSlot(200)),
        Opt("source", new TextSlot(100), "Where the number comes from, without a 'Source:' prefix"),
        Opt("reveal", Reveal(), "'one-by-one' shows the detail on a step; 'all' (the default) shows it with the slide"),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.When("detail", 1, "the detail"), rules: Rule);

    // The number part mirrors the client's splitValue: any non-digits, the first digit, then digits, dots
    // and commas. A value with no digit is all number part.
    public static int NumberPartLength(string value)
    {
        var first = value.AsSpan().IndexOfAnyInRange('0', '9');
        if (first < 0)
        {
            return value.Length;
        }

        var end = first + 1;
        while (end < value.Length && (value[end] is >= '0' and <= '9' or '.' or ','))
        {
            end++;
        }

        return end;
    }

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline", "caption");
        var number = NumberPartLength(slots.GetProperty("value").GetString()!);
        if (number > NUMBER_PART_MAX)
        {
            validation.Fail($"{path}.value", $"the number part is at most {NUMBER_PART_MAX} characters (got {number}); put the rest in the caption");
        }
    }
}
