using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class LevelsLayout
{
    // The ladder's heading is in capitals, which run wider than the body's average: an em factor, not a width from the look.
    private const double LADDER_TITLE_EM = 0.85;

    public static LayoutDefinition Definition { get; } = new("levels", "A ladder of levels with the current ones highlighted, beside a column of points, a paragraph or an image, with optional fact tiles under it.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Emphasis("headline"),
        Req("levels", new BlockSlot(InlineBlocks.Levels), "The ladder, top rung first"),
        Req("main", Column(60, icons: true), "Exactly one of items, text or image; an icon needs a title"),
        Opt("facts", new BlockSlot(InlineBlocks.Facts), "Tiles under the main column"),
        Opt("side", new EnumSlot("right", "left") { Default = "right" }, "'right' (the default) puts the ladder on the right of the main column; 'left' puts it on the left"),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        EmphasisRules.Check(slots, path, validation, "headline");
        var main = slots.GetProperty("main");
        ColumnIconNeedsTitle(main, $"{path}.main", validation);

        // The ladder is a 480 px card with 24 px padding: 432 px inside, and a rung's text has 346 px.
        var ladder = slots.GetProperty("levels");
        var height = 48.0;
        if (ObjectSlot.IsPresent(ladder, "title", out var title))
        {
            height += (SlideFit.Lines(title.GetString()!, 432, 16, LADDER_TITLE_EM) * 19.2) + 16;
        }

        var rungs = ladder.GetProperty("items").EnumerateArray().ToArray();
        height += rungs.Sum(r => RungHeight(r, validation)) + (8 * (rungs.Length - 1));
        SlideFit.Check(height, $"{path}.levels.items", validation);

        // The main column is 1144 px, as two-column's, with the tiles and the callout at its foot.
        var column = 0.0;
        if (ObjectSlot.IsPresent(main, "title", out var mainTitle))
        {
            column += (SlideFit.Lines(mainTitle.GetString()!, 1144, 34, validation.Widths.Head) * 40.8) + 16;
        }

        if (ObjectSlot.IsPresent(main, "items", out var items))
        {
            var list = items.EnumerateArray().ToArray();
            column += list.Sum(i => SlideFit.Lines(SlideFit.Words(i), 1100, 30, validation.Widths.Body) * 40.5) + ((list.Length - 1) * 30);
        }

        if (ObjectSlot.IsPresent(main, "text", out var text))
        {
            column += SlideFit.Lines(text.GetString()!, 1144, 28, validation.Widths.Body) * 39.2;
        }

        if (ObjectSlot.IsPresent(slots, "facts", out var facts))
        {
            column += TilesHeight(facts.EnumerateArray().ToArray(), validation);
        }

        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            column += LayoutRules.CalloutHeight(callout, 1144, validation.Widths);
        }

        SlideFit.Check(column, $"{path}.main", validation);
    }

    // A rung: a 44 px circle and 346 px of 22 px/1.4 text in 10 px padding, at least 64 px.
    private static double RungHeight(JsonElement rung, SlotValidation validation) =>
        Math.Max(64, (SlideFit.Lines(rung.GetProperty("text").GetString()!, 346, 22, validation.Widths.Body) * 30.8) + 20);

    // Equal tiles 20 px apart across 1144 px, each with 22 px of padding either side; the tallest value sets the height.
    private static double TilesHeight(JsonElement[] tiles, SlotValidation validation)
    {
        var inner = ((1144 - (20 * (tiles.Length - 1))) / tiles.Length) - 44;
        var lines = tiles.Max(t => SlideFit.Lines(t.GetProperty("value").GetString()!, inner, 24, validation.Widths.Body));
        return 24 + 66 + (lines * 30);
    }
}
