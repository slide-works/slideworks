using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class NumberedCardsLayout
{
    public static LayoutDefinition Definition { get; } = new("numbered-cards", "2–4 numbered cards, each a title and text, filling the slide. A card can be highlighted with a tone, a fill and a pin. Can reveal one card per step and bring the callout in last.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("cards", new ListSlot(Obj(
            Req("title", new TextSlot(40)),
            Req("text", new TextSlot(140)),
            Opt("tone", Tone(null), TONE + "; left out, the card is plain"),
            Opt("fill", PanelFill(), PANEL_FILL),
            Pin()), 2, 4)),
        Opt("reveal", Reveal(), REVEAL),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], steps: StepPlan.List("cards"), rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        if (validation.ErrorCount > 0)
        {
            return;
        }

        // The row rule: 2 or 3 cards share three columns (477 px of text in each), 4 keep four (335 px).
        // Regular cards: 72 px of padding, a 64 px numeral, 22 px gap, 30/1.2 title, 14 px gap, 23/1.4 text.
        // Large cards (CardScale): 80, 96, 26, 40/1.15 title, 14, 28/1.4 text. A pin adds 44 px, a plain fill 8.
        var cards = slots.GetProperty("cards");
        var large = CardScale.IsLarge(cards);
        var width = cards.GetArrayLength() == 4 ? 335 : 477;
        var budget = SlideFit.BODY_HEIGHT;
        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            budget -= LayoutRules.CalloutHeight(callout, 1680, validation.Widths);
        }

        var index = 0;
        foreach (var card in cards.EnumerateArray())
        {
            var title = card.GetProperty("title").GetString()!;
            var text = card.GetProperty("text").GetString()!;
            var height = large
                ? 80 + 96 + 26 + (SlideFit.Lines(title, width, 40, validation.Widths.Head) * 46) + 14 + (SlideFit.Lines(text, width, 28, validation.Widths.Body) * 39.2)
                : 72 + 64 + 22 + (SlideFit.Lines(title, width, 30, validation.Widths.Head) * 36) + 14 + (SlideFit.Lines(text, width, 23, validation.Widths.Body) * 32.2);
            if (card.TryGetProperty("pin", out var pin) && pin.ValueKind == JsonValueKind.String)
            {
                height += 44;
            }

            if (card.TryGetProperty("fill", out var fill) && fill.ValueKind == JsonValueKind.String && fill.GetString() == "plain")
            {
                height += 8;
            }

            var before = validation.ErrorCount;
            SlideFit.Check(height, $"{path}.cards[{index}]", validation, budget);
            if (validation.ErrorCount > before)
            {
                return;
            }

            index++;
        }
    }
}
