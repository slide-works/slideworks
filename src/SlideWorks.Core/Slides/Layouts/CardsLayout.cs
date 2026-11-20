using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class CardsLayout
{
    private const int MAX_CARDS = 4;
    private const int GRID_CARDS = 4;
    private const string CARD_FILL = "'soft' (the default) tints it; 'solid' fills it with the tone's colour; 'plain' keeps the card white and draws a top rule in the tone's colour";

    // The widths of a card's text, in px, by arrangement and scale (the layout's K21 table).
    private const double ROW_WIDTH = 467;
    private const double ROW_LARGE_WIDTH = 459;
    private const double GRID_WIDTH = 752;
    private const double GRID_LARGE_WIDTH = 744;
    private const double GRID_GAP = 28;

    private const double ICON_ROW = IconSlot.BLOCK_HEIGHT;
    private const double PICTURE_ROW = 144;
    private const double BADGE_ROW = 48;
    private const double BADGE_TWO_LINES_ROW = 68;
    private const double PIN_ROW = 44;
    private const double RULE = 8;

    public static LayoutDefinition Definition { get; } = new("cards", "1–4 cards, each with an optional picture or icon and a badge, a title, text, chips and a pin. Four cards lay out 2×2. A card can be plain with a top rule, and a callout can sit under the cards. Can reveal one card per step. Light or dark.", LayoutSurface.Either,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("cards", new ListSlot(Obj(
            Opt("icon", new IconSlot()),
            Opt("image", new ImageSlot()),
            Opt("badge", new BlockSlot(InlineBlocks.Badge)),
            Req("title", new TextSlot(70)),
            Req("text", new TextSlot(140)),
            Opt("chips", new ListSlot(new BlockSlot(InlineBlocks.Chip), 0, 4), "Optional. May be empty"),
            Opt("tone", Tone(null), TONE + "; left out, the card is plain"),
            Opt("fill", PanelFill(), CARD_FILL),
            Pin()), 1, MAX_CARDS)),
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

        var cards = slots.GetProperty("cards");
        var grid = cards.GetArrayLength() == GRID_CARDS;
        var large = CardScale.IsLarge(cards);
        var callout = ObjectSlot.IsPresent(slots, "callout", out var block) ? LayoutRules.CalloutHeight(block, 1680, validation.Widths) : 0;
        var body = SlideFit.BODY_HEIGHT - callout;
        var budget = grid ? (body - GRID_GAP) / 2 : body;
        var index = 0;
        foreach (var card in cards.EnumerateArray())
        {
            var height = Height(card, grid, large, validation.Widths);
            if (height > budget)
            {
                SlideFit.Check(height, $"{path}.cards[{index}]", validation, budget);
                return;
            }

            index++;
        }
    }

    // One card's drawn height in px: padding, then the badge, icon, title, text, chips, pin and rule that
    // are there.
    private static double Height(JsonElement card, bool grid, bool large, FitWidths widths)
    {
        var width = (grid, large) switch
        {
            (false, false) => ROW_WIDTH,
            (false, true) => ROW_LARGE_WIDTH,
            (true, false) => GRID_WIDTH,
            (true, true) => GRID_LARGE_WIDTH,
        };
        var (padding, titleSize, titleLine, titleGap, textSize, textLine) = (grid, large) switch
        {
            (false, false) => (68.0, 34, 39.1, 12.0, 24, 33.6),
            (false, true) => (80.0, 50, 56.0, 16.0, 30, 42.0),
            (true, false) => (56.0, 34, 39.1, 12.0, 24, 33.6),
            (true, true) => (64.0, 44, 49.3, 12.0, 28, 39.2),
        };
        var height = padding
            + BadgeHeight(card, width)
            + LeadHeight(card)
            + (SlideFit.Lines(Text(card, "title"), width, titleSize, widths.Head) * titleLine)
            + titleGap
            + (SlideFit.Lines(Text(card, "text"), width, textSize, widths.Body) * textLine)
            + ChipsHeight(card, width)
            + (ObjectSlot.IsPresent(card, "pin", out _) ? PIN_ROW : 0);
        return Text(card, "fill") == "plain" ? height + RULE : height;
    }

    // A picture takes the icon's place: a card with both draws the picture only.
    private static double LeadHeight(JsonElement card)
    {
        if (ObjectSlot.IsPresent(card, "image", out _))
        {
            return PICTURE_ROW;
        }

        return ObjectSlot.IsPresent(card, "icon", out _) ? ICON_ROW : 0;
    }

    // The badge row is 48 px, or 68 when the badge wraps (bold capitals with tracking: 0.85 em, the one
    // factor that is not the validation's widths).
    private static double BadgeHeight(JsonElement card, double width)
    {
        if (!ObjectSlot.IsPresent(card, "badge", out var badge))
        {
            return 0;
        }

        return SlideFit.Lines(badge.GetString()!, width - 28, 16, 0.85) > 1 ? BADGE_TWO_LINES_ROW : BADGE_ROW;
    }

    // Chips pack greedily into the card's width at 32 + 10.1 px a character, 10 px apart; a row is 38 px
    // with 10 px between rows and 22 px of margin.
    private static double ChipsHeight(JsonElement card, double width)
    {
        if (!card.TryGetProperty("chips", out var chips) || chips.GetArrayLength() == 0)
        {
            return 0;
        }

        var rows = 1;
        var used = 0.0;
        foreach (var chip in chips.EnumerateArray())
        {
            var chipWidth = 32 + (10.1 * TextSlot.Length(chip.GetString()!));
            if (used > 0 && used + 10 + chipWidth > width)
            {
                rows++;
                used = chipWidth;
            }
            else
            {
                used += (used > 0 ? 10 : 0) + chipWidth;
            }
        }

        return 22 + (rows * 38) + ((rows - 1) * 10);
    }

    private static string Text(JsonElement card, string name) =>
        card.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
}
