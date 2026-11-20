using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class QuoteLayout
{
    private const int HEADLINE_MAX = 80;
    private const int ROLE_MAX = 160;
    private const int STAT_VALUE_MAX = 12;
    private const int STAT_CAPTION_MAX = 90;
    private const int FOLLOW_UP_MAX = 160;
    private const int SOURCE_MAX = 200;

    // The slide's height from y 150 to y 996, and what the layout draws around the quote, in px.
    private const double CONTENT_HEIGHT = 846;
    private const double KICKER_HEIGHT = 42;
    private const double TEXT_WIDTH = 1432;
    private const double TEXT_WIDTH_WITH_STAT = 964;
    private const double HEADLINE_WIDTH = 1560;
    private const double STAT_CAPTION_WIDTH = 360;
    private const double SOURCE_WIDTH = 1480;
    private const double QUOTE_SIZE = 36;

    public static LayoutDefinition Definition { get; } = new("quote", "A quotation with its attribution, and optionally a headline, a supporting stat, a follow-up line and a source.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Opt("headline", new TextSlot(HEADLINE_MAX)),
        Req("quote", new TextSlot(280), "The words only; the layout adds the quotation marks"),
        Req("attribution", new TextSlot(80), "Who said it. With a role, this is the name"),
        Opt("role", new TextSlot(ROLE_MAX), "The speaker's role, and where and when it was said"),
        Opt("stat", Obj(
            Req("value", new TextSlot(STAT_VALUE_MAX)),
            Req("caption", new TextSlot(STAT_CAPTION_MAX)))),
        Opt("follow_up", new TextSlot(FOLLOW_UP_MAX)),
        Opt("source", new TextSlot(SOURCE_MAX), "Without a 'Source:' prefix"),
        Opt("image", new ImageSlot(), "A portrait of the speaker, drawn beside the attribution"),
        Opt("reveal", Reveal(), "'one-by-one' shows the stat, then the follow-up line, on steps; 'all' (the default) shows them with the slide"),
        Emphasis("quote", "headline"),
    ], steps: StepPlan.When("stat", 1, "the stat").Then(StepPlan.When("follow_up", 1, "the follow-up line")), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "quote", "headline");
        if (!HasNewSlot(slots))
        {
            return;
        }

        var widths = validation.Widths;
        var fixedHeight = KICKER_HEIGHT;
        if (Text(slots, "headline") is { } headline)
        {
            fixedHeight += (SlideFit.Lines(headline, HEADLINE_WIDTH, 64, widths.Head) * 69.1) + 46;
        }

        var name = Text(slots, "attribution")!;
        var who = Text(slots, "role") is { } role ? $"{name}, {role}" : name;
        var stat = slots.TryGetProperty("stat", out var statSlot) && statSlot.ValueKind == JsonValueKind.Object ? statSlot : (JsonElement?)null;
        var statHeight = 0.0;
        if (stat is { } value)
        {
            statHeight = 58 + 72 + (SlideFit.Lines(Text(value, "caption")!, STAT_CAPTION_WIDTH, 22, widths.Body) * 28.6);
        }

        var portrait = ObjectSlot.IsPresent(slots, "image", out _);
        var whoWidth = stat is null ? TEXT_WIDTH : TEXT_WIDTH_WITH_STAT;
        if (portrait)
        {
            whoWidth -= 180;
        }

        var whoHeight = SlideFit.Lines(who, whoWidth, 28, widths.Body) * 36.4;
        fixedHeight += 34 + Math.Max(whoHeight, Math.Max(statHeight, portrait ? 160 : 0));
        if (Text(slots, "follow_up") is { } followUp)
        {
            fixedHeight += 69 + (SlideFit.Lines(followUp, TEXT_WIDTH, 32, widths.Body) * 43.2);
        }

        if (Text(slots, "source") is { } source)
        {
            fixedHeight += 24 + (SlideFit.Lines(source, SOURCE_WIDTH, 20, widths.Body) * 27);
        }

        var quote = $"“{Text(slots, "quote")}”";
        SlideFit.Check(SlideFit.Lines(quote, TEXT_WIDTH, QUOTE_SIZE, widths.Head) * 43.9, $"{path}.quote", validation, CONTENT_HEIGHT - fixedHeight);
    }

    private static bool HasNewSlot(JsonElement slots) =>
        new[] { "headline", "role", "stat", "follow_up", "source" }
            .Any(name => slots.TryGetProperty(name, out var found) && found.ValueKind != JsonValueKind.Null);

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() : null;
}
